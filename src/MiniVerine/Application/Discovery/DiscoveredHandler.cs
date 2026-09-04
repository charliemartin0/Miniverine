using System.Reflection;

namespace MiniVerine.Application.Discovery;

/// <summary>
/// A public handler method found by convention. Extra parameters are injection slots, not resolved here.
/// </summary>
public sealed record DiscoveredHandler(
    MethodInfo Method,
    Type HandlerType,
    Type MessageClrType,
    bool IsStatic,
    IReadOnlyList<ParameterInfo> InjectionSlots,
    Func<object?>? ResolveTarget = null,
    bool Scheduled = false);
