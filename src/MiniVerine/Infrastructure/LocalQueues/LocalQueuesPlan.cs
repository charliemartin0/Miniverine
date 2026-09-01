namespace MiniVerine.Infrastructure.LocalQueues;

/// <summary>
/// Folder: async inside one process. TPL Dataflow or Channels, not Rabbit.
///
/// Put here: named queues, default vs Sequential/parallelism, back-pressure (queue length),
/// drain on shutdown, Enqueue from Publish/cascades. Circuit breaker pause of a local
/// listener belongs with the queue agent.
///
/// Do not put here: durable inbox rows (Persistence) or broker protocol (Transports).
/// A durable local queue is this folder + Persistence, same destination local://name/.
///
/// Prove with: PublishAsync returns before Handle runs; StopAsync does not drop in-flight
/// work that was not yet persisted.
/// </summary>
public sealed class LocalQueuesPlan;
