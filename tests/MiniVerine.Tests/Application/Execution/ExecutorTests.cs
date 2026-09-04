using MiniVerine.Application.Discovery;
using MiniVerine.Application.Execution;
using MiniVerine.Domain.Envelope;
using MiniVerine.Domain.Errors.ValueObjects;
using MiniVerine.Domain.Messaging;
using MiniVerine.Domain.Messaging.ValueObjects;
using MiniVerine.Tests.Domain;
using MiniVerine.Tests.Domain.Envelope;

namespace MiniVerine.Tests.Application.Execution;

public sealed class ExecutorTests
{
    public ExecutorTests()
    {
        FlakyChargePaymentHandler.SeenAttempts.Clear();
        FlakyChargePaymentHandler.SeenIds.Clear();
        AlwaysTimeoutChargePaymentHandler.SeenAttempts.Clear();
        SucceedingChargePaymentHandler.SeenAttempts.Clear();
        SucceedingChargePaymentHandler.SeenIds.Clear();
    }

    [Fact]
    public async Task same_envelope_comes_back_with_attempts_1_2_3_then_succeeds()
    {
        Envelope envelope = ChargePaymentEnvelope();
        ErrorPolicyCatalog policies = TimeoutRetriesThenErrorQueue();
        var errorQueue = new RecordingErrorQueue();
        var executor = new Executor(policies, errorQueue: errorQueue);

        object? result = await executor.InvokeAsync(envelope, HandlerFor<FlakyChargePaymentHandler>());

        Assert.Equal([1, 2, 3], FlakyChargePaymentHandler.SeenAttempts);
        Assert.All(FlakyChargePaymentHandler.SeenIds, id => Assert.Equal(envelope.Id.Value, id));
        var payment = Assert.IsType<ChargePayment>(result);
        Assert.Equal(1, payment.OrderId);
        Assert.Empty(errorQueue.Moved);
    }

    [Fact]
    public async Task exhausted_retries_move_the_envelope_to_the_error_queue_and_throw_handler_fault()
    {
        Envelope envelope = ChargePaymentEnvelope();
        ErrorPolicyCatalog policies = TimeoutRetriesThenErrorQueue();
        var errorQueue = new RecordingErrorQueue();
        var executor = new Executor(policies, errorQueue: errorQueue);

        HandlerFault fault = await Assert.ThrowsAsync<HandlerFault>(
            () => executor.InvokeAsync(envelope, HandlerFor<AlwaysTimeoutChargePaymentHandler>()));

        Assert.Equal([1, 2, 3], AlwaysTimeoutChargePaymentHandler.SeenAttempts);
        Assert.IsType<TimeoutException>(fault.InnerException);
        Envelope moved = Assert.Single(errorQueue.Moved);
        Assert.Equal(envelope.Id, moved.Id);
        Assert.Equal(3, moved.Attempts.Value);
    }

    [Fact]
    public async Task missing_policy_throws_handler_fault_without_retry_or_error_queue()
    {
        Envelope envelope = ChargePaymentEnvelope();
        var errorQueue = new RecordingErrorQueue();
        var executor = new Executor(new ErrorPolicyCatalog(), errorQueue: errorQueue);

        HandlerFault fault = await Assert.ThrowsAsync<HandlerFault>(
            () => executor.InvokeAsync(envelope, HandlerFor<AlwaysTimeoutChargePaymentHandler>()));

        Assert.Equal([1], AlwaysTimeoutChargePaymentHandler.SeenAttempts);
        Assert.IsType<TimeoutException>(fault.InnerException);
        Assert.Empty(errorQueue.Moved);
    }

    [Fact]
    public async Task success_on_first_try_keeps_attempts_at_one()
    {
        Envelope envelope = ChargePaymentEnvelope();
        var executor = new Executor(TimeoutRetriesThenErrorQueue());

        object? result = await executor.InvokeAsync(envelope, HandlerFor<SucceedingChargePaymentHandler>());

        Assert.Equal([1], SucceedingChargePaymentHandler.SeenAttempts);
        Assert.Same(envelope.Message.Value, result);
    }

    [Fact]
    public async Task zero_cooldown_retries_without_throwing()
    {
        Envelope envelope = ChargePaymentEnvelope();
        var policies = new ErrorPolicyCatalog();
        policies.OnException<TimeoutException>()
            .RetryWithCooldown(TimeSpan.Zero)
            .RetryWithCooldown(TimeSpan.Zero);
        var executor = new Executor(policies);

        object? result = await executor.InvokeAsync(envelope, HandlerFor<FlakyChargePaymentHandler>());

        Assert.Equal([1, 2, 3], FlakyChargePaymentHandler.SeenAttempts);
        Assert.IsType<ChargePayment>(result);
    }

    [Fact]
    public async Task handler_receives_the_envelope_including_attempts()
    {
        Envelope envelope = ChargePaymentEnvelope();
        var executor = new Executor(new ErrorPolicyCatalog());

        await executor.InvokeAsync(envelope, HandlerFor<SucceedingChargePaymentHandler>());

        Assert.Equal(envelope.Id.Value, Assert.Single(SucceedingChargePaymentHandler.SeenIds));
        Assert.Equal(1, Assert.Single(SucceedingChargePaymentHandler.SeenAttempts));
    }

    [Fact]
    public async Task invoke_does_not_apply_requeue()
    {
        Envelope envelope = ChargePaymentEnvelope();
        var policies = new ErrorPolicyCatalog();
        policies.OnException<TimeoutException>().Requeue();
        var errorQueue = new RecordingErrorQueue();
        var executor = new Executor(policies, errorQueue: errorQueue);

        HandlerFault fault = await Assert.ThrowsAsync<HandlerFault>(
            () => executor.InvokeAsync(envelope, HandlerFor<AlwaysTimeoutChargePaymentHandler>()));

        Assert.Equal([1], AlwaysTimeoutChargePaymentHandler.SeenAttempts);
        Assert.IsType<TimeoutException>(fault.InnerException);
        Assert.Empty(errorQueue.Moved);
    }

    [Fact]
    public async Task missing_handler_port_receives_the_envelope()
    {
        Envelope envelope = ChargePaymentEnvelope();
        var missing = new RecordingMissingHandler();
        var executor = new Executor(new ErrorPolicyCatalog(), missingHandler: missing);

        await executor.HandleMissingAsync(envelope);

        Assert.Same(envelope, missing.Received);
    }

    [Fact]
    public async Task missing_handler_without_a_port_throws_handler_not_found()
    {
        Envelope envelope = ChargePaymentEnvelope();
        var executor = new Executor(new ErrorPolicyCatalog());

        HandlerNotFound missing = await Assert.ThrowsAsync<HandlerNotFound>(
            () => executor.HandleMissingAsync(envelope));

        Assert.Equal(typeof(ChargePayment), missing.MessageType);
    }

    private static ErrorPolicyCatalog TimeoutRetriesThenErrorQueue()
    {
        var policies = new ErrorPolicyCatalog();
        policies.OnException<TimeoutException>().Retry().Retry().Then.MoveToErrorQueue();
        return policies;
    }

    private static Envelope ChargePaymentEnvelope() =>
        EnvelopeFactory.Create(
            message: new Message(new ChargePayment(1)),
            messageType: MessageTypeNaming.For(typeof(ChargePayment)));

    private static DiscoveredHandler HandlerFor<THandler>()
    {
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(THandler));
        return Assert.Single(catalog.Handlers);
    }
}

public sealed class FlakyChargePaymentHandler
{
    public static List<int> SeenAttempts { get; } = [];
    public static List<Guid> SeenIds { get; } = [];

    public ChargePayment Handle(ChargePayment payment, Envelope envelope)
    {
        SeenAttempts.Add(envelope.Attempts.Value);
        SeenIds.Add(envelope.Id.Value);
        if (envelope.Attempts.Value < 3)
        {
            throw new TimeoutException("Payment gateway timeout");
        }

        return payment;
    }
}

public sealed class AlwaysTimeoutChargePaymentHandler
{
    public static List<int> SeenAttempts { get; } = [];

    public void Handle(ChargePayment payment, Envelope envelope)
    {
        SeenAttempts.Add(envelope.Attempts.Value);
        throw new TimeoutException("Payment gateway timeout");
    }
}

public sealed class SucceedingChargePaymentHandler
{
    public static List<int> SeenAttempts { get; } = [];
    public static List<Guid> SeenIds { get; } = [];

    public ChargePayment Handle(ChargePayment payment, Envelope envelope)
    {
        SeenAttempts.Add(envelope.Attempts.Value);
        SeenIds.Add(envelope.Id.Value);
        return payment;
    }
}

public sealed class RecordingErrorQueue : IErrorQueue
{
    public List<Envelope> Moved { get; } = [];

    public void Move(Envelope envelope)
    {
        Moved.Add(envelope);
    }
}

public sealed class RecordingMissingHandler : IMissingHandler
{
    public Envelope? Received { get; private set; }

    public Task HandleAsync(Envelope envelope, CancellationToken cancellationToken = default)
    {
        Received = envelope;
        return Task.CompletedTask;
    }
}
