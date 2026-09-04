using MiniVerine.Domain.Sagas;
using MiniVerine.Tests.Domain;

namespace MiniVerine.Tests.Application.Discovery.InvalidSignatures;

public sealed class UncorrelatableSaga : Saga
{
    public void Handle(ChargePayment message)
    {
    }
}

public sealed class StartAndHandleSaga : Saga
{
    public void Start(PlaceOrder message)
    {
    }

    public void Handle(PlaceOrder message)
    {
    }
}

public sealed class StaticStartSaga : Saga
{
    public static void Start(PlaceOrder message)
    {
    }
}
