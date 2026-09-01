using System.Reflection;
using MiniVerine.Application.Discovery.ValueObjects;
using MiniVerine.Domain.Messaging;

namespace MiniVerine.Application.Discovery;

/// <summary>
/// Message type to handler method(s). Opt-in types and assemblies; include/exclude filters.
/// Missing is a lookup result, not an exception.
/// </summary>
public sealed class HandlerCatalog
{
    public IReadOnlyList<DiscoveredHandler> Handlers => [];

    public MessageTypeCatalog MessageTypes { get; } = new();

    public void Scan(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
    }

    public void Scan(Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(handlerType);
    }

    public void IncludeNamespace(string namespaceName)
    {
        ArgumentNullException.ThrowIfNull(namespaceName);
    }

    public void ExcludeNamespace(string namespaceName)
    {
        ArgumentNullException.ThrowIfNull(namespaceName);
    }

    public void ExcludeType(Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(handlerType);
    }

    public HandlerLookup Lookup(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        return new MissingHandler(messageType);
    }
}
