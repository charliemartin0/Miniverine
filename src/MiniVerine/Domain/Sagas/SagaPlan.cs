namespace MiniVerine.Domain.Sagas;

/// <summary>
/// Folder: saga identity and timeout as data, not as a running timer.
///
/// Put here: saga Id, [SagaIdentity] / {Type}Id / Id conventions, [Timeout] as
/// delay metadata on a message type. The saga class is inert state.
///
/// Do not put here: Marten documents, Start/Handle dispatch, or PlayScheduledMessagesAsync.
/// Persistence is Infrastructure (or MiniVerine.Postgresql). Dispatch is Application/Sagas.
///
/// Prove with: given a message, you can say which saga instance it belongs to without I/O.
/// </summary>
public sealed class SagaPlan;
