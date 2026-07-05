using System.Net;
using Reqnroll;
using XModelBuilder.Demo.Shop.Contracts;
using XModelBuilder.Demo.Shop.IntegrationTests.Common;
using Xunit;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Ordering;

/// <summary>
/// Order fulfilment: the place → pay → ship state machine and its rules. The setup steps build a
/// minimal order with XModelBuilder (overriding just the line), so the scenarios stay focused on
/// behaviour rather than data.
/// </summary>
[Binding]
public sealed class FulfillOrderSteps(
    IModelBuilderProvider xmodels,
    OrderApiDriver orders,
    OrderContext orderContext,
    HttpResponseContext response)
{
    [Given(@"ik een betaalde bestelling heb geplaatst voor (\d+) x ""(.*)""")]
    public async Task GivenIHavePaidOrder(int quantity, string sku)
    {
        await PlaceOrder(quantity, sku);
        await Pay();
    }

    [Given(@"ik een bestelling heb geplaatst voor (\d+) x ""(.*)""")]
    public async Task GivenIHaveOrder(int quantity, string sku) => await PlaceOrder(quantity, sku);

    [When(@"ik de bestelling verzend")]
    public async Task WhenIShipTheOrder() => await orders.Ship(orderContext.CurrentId);

    [Then(@"wordt de verzending geaccepteerd")]
    public void ThenShipmentAccepted()
    {
        var result = response.Require();
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        orderContext.Current = result.Read<OrderResponse>();
    }

    [Then(@"wordt de verzending geweigerd")]
    public void ThenShipmentRejected() =>
        Assert.Equal(HttpStatusCode.Conflict, response.Require().StatusCode);

    private async Task PlaceOrder(int quantity, string sku)
    {
        var request = xmodels.For<PlaceOrderRequest>("order")
            .With(r => r.Lines, [new OrderLineRequest { Sku = sku, Quantity = quantity }])
            .Build();

        var result = await orders.PlaceOrder(request);
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        orderContext.Current = result.Read<OrderResponse>();
    }

    private async Task Pay()
    {
        var result = await orders.Pay(orderContext.CurrentId);
        Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        orderContext.Current = result.Read<OrderResponse>();
    }
}
