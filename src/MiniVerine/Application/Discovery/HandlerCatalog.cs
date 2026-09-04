using System.Reflection;
using MiniVerine.Domain.Messaging;

namespace MiniVerine.Application.Discovery;

/// <summary>
/// Message type to handler method(s). Opt-in types and assemblies; include/exclude filters.
/// Missing is a lookup result, not an exception.
/// </summary>
public sealed class HandlerCatalog
{
    private static readonly BindingFlags HandlerMethods =
        BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly;

    private readonly List<DiscoveredHandler> _handlers = [];
    private readonly HashSet<Type> _scannedTypes = [];
    private readonly HashSet<string> _includedNamespaces = new(StringComparer.Ordinal);
    private readonly HashSet<string> _excludedNamespaces = new(StringComparer.Ordinal);
    private readonly HashSet<Type> _excludedTypes = [];

    public IReadOnlyList<DiscoveredHandler> Handlers => _handlers;

    public MessageTypeCatalog MessageTypes { get; } = new();

    public void Scan(Assembly assembly)
    {
        ArgumentNullException.ThrowIfNull(assembly);
        foreach (Type type in assembly.GetTypes())
        {
            Scan(type);
        }
    }

    public void Scan(Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(handlerType);
        if (!_scannedTypes.Add(handlerType))
        {
            return;
        }

        if (handlerType.IsAbstract)
        {
            return;
        }

        if (!ShouldScan(handlerType))
        {
            return;
        }

        foreach (MethodInfo method in handlerType.GetMethods(HandlerMethods))
        {
            DiscoveredHandler? discovered = HandlerConvention.For(method);
            if (discovered is null)
            {
                continue;
            }

            _handlers.Add(discovered);
            MessageTypes.Register(discovered.MessageClrType);
        }
    }

    public void IncludeNamespace(string namespaceName)
    {
        ArgumentNullException.ThrowIfNull(namespaceName);
        _includedNamespaces.Add(namespaceName);
    }

    public void ExcludeNamespace(string namespaceName)
    {
        ArgumentNullException.ThrowIfNull(namespaceName);
        _excludedNamespaces.Add(namespaceName);
    }

    public void ExcludeType(Type handlerType)
    {
        ArgumentNullException.ThrowIfNull(handlerType);
        _excludedTypes.Add(handlerType);
    }

    public HandlerLookup Lookup(Type messageType)
    {
        ArgumentNullException.ThrowIfNull(messageType);
        DiscoveredHandler[] found = [.. _handlers.Where(handler => handler.MessageClrType == messageType)];
        return found.Length == 0
            ? new MissingHandler(messageType)
            : new FoundHandlers(found);
    }

    private bool ShouldScan(Type handlerType)
    {
        if (_excludedTypes.Contains(handlerType))
        {
            return false;
        }

        if (InAnyNamespace(handlerType.Namespace, _excludedNamespaces))
        {
            return false;
        }

        if (_includedNamespaces.Count == 0)
        {
            return true;
        }

        return InAnyNamespace(handlerType.Namespace, _includedNamespaces);
    }

    private static bool InAnyNamespace(string? ns, HashSet<string> prefixes)
    {
        if (ns is null)
        {
            return false;
        }

        foreach (string prefix in prefixes)
        {
            if (ns == prefix || ns.StartsWith(prefix + ".", StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }
}
