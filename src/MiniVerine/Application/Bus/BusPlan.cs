namespace MiniVerine.Application.Bus;

/// <summary>
/// Folder: the public facade handlers and the sample host talk to.
///
/// Put here: IMessageBus (InvokeAsync, InvokeAsync&lt;T&gt;, PublishAsync, maybe Schedule).
/// This is the application service of the bus. It does not own threads or sockets.
///
/// Do not put here: TPL Dataflow, Npgsql, or ASP.NET. Invoke vs Publish is a method
/// choice on this type; the difference is implemented in Mediator vs Routing/LocalQueues.
///
/// Prove with: the sample host can call the bus without referencing Infrastructure types.
/// </summary>
public sealed class BusPlan;
