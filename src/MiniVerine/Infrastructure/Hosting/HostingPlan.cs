namespace MiniVerine.Infrastructure.Hosting;

/// <summary>
/// Folder: Generic Host glue. UseMiniVerine() and MiniVerineOptions already live here.
///
/// Put here: IHostedService that starts listeners / durability agents and drains on
/// StopAsync. Opt-in handler assemblies on options. DI: singleton bus, scoped per message.
/// DurabilityMode.Solo belongs on options when Persistence exists.
///
/// Do not put here: handler methods, Envelope, or SQL. This folder only composes
/// Application services onto IHostApplicationBuilder.
///
/// Prove with: the console host starts and stops cleanly with no messages.
/// </summary>
public sealed class HostingPlan;
