namespace MiniVerine.Application.Scheduling;

/// <summary>
/// Folder: time is a message. Delay / ScheduleTime / [Timeout].
///
/// Put here: “deliver this Envelope at T+n”. Fast-forward hook used by Tracking
/// (PlayScheduledMessagesAsync). In-memory scheduler is enough until Persistence
/// stores execution_time.
///
/// Do not put here: Task.Delay inside a saga, or a System.Threading.Timer on the saga
/// instance. Do not wait a minute in tests.
///
/// Prove with: a 1-minute timeout can be played now; unpaid vs already-completed saga
/// are two different handler methods, not an if in Start.
/// </summary>
public sealed class SchedulingPlan;
