using XModelBuilder.Demo.Shop.Contracts;
using XModelBuilder.Demo.Shop.IntegrationTests.Common;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Catalog;

/// <summary>SPECIFIC driver for the catalog endpoints.</summary>
public sealed class CatalogApiDriver(HttpClient client, CurrentUserContext user, HttpResponseContext response)
    : ApiDriver(client, user, response)
{
    public Task<ApiResponse> AddProduct(CreateProductRequest request) => PostAsync("/api/products", request);

    public Task<ApiResponse> GetProducts() => GetAsync("/api/products");
}
