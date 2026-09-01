namespace MiniVerine.Application.Discovery;

/// <summary>
/// Folder: find handlers by convention, not IRequestHandler&lt;T&gt;.
///
/// Put here: rules for public Handle / HandleAsync / Consume, static vs instance,
/// first parameter is the message, extra parameters are method-injection slots,
/// opt-in assemblies, include/exclude filters, missing-handler policy hook.
///
/// Do not put here: MethodInfo.Invoke vs compiled delegates (that is how Discovery is
/// *executed* — keep the catalog here, the invoker in Execution). Do not codegen yet.
///
/// Prove with: given an assembly, you can list message type → handler method(s).
/// </summary>
public sealed class DiscoveryPlan;
