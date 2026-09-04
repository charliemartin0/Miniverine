using MiniVerine.Application.Bus;
using MiniVerine.Application.Cascades;
using MiniVerine.Application.Discovery;
using MiniVerine.Application.Execution;
using MiniVerine.Application.Sagas;
using MiniVerine.Application.Scheduling;
using MiniVerine.Application.Tracking;
using MiniVerine.Domain.Envelope;
using MiniVerine.Domain.Envelope.ValueObjects;
using MiniVerine.Domain.Messaging;
using MiniVerine.Domain.Messaging.ValueObjects;
using MiniVerine.Domain.Sagas;
using MiniVerine.Domain.Sagas.ValueObjects;

namespace MiniVerine.Application.Mediator;

public sealed class Mediator : IMessageBus
{
    private static readonly AsyncLocal<bool> InHandler = new();

    private readonly Executor _executor;
    private readonly OutgoingDispatcher _dispatcher;
    private readonly ISagaStore _sagas;
    private readonly MessageScheduler _scheduler;
    private readonly Queue<(object Body, Envelope Parent)> _worklist = [];
    private readonly object _trackGate = new();

    private SessionLog? _log;
    private bool _tracking;

    public HandlerCatalog Catalog { get; }

    public Mediator(
        HandlerCatalog catalog,
        ICascadePublisher? cascades = null,
        Executor? executor = null,
        IScheduledEnvelopeHold? hold = null,
        ISagaStore? sagas = null,
        ErrorPolicyCatalog? policies = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Catalog = catalog;
        IScheduledEnvelopeHold inner = hold ?? new InMemoryScheduledEnvelopeHold();
        var scheduled = new RecordingScheduledEnvelopeHold(inner, OnPark);
        _dispatcher = new OutgoingDispatcher(scheduled, cascades, OnImmediate);
        _executor = executor ?? new Executor(
            policies ?? new ErrorPolicyCatalog(),
            scheduled: scheduled,
            attempts: new SessionAttemptObserver(this));
        _sagas = sagas ?? new InMemorySagaStore();
        _scheduler = new MessageScheduler(catalog, _executor, scheduled, _dispatcher);
    }

    public Task InvokeAsync(object message, CancellationToken cancellationToken = default) =>
        InvokeAsync(message, new DeliveryOptions(), cancellationToken);

    public async Task InvokeAsync(
        object message,
        DeliveryOptions options,
        CancellationToken cancellationToken = default)
    {
        Envelope envelope = EnvelopeForInvoke(message, options);
        await DispatchHandlers(envelope, scheduled: false, cancellationToken);
    }

    public Task<TResult> InvokeAsync<TResult>(object message, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task PublishAsync(object message, CancellationToken cancellationToken = default) =>
        PublishAsync(message, new DeliveryOptions(), cancellationToken);

    public Task PublishAsync(
        object message,
        DeliveryOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(options);
        cancellationToken.ThrowIfCancellationRequested();
        if (_dispatcher.TryPark(message, options))
        {
            return Task.CompletedTask;
        }

        throw new NotSupportedException("PublishAsync is implemented by Routing, not Mediator.");
    }

    public Task<TrackedSession> InvokeTrackedAsync(
        object message,
        CancellationToken cancellationToken = default) =>
        InvokeTrackedAsync(message, new DeliveryOptions(), cancellationToken);

    public async Task<TrackedSession> InvokeTrackedAsync(
        object message,
        DeliveryOptions options,
        CancellationToken cancellationToken = default)
    {
        using IDisposable session = BeginTrack();
        try
        {
            Envelope envelope = EnvelopeForInvoke(message, options);
            await DispatchHandlers(envelope, scheduled: false, cancellationToken);
            await DrainAsync(cancellationToken);
            return Freeze();
        }
        catch (HandlerFault fault)
        {
            fault.Session = Freeze();
            throw;
        }
    }

    public Task<TrackedSession> PublishTrackedAsync(
        object message,
        CancellationToken cancellationToken = default) =>
        PublishTrackedAsync(message, new DeliveryOptions(), cancellationToken);

    public async Task<TrackedSession> PublishTrackedAsync(
        object message,
        DeliveryOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(options);
        using IDisposable session = BeginTrack();
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (_dispatcher.TryPark(message, options))
            {
                return Freeze();
            }

            _log!.Published.Add(new PublishedRecord(message));
            Envelope envelope = EnvelopeForPublish(message);
            await DispatchHandlers(envelope, scheduled: true, cancellationToken);
            await DrainAsync(cancellationToken);
            return Freeze();
        }
        catch (HandlerFault fault)
        {
            fault.Session = Freeze();
            throw;
        }
    }

    private IDisposable BeginTrack()
    {
        if (InHandler.Value)
        {
            throw new NestedTrackNotSupported();
        }

        lock (_trackGate)
        {
            if (_tracking)
            {
                throw new TrackInProgress();
            }

            _tracking = true;
            _log = new SessionLog();
            _worklist.Clear();
        }

        return new TrackScope(this);
    }

    private async Task DrainAsync(CancellationToken cancellationToken)
    {
        while (_worklist.Count > 0)
        {
            cancellationToken.ThrowIfCancellationRequested();
            (object body, Envelope parent) = _worklist.Dequeue();
            Envelope child = EnvelopeForDescendant(body, parent);
            await DispatchHandlers(child, scheduled: true, cancellationToken);
        }
    }

    /// <summary>
    /// Drain and tracked Publish use Scheduled kind and inherit conversation/correlation ids.
    /// Public InvokeAsync stays Invoke-kind and mints new ids.
    /// </summary>
    private async Task DispatchHandlers(Envelope envelope, bool scheduled, CancellationToken cancellationToken)
    {
        HandlerLookup lookup = Catalog.Lookup(envelope.Message.Value.GetType());
        if (lookup is MissingHandler)
        {
            await _executor.HandleMissingAsync(envelope, cancellationToken);
            return;
        }

        foreach (DiscoveredHandler handler in ((FoundHandlers)lookup).Handlers)
        {
            DiscoveredHandler target = scheduled ? handler with { Scheduled = true } : handler;
            InHandler.Value = true;
            try
            {
                object? result = IsSagaHandler(target)
                    ? await InvokeSagaAsync(envelope, target, cancellationToken)
                    : await _executor.InvokeAsync(envelope, target, cancellationToken);
                IReadOnlyList<object> outgoing = CascadingMessages.From(result);
                if (outgoing.Count > 0)
                {
                    _dispatcher.Dispatch(outgoing, envelope);
                }
            }
            finally
            {
                InHandler.Value = false;
            }
        }
    }

    private async Task<object?> InvokeSagaAsync(
        Envelope envelope,
        DiscoveredHandler handler,
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        object message = envelope.Message.Value;
        Type sagaType = handler.HandlerType;
        SagaId sagaId = SagaIdentityNaming.For(message, sagaType);
        if (string.IsNullOrEmpty(sagaId.Value))
        {
            throw new SagaIdRequired(sagaType, message.GetType());
        }

        Envelope sagaEnvelope = envelope with { SagaId = sagaId };
        Saga? row = _sagas.Load(sagaType, sagaId);

        if (HandlerConvention.IsStart(handler.Method.Name))
        {
            if (row is not null)
            {
                throw new SagaAlreadyExists(sagaType, sagaId);
            }

            object? started = null;
            object? result = await _executor.InvokeAsync(
                sagaEnvelope,
                handler with { ResolveTarget = () => started = Activator.CreateInstance(sagaType) },
                cancellationToken);
            _sagas.Save(sagaType, sagaId, (Saga)started!);
            return result;
        }

        if (row is null || row.IsCompleted)
        {
            return await InvokeNotFoundAsync(sagaEnvelope, handler, sagaId, cancellationToken);
        }

        object? loaded = null;
        object? handled = await _executor.InvokeAsync(
            sagaEnvelope,
            handler with { ResolveTarget = () => loaded = _sagas.Load(sagaType, sagaId) },
            cancellationToken);
        _sagas.Save(sagaType, sagaId, (Saga)loaded!);
        return handled;
    }

    private async Task<object?> InvokeNotFoundAsync(
        Envelope envelope,
        DiscoveredHandler handler,
        SagaId sagaId,
        CancellationToken cancellationToken)
    {
        DiscoveredHandler? notFound = NotFoundConvention.For(handler.HandlerType, handler.MessageClrType);
        if (notFound is null)
        {
            throw new SagaInstanceNotFound(handler.HandlerType, sagaId, handler.MessageClrType);
        }

        return await _executor.InvokeAsync(
            envelope,
            notFound with { ResolveTarget = () => Activator.CreateInstance(handler.HandlerType) },
            cancellationToken);
    }

    private static bool IsSagaHandler(DiscoveredHandler handler) =>
        handler.HandlerType != typeof(Saga) && handler.HandlerType.IsAssignableTo(typeof(Saga));

    private Envelope EnvelopeForInvoke(object message, DeliveryOptions options)
    {
        ArgumentNullException.ThrowIfNull(message);
        ArgumentNullException.ThrowIfNull(options);
        if (options.Delay is not null && options.Until is not null)
        {
            throw new AmbiguousDeliveryOptions();
        }

        if (options.HasSchedule)
        {
            throw new DelayedInvokeNotSupported();
        }

        return EnvelopeForPublish(message);
    }

    private static Envelope EnvelopeForPublish(object message)
    {
        DateTimeOffset sent = DateTimeOffset.UtcNow;
        return new Envelope(
            new EnvelopeId(Guid.NewGuid()),
            new Message(message),
            MessageTypeNaming.For(message.GetType()),
            new Destination(new Uri("local://invoke/")),
            new CorrelationId(Guid.NewGuid()),
            new ConversationId(Guid.NewGuid()),
            new SagaId(""),
            new SentAt(sent),
            new DeliverBy(null),
            new Headers(),
            new ContentType(""),
            new Attempts(1),
            new EnvelopeData());
    }

    private static Envelope EnvelopeForDescendant(object message, Envelope parent)
    {
        DateTimeOffset sent = DateTimeOffset.UtcNow;
        return new Envelope(
            new EnvelopeId(Guid.NewGuid()),
            new Message(message),
            MessageTypeNaming.For(message.GetType()),
            new Destination(new Uri("local://invoke/")),
            parent.CorrelationId,
            parent.ConversationId,
            parent.SagaId,
            new SentAt(sent),
            new DeliverBy(null),
            new Headers(),
            new ContentType(""),
            new Attempts(1),
            new EnvelopeData());
    }

    private void OnPark(Envelope envelope)
    {
        _log?.Scheduled.Add(new ScheduledRecord(envelope));
    }

    private void OnImmediate(object message, Envelope parent)
    {
        if (_log is null)
        {
            return;
        }

        _log.Published.Add(new PublishedRecord(message));
        _worklist.Enqueue((message, parent));
    }

    private TrackedSession Freeze()
    {
        SessionLog log = _log ?? new SessionLog();
        return new TrackedSession(
            [.. log.Executed],
            [.. log.Published],
            [.. log.Scheduled],
            PlayScheduledMessagesAsync);
    }

    private async Task<TrackedSession> PlayScheduledMessagesAsync(
        DateTimeOffset asOf,
        CancellationToken cancellationToken)
    {
        using IDisposable session = BeginTrack();
        try
        {
            await _scheduler.PlayDue(asOf, cancellationToken);
            await DrainAsync(cancellationToken);
            return Freeze();
        }
        catch (HandlerFault fault)
        {
            fault.Session = Freeze();
            throw;
        }
    }

    private void EndTrack()
    {
        lock (_trackGate)
        {
            _tracking = false;
            _log = null;
            _worklist.Clear();
        }
    }

    private sealed class SessionLog
    {
        public List<ExecutedRecord> Executed { get; } = [];

        public List<PublishedRecord> Published { get; } = [];

        public List<ScheduledRecord> Scheduled { get; } = [];
    }

    private sealed class SessionAttemptObserver : IHandlerAttemptObserver
    {
        private readonly Mediator _mediator;

        public SessionAttemptObserver(Mediator mediator) => _mediator = mediator;

        public void OnAttempt(Envelope envelope, DiscoveredHandler handler, Exception? exception)
        {
            _mediator._log?.Executed.Add(
                new ExecutedRecord(envelope.Message.Value, envelope, envelope.Attempts.Value, exception));
        }
    }

    private sealed class TrackScope : IDisposable
    {
        private readonly Mediator _mediator;

        public TrackScope(Mediator mediator) => _mediator = mediator;

        public void Dispose() => _mediator.EndTrack();
    }
}
