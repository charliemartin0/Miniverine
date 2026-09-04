using MiniVerine.Application.Bus;
using MiniVerine.Application.Cascades;
using MiniVerine.Application.Discovery;
using MiniVerine.Application.Execution;
using MiniVerine.Application.Scheduling;
using MiniVerine.Domain.Envelope;
using MiniVerine.Domain.Envelope.ValueObjects;
using MiniVerine.Domain.Messaging;
using MiniVerine.Domain.Messaging.ValueObjects;
using MiniVerine.Domain.Sagas.ValueObjects;

namespace MiniVerine.Application.Mediator;

public sealed class Mediator : IMessageBus
{
    private readonly Executor _executor;
    private readonly OutgoingDispatcher _dispatcher;

    public HandlerCatalog Catalog { get; }

    public Mediator(
        HandlerCatalog catalog,
        ICascadePublisher? cascades = null,
        Executor? executor = null,
        IScheduledEnvelopeHold? hold = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Catalog = catalog;
        IScheduledEnvelopeHold scheduled = hold ?? new InMemoryScheduledEnvelopeHold();
        _dispatcher = new OutgoingDispatcher(scheduled, cascades);
        _executor = executor ?? new Executor(new ErrorPolicyCatalog(), scheduled: scheduled);
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
            object? result = await _executor.InvokeAsync(
                envelope,
                handler,
                cancellationToken,
                InvocationKind.Invoke);
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
