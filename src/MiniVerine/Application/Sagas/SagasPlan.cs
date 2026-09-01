namespace MiniVerine.Application.Sagas;

/// <summary>
/// Folder: process-manager runtime. Load by id, run Start / Handle / Complete / NotFound.
///
/// Put here: orchestration conventions, MarkCompleted, optimistic concurrency as a
/// rule (version on the saga). ISagaStore as a port — this folder must not know Marten.
///
/// Do not put here: the document schema or JSON serialization of saga state. Domain/Sagas
/// owns identity; this folder owns the conversation; Infrastructure/Persistence (or
/// MiniVerine.Postgresql) owns the rows.
///
/// Prove with: PaymentCharged loads the same instance Start created; after complete,
/// timeout hits NotFound instead of failing.
/// </summary>
public sealed class SagasPlan;
