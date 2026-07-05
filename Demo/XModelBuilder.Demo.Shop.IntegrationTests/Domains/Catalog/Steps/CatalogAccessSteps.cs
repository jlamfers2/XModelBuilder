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
    IModelBuilderProvider xmodels,
    CatalogApiDriver catalog,
    OrderApiDriver orders,
    CatalogContext catalogContext,
    HttpResponseContext response)
{
    [When(@"ik het volgende product toevoeg:")]
    public async Task WhenIAddTheProduct(Table table)
    {
        var request = xmodels.For<CreateProductRequest>().CreateModel(table);
        await catalog.AddProduct(request);
    }

    [Then(@"wordt het product toegevoegd")]
    public void ThenProductIsAdded()
    {
        var result = response.Require();
        Assert.Equal(HttpStatusCode.Created, result.StatusCode);
        catalogContext.LastCreated = result.Read<ProductResponse>();
    }

    [Then(@"bevat de catalogus een product met sku ""(.*)""")]
    public async Task ThenCatalogContains(string sku)
    {
        var result = await catalog.GetProducts();
        var products = result.Read<List<ProductResponse>>();
        Assert.Contains(products, p => p.Sku == sku);
    }

    [When(@"ik de bestellingen opvraag van ""(.*)""")]
    public async Task WhenIRequestOrdersOf(string email) => await orders.GetCustomerOrders(email);
}
