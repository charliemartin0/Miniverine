using MiniVerine.Application.Bus;
using MiniVerine.Application.Cascades;
using MiniVerine.Application.Discovery;
using MiniVerine.Application.Execution;
using MiniVerine.Domain.Envelope;
using MiniVerine.Domain.Envelope.ValueObjects;
using MiniVerine.Domain.Messaging;
using MiniVerine.Domain.Messaging.ValueObjects;
using MiniVerine.Domain.Sagas.ValueObjects;

namespace MiniVerine.Application.Mediator;

public sealed class Mediator : IMessageBus
{
    private readonly ICascadePublisher? _cascades;
    private readonly Executor _executor;

    public HandlerCatalog Catalog { get; }

    public Mediator(
        HandlerCatalog catalog,
        ICascadePublisher? cascades = null,
        Executor? executor = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Catalog = catalog;
        _cascades = cascades;
        _executor = executor ?? new Executor(new ErrorPolicyCatalog());
    }

    public async Task InvokeAsync(object message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        Envelope envelope = EnvelopeForInvoke(message);
        HandlerLookup lookup = Catalog.Lookup(message.GetType());
        if (lookup is MissingHandler)
        {
            await _executor.HandleMissingAsync(envelope, cancellationToken);
            return;
        }

        foreach (DiscoveredHandler handler in ((FoundHandlers)lookup).Handlers)
        {
            object? result = await _executor.InvokeAsync(envelope, handler, cancellationToken);
            IReadOnlyList<object> outgoing = CascadingMessages.From(result);
            if (outgoing.Count > 0)
            {
                _cascades?.Publish(outgoing);
            }
        }
    }

    public Task<TResult> InvokeAsync<TResult>(object message, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task PublishAsync(object message, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("PublishAsync is implemented by Routing, not Mediator.");

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
