using MiniVerine.Application.Cascades;
using MiniVerine.Application.Discovery;
using MiniVerine.Application.Execution;
using MiniVerine.Domain.Envelope;

namespace MiniVerine.Application.Scheduling;

public interface IMessageScheduler
{
    Task PlayDue(DateTimeOffset asOf, CancellationToken cancellationToken = default);
}

public sealed class MessageScheduler : IMessageScheduler
{
    private readonly HandlerCatalog _catalog;
    private readonly Executor _executor;
    private readonly IScheduledEnvelopeHold _hold;
    private readonly OutgoingDispatcher _dispatcher;

    public MessageScheduler(
        HandlerCatalog catalog,
        Executor executor,
        IScheduledEnvelopeHold hold,
        OutgoingDispatcher dispatcher)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(executor);
        ArgumentNullException.ThrowIfNull(hold);
        ArgumentNullException.ThrowIfNull(dispatcher);
        _catalog = catalog;
        _executor = executor;
        _hold = hold;
        _dispatcher = dispatcher;
    }

    public async Task PlayDue(DateTimeOffset asOf, CancellationToken cancellationToken = default)
    {
        using IDisposable play = _hold.BeginPlay();
        IReadOnlyList<Envelope> snapshot = [.. Due(asOf)];
        foreach (Envelope envelope in snapshot)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!_hold.TryRemove(envelope.Id))
            {
                continue;
            }

            try
            {
                await InvokeDue(envelope, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                _hold.Park(envelope);
                throw;
            }
        }
    }

    private IEnumerable<Envelope> Due(DateTimeOffset asOf) =>
        _hold.Peek()
            .Where(envelope => envelope.DeliverBy.Value is { } due && due <= asOf)
            .OrderBy(envelope => envelope.DeliverBy.Value)
            .ThenBy(envelope => IndexOf(envelope));

    private int IndexOf(Envelope envelope)
    {
        IReadOnlyList<Envelope> held = _hold.Peek();
        for (int i = 0; i < held.Count; i++)
        {
            if (held[i].Id.Value == envelope.Id.Value)
            {
                return i;
            }
        }

        return int.MaxValue;
    }

    private async Task InvokeDue(Envelope envelope, CancellationToken cancellationToken)
    {
        HandlerLookup lookup = _catalog.Lookup(envelope.Message.Value.GetType());
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
                InvocationKind.Scheduled);
            IReadOnlyList<object> outgoing = CascadingMessages.From(result);
            if (outgoing.Count > 0)
            {
                _dispatcher.Dispatch(outgoing, envelope);
            }
        }
    }
}
