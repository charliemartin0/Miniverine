namespace MiniVerine.Application.Mediator;

/// <summary>
/// Folder: InvokeAsync — the MediatR door.
///
/// Put here: run the matching handler(s) on the caller’s thread and wait.
/// InvokeAsync&lt;TResult&gt; returns a value. Retry-now / retry-with-cooldown happen
/// inside that await. Requeue and dead-letter barely apply: there is no queue in front
/// of the caller.
///
/// Do not put here: named local queues, brokers, or “send the next command” from inside
/// a handler. The next step is a cascade (Application/Cascades), not another Invoke.
///
/// Prove with: await InvokeAsync(new LogIncident(...)) does not return until Handle returns.
/// </summary>
public sealed class MediatorPlan;
