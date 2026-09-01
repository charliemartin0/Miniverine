namespace MiniVerine.Application.Cascades;

/// <summary>
/// Folder: the in-memory outbox. Decide in the handler; emit only after success.
///
/// Put here: handler return values become outgoing messages (object, IEnumerable,
/// tuples, OutgoingMessages). IMessageContext for extra sends. Failure → no cascades.
/// RespondToSender can be stubbed at first.
///
/// Do not put here: Postgres rows. Durable outbox is the same rule persisted
/// (Infrastructure/Persistence). Do not InvokeAsync the next saga step from here.
///
/// Prove with: a throwing handler publishes nothing; a succeeding handler publishes
/// exactly its return values, after it returns.
/// </summary>
public sealed class CascadesPlan;
