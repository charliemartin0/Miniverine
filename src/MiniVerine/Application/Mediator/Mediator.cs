using MiniVerine.Application.Bus;
using MiniVerine.Application.Cascades;
using MiniVerine.Application.Discovery;
using MiniVerine.Application.Execution;
using MiniVerine.Application.Sagas;
using MiniVerine.Application.Scheduling;
using MiniVerine.Domain.Envelope;
using MiniVerine.Domain.Envelope.ValueObjects;
using MiniVerine.Domain.Messaging;
using MiniVerine.Domain.Messaging.ValueObjects;
using MiniVerine.Domain.Sagas;
using MiniVerine.Domain.Sagas.ValueObjects;

namespace MiniVerine.Application.Mediator;

public sealed class Mediator : IMessageBus
{
    private readonly Executor _executor;
    private readonly OutgoingDispatcher _dispatcher;
    private readonly ISagaStore _sagas;

    public HandlerCatalog Catalog { get; }

    public Mediator(
        HandlerCatalog catalog,
        ICascadePublisher? cascades = null,
        Executor? executor = null,
        IScheduledEnvelopeHold? hold = null,
        ISagaStore? sagas = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Catalog = catalog;
        IScheduledEnvelopeHold scheduled = hold ?? new InMemoryScheduledEnvelopeHold();
        _dispatcher = new OutgoingDispatcher(scheduled, cascades);
        _executor = executor ?? new Executor(new ErrorPolicyCatalog(), scheduled: scheduled);
        _sagas = sagas ?? new InMemorySagaStore();
    }

    public Task InvokeAsync(object message, CancellationToken cancellationToken = default) =>
        InvokeAsync(message, new DeliveryOptions(), cancellationToken);

    public async Task InvokeAsync(
        object message,
        DeliveryOptions options,
        CancellationToken cancellationToken = default)
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

        Envelope envelope = EnvelopeForInvoke(message);
        HandlerLookup lookup = Catalog.Lookup(message.GetType());
        if (lookup is MissingHandler)
        {
            await _executor.HandleMissingAsync(envelope, cancellationToken);
            return;
        }

        foreach (DiscoveredHandler handler in ((FoundHandlers)lookup).Handlers)
        {
            Envelope handlerEnvelope = envelope;
            object? result = IsSagaHandler(handler)
                ? await InvokeSagaAsync(handlerEnvelope, handler, cancellationToken)
                : await _executor.InvokeAsync(
                    handlerEnvelope,
                    handler,
                    cancellationToken);
            IReadOnlyList<object> outgoing = CascadingMessages.From(result);
            if (outgoing.Count > 0)
            {
                _dispatcher.Dispatch(outgoing, envelope);
            }
        }
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

    private static Envelope EnvelopeForInvoke(object message)
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
}
