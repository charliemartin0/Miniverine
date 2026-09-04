using MiniVerine.Application.Bus;
using MiniVerine.Application.Cascades;
using MiniVerine.Application.Discovery;
using MiniVerine.Application.Execution;
using MiniVerine.Application.Scheduling;
using MiniVerine.Domain.Envelope;
using MiniVerine.Tests.Domain;

namespace MiniVerine.Tests.Application.Scheduling;

public sealed class MessageSchedulerTests
{
    public MessageSchedulerTests()
    {
        PlayedTimeoutHandler.Handled.Clear();
        ScheduledStartHandler.Started = false;
        FlakyScheduledTimeoutHandler.SeenAttempts.Clear();
        FailingThenPendingTimeoutHandler.Seen.Clear();
    }

    [Fact]
    public async Task a_one_minute_timeout_can_be_played_now()
    {
        var hold = new InMemoryScheduledEnvelopeHold();
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(ScheduledStartHandler));
        catalog.Scan(typeof(PlayedTimeoutHandler));
        var executor = new Executor(new ErrorPolicyCatalog(), scheduled: hold);
        var dispatcher = new OutgoingDispatcher(hold);
        var bus = new MiniVerine.Application.Mediator.Mediator(catalog, executor: executor, hold: hold);
        var scheduler = new MessageScheduler(catalog, executor, hold, dispatcher);

        await bus.InvokeAsync(new PlaceOrder(1));

        Assert.True(ScheduledStartHandler.Started);
        Assert.Empty(PlayedTimeoutHandler.Handled);
        var parked = Assert.Single(hold.Peek());
        Assert.IsType<OrderTimeout>(parked.Message.Value);

        await scheduler.PlayDue(parked.DeliverBy.Value!.Value);

        Assert.Equal([1], PlayedTimeoutHandler.Handled);
        Assert.Empty(hold.Peek());
    }

    [Fact]
    public async Task play_due_does_not_run_work_parked_during_the_drain()
    {
        var hold = new InMemoryScheduledEnvelopeHold();
        var policies = new ErrorPolicyCatalog();
        policies.OnException<TimeoutException>().ScheduleRetry(TimeSpan.FromTicks(1));
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(FlakyScheduledTimeoutHandler));
        var executor = new Executor(policies, scheduled: hold);
        var dispatcher = new OutgoingDispatcher(hold);
        hold.Park(TimeoutEnvelope());
        var scheduler = new MessageScheduler(catalog, executor, hold, dispatcher);

        await scheduler.PlayDue(DateTimeOffset.UtcNow.AddYears(10));

        Assert.Equal([1], FlakyScheduledTimeoutHandler.SeenAttempts);
        var parked = Assert.Single(hold.Peek());
        Assert.Equal(2, parked.Attempts.Value);
    }

    [Fact]
    public async Task play_due_with_nothing_due_is_a_noop()
    {
        var hold = new InMemoryScheduledEnvelopeHold();
        var catalog = new HandlerCatalog();
        var executor = new Executor(new ErrorPolicyCatalog(), scheduled: hold);
        var scheduler = new MessageScheduler(catalog, executor, hold, new OutgoingDispatcher(hold));

        hold.Park(TimeoutEnvelope(DateTimeOffset.UtcNow.AddHours(1)));

        await scheduler.PlayDue(DateTimeOffset.UtcNow);

        Assert.Single(hold.Peek());
    }

    [Fact]
    public async Task play_due_fail_fast_leaves_unstarted_snapshot_members_held()
    {
        var hold = new InMemoryScheduledEnvelopeHold();
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(FailingThenPendingTimeoutHandler));
        var executor = new Executor(new ErrorPolicyCatalog(), scheduled: hold);
        var scheduler = new MessageScheduler(catalog, executor, hold, new OutgoingDispatcher(hold));
        DateTimeOffset due = DateTimeOffset.UtcNow.AddMinutes(-1);
        hold.Park(TimeoutEnvelope(due, orderId: 1));
        hold.Park(TimeoutEnvelope(due, orderId: 2));

        await Assert.ThrowsAsync<HandlerFault>(() => scheduler.PlayDue(DateTimeOffset.UtcNow));

        Assert.Equal([1], FailingThenPendingTimeoutHandler.Seen);
        var remaining = Assert.Single(hold.Peek());
        Assert.Equal(2, ((OrderTimeout)remaining.Message.Value).OrderId);
    }

    [Fact]
    public async Task invoke_of_a_timeout_type_runs_now_and_does_not_park()
    {
        var hold = new InMemoryScheduledEnvelopeHold();
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(PlayedTimeoutHandler));
        IMessageBus bus = new MiniVerine.Application.Mediator.Mediator(catalog, hold: hold);

        await bus.InvokeAsync(new OrderTimeout(1));

        Assert.Equal([1], PlayedTimeoutHandler.Handled);
        Assert.Empty(hold.Peek());
    }

    [Fact]
    public async Task cancelled_play_due_before_work_starts_leaves_held_envelopes()
    {
        var hold = new InMemoryScheduledEnvelopeHold();
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(PlayedTimeoutHandler));
        var executor = new Executor(new ErrorPolicyCatalog(), scheduled: hold);
        var scheduler = new MessageScheduler(catalog, executor, hold, new OutgoingDispatcher(hold));
        hold.Park(TimeoutEnvelope());
        using var cancelled = new CancellationTokenSource();
        cancelled.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => scheduler.PlayDue(DateTimeOffset.UtcNow, cancelled.Token));

        Assert.Single(hold.Peek());
        Assert.Empty(PlayedTimeoutHandler.Handled);
    }

    [Fact]
    public async Task invoke_with_delay_is_delayed_invoke_not_supported()
    {
        IMessageBus bus = new MiniVerine.Application.Mediator.Mediator(new HandlerCatalog());

        await Assert.ThrowsAsync<DelayedInvokeNotSupported>(
            () => bus.InvokeAsync(new PlaceOrder(1), new DeliveryOptions { Delay = TimeSpan.FromMinutes(1) }));
    }

    [Fact]
    public async Task invoke_with_delay_and_until_is_ambiguous()
    {
        IMessageBus bus = new MiniVerine.Application.Mediator.Mediator(new HandlerCatalog());

        await Assert.ThrowsAsync<AmbiguousDeliveryOptions>(
            () => bus.InvokeAsync(
                new PlaceOrder(1),
                new DeliveryOptions { Delay = TimeSpan.Zero, Until = DateTimeOffset.UtcNow }));
    }

    [Fact]
    public async Task publish_timeout_parks_without_a_worker()
    {
        var hold = new InMemoryScheduledEnvelopeHold();
        IMessageBus bus = new MiniVerine.Application.Mediator.Mediator(new HandlerCatalog(), hold: hold);

        await bus.PublishAsync(new OrderTimeout(1));

        var parked = Assert.Single(hold.Peek());
        Assert.IsType<OrderTimeout>(parked.Message.Value);
    }

    [Fact]
    public async Task publish_zero_delay_timeout_is_still_immediate_and_unsupported()
    {
        var hold = new InMemoryScheduledEnvelopeHold();
        IMessageBus bus = new MiniVerine.Application.Mediator.Mediator(new HandlerCatalog(), hold: hold);

        await Assert.ThrowsAsync<NotSupportedException>(
            () => bus.PublishAsync(new OrderTimeout(1), new DeliveryOptions { Delay = TimeSpan.Zero }));
        Assert.Empty(hold.Peek());
    }

    [Fact]
    public async Task throwing_handler_parks_no_timeout()
    {
        var hold = new InMemoryScheduledEnvelopeHold();
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(ThrowingScheduledStartHandler));
        var cascades = new RecordingImmediate();
        IMessageBus bus = new MiniVerine.Application.Mediator.Mediator(catalog, cascades, hold: hold);

        await Assert.ThrowsAsync<HandlerFault>(() => bus.InvokeAsync(new PlaceOrder(1)));

        Assert.Empty(hold.Peek());
        Assert.Empty(cascades.Published);
    }

    private static Envelope TimeoutEnvelope(DateTimeOffset? deliverBy = null, int orderId = 1) =>
        MiniVerine.Tests.Domain.Envelope.EnvelopeFactory.Create(
            message: new MiniVerine.Domain.Messaging.ValueObjects.Message(new OrderTimeout(orderId)),
            messageType: MiniVerine.Domain.Messaging.MessageTypeNaming.For(typeof(OrderTimeout)),
            deliverBy: deliverBy ?? DateTimeOffset.UtcNow.AddMinutes(-1));

    private sealed class RecordingImmediate : ICascadePublisher
    {
        public List<object> Published { get; } = [];

        public void Publish(IReadOnlyList<object> outgoing) => Published.AddRange(outgoing);
    }
}

public sealed class ScheduledStartHandler
{
    public static bool Started;

    public OrderTimeout Handle(PlaceOrder message)
    {
        Started = true;
        return new OrderTimeout(message.OrderId);
    }
}

public sealed class PlayedTimeoutHandler
{
    public static List<int> Handled { get; } = [];

    public void Handle(OrderTimeout message) => Handled.Add(message.OrderId);
}

public sealed class FlakyScheduledTimeoutHandler
{
    public static List<int> SeenAttempts { get; } = [];

    public void Handle(OrderTimeout message, Envelope envelope)
    {
        SeenAttempts.Add(envelope.Attempts.Value);
        throw new TimeoutException("not yet");
    }
}

public sealed class FailingThenPendingTimeoutHandler
{
    public static List<int> Seen { get; } = [];

    public void Handle(OrderTimeout message)
    {
        Seen.Add(message.OrderId);
        if (message.OrderId == 1)
        {
            throw new InvalidOperationException("first failed");
        }
    }
}

public sealed class ThrowingScheduledStartHandler
{
    public OrderTimeout Handle(PlaceOrder message)
    {
        _ = new OrderTimeout(message.OrderId);
        throw new TimeoutException("start failed");
    }
}
