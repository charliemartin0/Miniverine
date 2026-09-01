namespace MiniVerine.Application.Routing;

/// <summary>
/// Folder: message type → destination URI.
///
/// Put here: default local queue per type, PublishMessage&lt;T&gt;().ToLocalQueue("payments"),
/// [LocalQueue] override, later rabbitmq:// and tcp://. Routing is a table of rules,
/// not a switch inside each handler.
///
/// Do not put here: the TPL Dataflow block or the Rabbit client. Those are Infrastructure
/// that *obey* a destination this folder chose.
///
/// Prove with: ChargePayment always gets destination local://payments/ without the saga
/// naming a queue in Start.
/// </summary>
public sealed class RoutingPlan;
