using MiniVerine.Application.Bus;
using MiniVerine.Application.Discovery;

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
