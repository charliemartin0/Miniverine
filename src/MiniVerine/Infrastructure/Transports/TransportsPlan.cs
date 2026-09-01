namespace MiniVerine.Infrastructure.Transports;

/// <summary>
/// Folder: ITransport / Endpoint. Envelope is independent of the broker.
///
/// Put here: URI schemes (local://, later tcp://), Inline / Buffered / Durable modes,
/// send path (serialize → route → map Envelope → outbound), listen path (inbound →
/// Envelope → deserialize → Execution). TCP is the teaching transport (no Docker).
/// RabbitMQ implementation lives in MiniVerine.RabbitMQ and plugs into these ports.
/// HTTP is MiniVerine.Http — another front door, same Execution, not a broker.
///
/// Do not put here: handler conventions or saga state. Do not take a Rabbit dependency
/// in this project.
///
/// Prove with: a test transport can deliver an Envelope to Execution without Application
/// knowing which transport it was.
/// </summary>
public sealed class TransportsPlan;
