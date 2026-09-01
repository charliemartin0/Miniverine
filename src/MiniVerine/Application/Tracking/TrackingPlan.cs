namespace MiniVerine.Application.Tracking;

/// <summary>
/// Folder: watch a conversation in tests without Thread.Sleep.
///
/// Put here: TrackActivity / tracked session, assert published / executed / scheduled,
/// DoNotAssertOnExceptionsDetected, PlayScheduledMessagesAsync returning a new session.
/// This is a first-class bus API that tests use, not a test-project helper.
///
/// Do not put here: xUnit fixtures, FluentAssertions, or Testcontainers. Those stay in
/// tests/. Do not make TrackActivity the production host’s way to wait.
///
/// Prove with: PlaceOrder → three ChargePayment executions → one PaymentCharged, asserted
/// on the session, including when attempts 1–2 throw on purpose.
/// </summary>
public sealed class TrackingPlan;
