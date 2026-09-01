using MiniVerine.Application.Bus;
using MiniVerine.Application.Discovery;
using MiniVerine.Application.Discovery.ValueObjects;

namespace MiniVerine.Application.Mediator;

public sealed class Mediator : IMessageBus
{
    public HandlerCatalog Catalog { get; }

    public Mediator(HandlerCatalog catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Catalog = catalog;
    }

    public Task InvokeAsync(object message, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(message);
        HandlerLookup lookup = Catalog.Lookup(message.GetType());
        if (lookup is MissingHandler)
        {
            throw new HandlerNotFound(message.GetType());
        }

        return InvokeFound((FoundHandlers)lookup, message);
    }

    public Task<TResult> InvokeAsync<TResult>(object message, CancellationToken cancellationToken = default) =>
        throw new NotImplementedException();

    public Task PublishAsync(object message, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("PublishAsync is implemented by Routing, not Mediator.");

    private static async Task InvokeFound(FoundHandlers found, object message)
    {
        foreach (DiscoveredHandler handler in found.Handlers)
        {
            object? target = handler.IsStatic ? null : Activator.CreateInstance(handler.HandlerType);
            object? result = handler.Method.Invoke(target, [message]);
            if (result is Task task)
            {
                await task;
            }
        }
    }
}
