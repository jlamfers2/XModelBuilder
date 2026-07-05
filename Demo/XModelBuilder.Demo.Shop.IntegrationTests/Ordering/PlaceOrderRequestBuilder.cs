using Microsoft.Extensions.Options;
using XModelBuilder.Demo.Shop.Contracts;
using XModelBuilder.Demo.Shop.Domain;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Ordering;

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
