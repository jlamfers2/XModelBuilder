using System.Globalization;
using System.Net;
using Reqnroll;
using XModelBuilder.Demo.Shop.Contracts;
using XModelBuilder.Demo.Shop.Domain;
using XModelBuilder.Demo.Shop.IntegrationTests.Common;
using XModelBuilder.Reqnroll;
using Xunit;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Domains.Ordering;

/// <summary>
/// Placing orders. The request - a small graph of lines + shipping/billing address + payment - is
/// built by XModelBuilder from a compact Gherkin table: the "order" builder fills the addresses and
/// payment by default, so the table only specifies the lines via deep paths (<c>Lines[0].Sku</c>).
/// </summary>
[Binding]
public sealed class PlaceOrderSteps(
    IModelBuilderProvider xmodels,
    OrderApiDriver orders,
    OrderContext orderContext,
    HttpResponseContext response)
{
    [When(@"ik de volgende bestelling plaats:")]
    public async Task WhenIPlaceTheOrder(Table table)
    {
        var request = xmodels.For<PlaceOrderRequest>("order").CreateModel(table);
        await orders.PlaceOrder(request);
    }

    [Then(@"wordt de bestelling aangemaakt")]
    public void ThenOrderIsCreated()
    {
        var result = response.Require();
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        orderContext.Current = result.Read<OrderResponse>();
    }

    [Then(@"heeft de bestelling status ""(.*)""")]
    public void ThenOrderHasStatus(string status) =>
        Assert.Equal(Enum.Parse<OrderStatus>(status), orderContext.Current!.Status);

    [Then(@"is het totaalbedrag (.*)")]
    public void ThenTotalIs(string expected) =>
        Assert.Equal(Parse(expected), orderContext.Current!.TotalAmount);

    [Then(@"is het kortingsbedrag (.*)")]
    public void ThenDiscountIs(string expected) =>
        Assert.Equal(Parse(expected), orderContext.Current!.DiscountAmount);

    [Then(@"wordt de bestelling geweigerd wegens onvoldoende voorraad")]
    public void ThenRejectedForStock()
    {
        var result = response.Require();
        Assert.Equal(HttpStatusCode.Conflict, result.StatusCode);
        Assert.Contains("stock", result.Body, StringComparison.OrdinalIgnoreCase);
    }

    private static decimal Parse(string value) => decimal.Parse(value, CultureInfo.InvariantCulture);
}
