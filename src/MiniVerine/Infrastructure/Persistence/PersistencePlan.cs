namespace MiniVerine.Infrastructure.Persistence;

/// <summary>
/// Folder: ports for inbox, outbox, dead letter, saga store. Dual-write is the reason.
///
/// Put here: IMessageStore-shaped interfaces (incoming, outgoing, dead letter, recover
/// on start, duplicate id). Transactional middleware contract: if the handler has a
/// session/connection, wrap and flush outbox in the same commit. In-memory store is
/// enough until MiniVerine.Postgresql implements the same ports.
///
/// Do not put here: Npgsql, Marten, or SQL scripts. Those belong in MiniVerine.Postgresql.
/// Do not “save then publish” from a handler.
///
/// Prove with: after a successful Start, stopping the host still leaves ChargePayment
/// recoverable; a throwing Start leaves no outgoing row.
/// </summary>
public sealed class PersistencePlan;
