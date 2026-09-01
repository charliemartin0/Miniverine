namespace MiniVerine.Application.Execution;

/// <summary>
/// Folder: wrap one handler call — log, duration, Attempts, error policy, missing handler.
///
/// Put here: retry / retry-with-cooldown / requeue / schedule-retry / discard /
/// move-to-error-queue, by exception type and/or message type. InvokeAsync only applies
/// Retry and Retry-with-cooldown (match Wolverine). IMissingHandler. Fan-out: multiple
/// handlers for one type.
///
/// Do not put here: OpenTelemetry exporters (Infrastructure/Observability) or the durable
/// error table (Infrastructure/Persistence). Policies are declared here; storage is a port.
///
/// Prove with: the same Envelope comes back with Attempts 1, 2, 3; then error-queue
/// if the policy says so.
/// </summary>
public sealed class ExecutionPlan;
