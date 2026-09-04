using System.Reflection;
using MiniVerine.Application.Discovery;
using MiniVerine.Application.Discovery.Validators;
using MiniVerine.Domain.Messaging;
using MiniVerine.Domain.Messaging.ValueObjects;
using MiniVerine.Tests.Application.Discovery.InvalidSignatures;
using MiniVerine.Tests.Application.Discovery.Other;
using MiniVerine.Tests.Application.Discovery.Scanned;
using MiniVerine.Tests.Domain;

namespace MiniVerine.Tests.Application.Discovery;

public sealed class HandlerCatalogTests
{
    [Fact]
    public void scan_type_maps_message_to_handle()
    {
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(PlaceOrderHandler));

        var found = Assert.IsType<FoundHandlers>(catalog.Lookup(typeof(PlaceOrder)));
        Assert.Contains(found.Handlers, handler =>
            handler.HandlerType == typeof(PlaceOrderHandler)
            && handler.Method.Name == nameof(PlaceOrderHandler.Handle)
            && handler.MessageClrType == typeof(PlaceOrder));
    }

    [Fact]
    public void scan_type_maps_start_as_the_place_order_handler()
    {
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(DiscoveredOrderSaga));

        var found = Assert.IsType<FoundHandlers>(catalog.Lookup(typeof(PlaceOrder)));
        Assert.Contains(found.Handlers, handler =>
            handler.HandlerType == typeof(DiscoveredOrderSaga)
            && handler.Method.Name == nameof(DiscoveredOrderSaga.Start));
    }

    [Fact]
    public void scan_type_ignores_process_and_private_handle()
    {
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(NotAHandler));
        catalog.Scan(typeof(HiddenHandler));

        Assert.IsType<MissingHandler>(catalog.Lookup(typeof(PlaceOrder)));
        Assert.IsType<MissingHandler>(catalog.Lookup(typeof(HiddenMessage)));
        Assert.Empty(catalog.Handlers);
    }

    [Fact]
    public void scan_skips_abstract_handler_types()
    {
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(AbstractPlaceOrderHandler));

        Assert.IsType<MissingHandler>(catalog.Lookup(typeof(PlaceOrder)));
    }

    [Fact]
    public void fan_out_keeps_every_handler_for_the_same_message()
    {
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(FirstChargePaymentHandler));
        catalog.Scan(typeof(SecondChargePaymentHandler));

        var found = Assert.IsType<FoundHandlers>(catalog.Lookup(typeof(ChargePayment)));
        Assert.Equal(2, found.Handlers.Count);
        Assert.Contains(found.Handlers, handler => handler.HandlerType == typeof(FirstChargePaymentHandler));
        Assert.Contains(found.Handlers, handler => handler.HandlerType == typeof(SecondChargePaymentHandler) && handler.IsStatic);
    }

    [Fact]
    public void scanning_the_same_type_twice_is_a_noop()
    {
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(PlaceOrderHandler));
        catalog.Scan(typeof(PlaceOrderHandler));

        var found = Assert.IsType<FoundHandlers>(catalog.Lookup(typeof(PlaceOrder)));
        Assert.Single(found.Handlers);
        Assert.Single(catalog.Handlers);
    }

    [Fact]
    public void lookup_unknown_message_type_is_a_result_not_an_exception()
    {
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(PlaceOrderHandler));

        var lookup = catalog.Lookup(typeof(LogIncident));

        var missing = Assert.IsType<MissingHandler>(lookup);
        Assert.Equal(typeof(LogIncident), missing.MessageType);
    }

    [Fact]
    public void scan_registers_discovered_message_types_on_the_message_type_catalog()
    {
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(PlaceOrderHandler));

        Assert.Contains(catalog.MessageTypes.Registrations, registration => registration.Type == typeof(PlaceOrder));

        var lookup = catalog.MessageTypes.Lookup(MessageTypeNaming.For(typeof(PlaceOrder)));
        var known = Assert.IsType<KnownMessageType>(lookup);
        Assert.Equal(typeof(PlaceOrder), known.ClrType);
    }

    [Fact]
    public void extra_parameters_stay_on_the_discovered_handler()
    {
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(ChargePaymentHandler));

        var found = Assert.IsType<FoundHandlers>(catalog.Lookup(typeof(ChargePayment)));
        var handler = Assert.Single(found.Handlers);
        Assert.False(handler.IsStatic);
        Assert.Equal(2, handler.InjectionSlots.Count);
        Assert.Equal(typeof(IPaymentGateway), handler.InjectionSlots[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), handler.InjectionSlots[1].ParameterType);
    }

    [Fact]
    public void scan_assembly_lists_message_type_to_handler_methods()
    {
        var catalog = new HandlerCatalog();
        catalog.IncludeNamespace("MiniVerine.Tests.Application.Discovery.Scanned");
        catalog.Scan(typeof(PingHandler).Assembly);

        var found = Assert.IsType<FoundHandlers>(catalog.Lookup(typeof(Ping)));
        Assert.Contains(found.Handlers, handler => handler.HandlerType == typeof(PingHandler));
        Assert.IsType<MissingHandler>(catalog.Lookup(typeof(Pong)));
        Assert.IsType<MissingHandler>(catalog.Lookup(typeof(PlaceOrder)));
    }

    [Fact]
    public void exclude_type_skips_that_handler()
    {
        var catalog = new HandlerCatalog();
        catalog.ExcludeType(typeof(PlaceOrderHandler));
        catalog.Scan(typeof(PlaceOrderHandler));

        Assert.IsType<MissingHandler>(catalog.Lookup(typeof(PlaceOrder)));
    }

    [Fact]
    public void exclude_namespace_skips_types_in_that_namespace()
    {
        var catalog = new HandlerCatalog();
        catalog.ExcludeNamespace("MiniVerine.Tests.Application.Discovery.Other");
        catalog.ExcludeNamespace("MiniVerine.Tests.Application.Discovery.InvalidSignatures");
        catalog.Scan(typeof(PongHandler).Assembly);

        Assert.IsType<MissingHandler>(catalog.Lookup(typeof(Pong)));
        var found = Assert.IsType<FoundHandlers>(catalog.Lookup(typeof(Ping)));
        Assert.Contains(found.Handlers, handler => handler.HandlerType == typeof(PingHandler));
    }

    [Fact]
    public void include_namespace_skips_types_outside_it()
    {
        var catalog = new HandlerCatalog();
        catalog.IncludeNamespace("MiniVerine.Tests.Application.Discovery.Scanned");
        catalog.Scan(typeof(PlaceOrderHandler));

        Assert.IsType<MissingHandler>(catalog.Lookup(typeof(PlaceOrder)));
    }

    [Fact]
    public void missing_handler_is_a_port_not_an_invoker()
    {
        Assert.True(typeof(IMissingHandler).IsInterface);
        MethodInfo method = Assert.Single(typeof(IMissingHandler).GetMethods());
        Assert.Equal(nameof(IMissingHandler.HandleAsync), method.Name);
        Assert.Equal(typeof(Task), method.ReturnType);
        Assert.Equal(typeof(MiniVerine.Domain.Envelope.Envelope), method.GetParameters()[0].ParameterType);
        Assert.Equal(typeof(CancellationToken), method.GetParameters()[1].ParameterType);
    }

    [Fact]
    public void unique_discoveries_pass_catalog_validation()
    {
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(PlaceOrderHandler));
        catalog.Scan(typeof(LogIncidentConsumer));

        var result = new HandlerCatalogValidator().Validate(catalog);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void colliding_message_aliases_fail_catalog_validation()
    {
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(FirstSharedAliasHandler));
        catalog.Scan(typeof(SecondSharedAliasHandler));

        var result = new HandlerCatalogValidator().Validate(catalog);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage.Contains("unique", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void scan_start_async_saga_succeeds_when_the_message_is_correlatable()
    {
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(StartAsyncSaga));

        var found = Assert.IsType<FoundHandlers>(catalog.Lookup(typeof(StartAsyncMessage)));
        Assert.Contains(found.Handlers, handler =>
            handler.HandlerType == typeof(StartAsyncSaga)
            && handler.Method.Name == nameof(StartAsyncSaga.StartAsync));
    }

    [Fact]
    public void lookup_does_not_return_not_found_as_a_catalog_handler()
    {
        var catalog = new HandlerCatalog();
        catalog.Scan(typeof(TimeoutWithNotFoundSaga));

        var found = Assert.IsType<FoundHandlers>(catalog.Lookup(typeof(OrderTimeout)));
        Assert.All(found.Handlers, handler => Assert.NotEqual("NotFound", handler.Method.Name));
        Assert.Contains(found.Handlers, handler => handler.Method.Name == nameof(TimeoutWithNotFoundSaga.Handle));
    }

    [Fact]
    public void scan_uncorrelatable_saga_message_throws_invalid_handler_signature()
    {
        var catalog = new HandlerCatalog();

        InvalidHandlerSignature error = Assert.Throws<InvalidHandlerSignature>(
            () => catalog.Scan(typeof(UncorrelatableSaga)));

        Assert.Equal(typeof(UncorrelatableSaga), error.HandlerType);
        Assert.Empty(catalog.Handlers);
    }

    [Fact]
    public void scan_start_and_handle_for_the_same_saga_message_throws()
    {
        var catalog = new HandlerCatalog();

        InvalidHandlerSignature error = Assert.Throws<InvalidHandlerSignature>(
            () => catalog.Scan(typeof(StartAndHandleSaga)));

        Assert.Equal(typeof(StartAndHandleSaga), error.HandlerType);
    }

    [Fact]
    public void scan_static_saga_handler_throws()
    {
        var catalog = new HandlerCatalog();

        InvalidHandlerSignature error = Assert.Throws<InvalidHandlerSignature>(
            () => catalog.Scan(typeof(StaticStartSaga)));

        Assert.Equal(typeof(StaticStartSaga), error.HandlerType);
    }

    [Fact]
    public void scan_null_throws()
    {
        var catalog = new HandlerCatalog();

        Assert.Throws<ArgumentNullException>(() => catalog.Scan((Type)null!));
        Assert.Throws<ArgumentNullException>(() => catalog.Scan((Assembly)null!));
        Assert.Throws<ArgumentNullException>(() => catalog.Lookup(null!));
        Assert.Throws<ArgumentNullException>(() => catalog.IncludeNamespace(null!));
        Assert.Throws<ArgumentNullException>(() => catalog.ExcludeNamespace(null!));
        Assert.Throws<ArgumentNullException>(() => catalog.ExcludeType(null!));
    }
}
