using System.Reflection;
using MiniVerine.Application.Discovery.ValueObjects;

namespace MiniVerine.Application.Discovery;

/// <summary>
/// Whether a method is a handler by convention. Handle / HandleAsync / Consume / ConsumeAsync / Start / StartAsync.
/// First parameter is the message; extra parameters are injection slots.
/// </summary>
public static class HandlerConvention
{
    public static DiscoveredHandler? For(MethodInfo method)
    {
        ArgumentNullException.ThrowIfNull(method);
        return null;
    }
}
