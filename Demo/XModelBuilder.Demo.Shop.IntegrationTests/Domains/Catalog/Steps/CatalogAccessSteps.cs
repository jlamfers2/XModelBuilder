using System.Net;
using Reqnroll;
using XModelBuilder.Demo.Shop.Contracts;
using XModelBuilder.Demo.Shop.IntegrationTests.Common;
using XModelBuilder.Demo.Shop.IntegrationTests.Domains.Ordering;
using XModelBuilder.Reqnroll;
using Xunit;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Domains.Catalog;

/// <summary>Catalog management (admin-only) and ownership-based access to orders.</summary>
[Binding]
public sealed class CatalogAccessSteps(
    IModelBuilderProvider xprovider,
    CatalogApiDriver catalog,
    OrderApiDriver orders,
    CatalogContext catalogContext,
    HttpResponseContext response)
{
    [When(@"I add the following product:")]
    public async Task WhenIAddTheProduct(Table table)
    {
        var request = xprovider.For<CreateProductRequest>().CreateModel(table);
        await catalog.AddProduct(request);
    }

    [Then(@"the product is added")]
    public void ThenProductIsAdded()
    {
        var result = response.Require();
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        catalogContext.LastCreated = result.Read<ProductResponse>();
    }

    [Then(@"the catalog contains a product with sku ""(.*)""")]
    public async Task ThenCatalogContains(string sku)
    {
        var result = await catalog.GetProducts();
        var products = result.Read<List<ProductResponse>>();
        Assert.Contains(products, p => p.Sku == sku);
    }

    [When(@"I request the orders of ""(.*)""")]
    public async Task WhenIRequestOrdersOf(string email) => await orders.GetCustomerOrders(email);
}
