using Microsoft.Extensions.Options;
using XModelBuilder.Demo.Shop.Contracts;
using XModelBuilder.Demo.Shop.Domain;
using XModelBuilder.Fakers.XFaker;

namespace XModelBuilder.Demo.Shop.IntegrationTests.ModelBuilders;

/// <summary>
/// The main XModelBuilder showcase: a request that is itself a small graph. The builder fills in a
/// complete, valid shipping+billing address and a payment method by DEFAULT, so a scenario table only
/// has to specify the order lines it cares about (via deep paths like <c>Lines[0].Sku</c>).
/// </summary>
[ModelBuilder("order")]
public sealed class PlaceOrderRequestBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels)
    : ModelBuilder<PlaceOrderRequestBuilder, PlaceOrderRequest>(options, xmodels)
{
    protected override void SetDefaults()
    {
        With(r => r.PaymentMethod, PaymentMethodType.Ideal);
        With(r => r.ShippingAddress, x => x.For<AddressRequest>().Build());
        With(r => r.BillingAddress, x => x.For<AddressRequest>().Build());
    }
}

/// <summary>A default NL address, used for both the shipping and billing side of an order request.</summary>
[ModelBuilder("address")]
public sealed class AddressRequestBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels)
    : ModelBuilder<AddressRequestBuilder, AddressRequest>(options, xmodels)
{
    protected override void SetDefaults()
    {
        With(a => a.Street, "Teststraat");
        With(a => a.HouseNumber, x => x.XFaker().IntBetween(1, 200).ToString());
        With(a => a.PostalCode, "1000 AA");
        With(a => a.City, "Amsterdam");
        With(a => a.Country, "NL");
    }
}
