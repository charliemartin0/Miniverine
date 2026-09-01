using System.Reflection;
using System.Runtime.ExceptionServices;
using MiniVerine.Application.Bus;
using MiniVerine.Application.Cascades;
using MiniVerine.Application.Discovery;
using MiniVerine.Application.Discovery.ValueObjects;

namespace MiniVerine.Application.Mediator;

public sealed class Mediator : IMessageBus
{
    private readonly ICascadePublisher? _cascades;

    public HandlerCatalog Catalog { get; }

    public Mediator(HandlerCatalog catalog, ICascadePublisher? cascades = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        Catalog = catalog;
        _cascades = cascades;
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

    private async Task InvokeFound(FoundHandlers found, object message)
    {
        foreach (DiscoveredHandler handler in found.Handlers)
        {
            object? target = handler.IsStatic ? null : Activator.CreateInstance(handler.HandlerType);
            object? result;
            try
            {
                result = handler.Method.Invoke(target, [message]);
                if (result is Task task)
                {
                    await task;
                }
            }
            catch (TargetInvocationException ex) when (ex.InnerException is not null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
                throw;
            }

            IReadOnlyList<object> outgoing = CascadingMessages.From(result);
            if (outgoing.Count > 0)
            {
                _cascades?.Publish(outgoing);
            }
        }
    }
}
