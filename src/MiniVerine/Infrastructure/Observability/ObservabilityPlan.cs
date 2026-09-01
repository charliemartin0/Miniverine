namespace MiniVerine.Infrastructure.Observability;

/// <summary>
/// Folder: see the bus without reading Console.WriteLine in handlers.
///
/// Put here: OpenTelemetry Activity per message (no-op exporter at first), metrics
/// (in-flight, failures, latency), correlation id in ILogger scopes, health checks for
/// listeners and storage, describe-routing, envelope logging with redaction.
///
/// Do not put here: business logging (“payment charged”). Handlers may log domain facts;
/// this folder logs the machine (Attempts, destination, duration).
///
/// Prove with: one failed ChargePayment attempt is visible as a span/metric without
/// asserting on stdout.
/// </summary>
public sealed class ObservabilityPlan;
