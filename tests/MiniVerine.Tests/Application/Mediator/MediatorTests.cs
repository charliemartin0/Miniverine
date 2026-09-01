using MiniVerine.Application.Bus;
using MiniVerine.Application.Cascades;
using MiniVerine.Application.Discovery;
using MiniVerine.Tests.Domain;

namespace MiniVerine.Tests.Application.Mediator;

public sealed class MediatorTests
{
    [Fact]
    public async Task invoke_async_does_not_return_until_handle_returns()
    {
        RecordedIncidentHandler.Handled = false;

        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(RecordedIncidentHandler));

        IMessageBus bus = new MiniVerine.Application.Mediator.Mediator(catalog);

        await bus.InvokeAsync(new RecordedIncident("Everything broken"));

        Assert.True(RecordedIncidentHandler.Handled);
    }

    [Fact]
    public async Task invoke_publishes_return_values_after_the_handler_succeeds()
    {
        ReturnsChargePaymentHandler.Returned = false;
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(ReturnsChargePaymentHandler));
        var cascades = new RecordingCascadePublisher();
        IMessageBus bus = new MiniVerine.Application.Mediator.Mediator(catalog, cascades);

        await bus.InvokeAsync(new PlaceOrder(1));

        Assert.True(ReturnsChargePaymentHandler.Returned);
        var payment = Assert.IsType<ChargePayment>(Assert.Single(cascades.Published));
        Assert.Equal(1, payment.OrderId);
    }

    [Fact]
    public async Task invoke_of_throwing_handler_publishes_nothing()
    {
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(ThrowingPlaceOrderHandler));
        var cascades = new RecordingCascadePublisher();
        IMessageBus bus = new MiniVerine.Application.Mediator.Mediator(catalog, cascades);

        await Assert.ThrowsAsync<TimeoutException>(() => bus.InvokeAsync(new PlaceOrder(1)));

        Assert.Empty(cascades.Published);
    }

    [Fact]
    public async Task invoke_does_not_run_handlers_for_cascaded_messages()
    {
        ChargePaymentWasHandled.Handled = false;
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(ReturnsChargePaymentHandler));
        catalog.Scan(typeof(ChargePaymentWasHandled));
        var cascades = new RecordingCascadePublisher();
        IMessageBus bus = new MiniVerine.Application.Mediator.Mediator(catalog, cascades);

        await bus.InvokeAsync(new PlaceOrder(1));

        Assert.False(ChargePaymentWasHandled.Handled);
        Assert.Single(cascades.Published);
    }
}

public sealed record RecordedIncident(string Description);

public sealed class RecordedIncidentHandler
{
    public static bool Handled;

    public void Handle(RecordedIncident message)
    {
        Handled = true;
    }
}

public sealed class ReturnsChargePaymentHandler
{
    public static bool Returned;

    public ChargePayment Handle(PlaceOrder message)
    {
        Returned = true;
        return new ChargePayment(message.OrderId);
    }
}

public sealed class ThrowingPlaceOrderHandler
{
    public ChargePayment Handle(PlaceOrder message)
    {
        _ = new ChargePayment(message.OrderId);
        throw new TimeoutException("Payment gateway timeout");
    }
}

public sealed class ChargePaymentWasHandled
{
    public static bool Handled;

    public void Handle(ChargePayment message)
    {
        Handled = true;
    }
}

public sealed class RecordingCascadePublisher : ICascadePublisher
{
    public List<object> Published { get; } = [];

    public void Publish(IReadOnlyList<object> outgoing)
    {
        Published.AddRange(outgoing);
    }
}
