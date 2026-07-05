using XModelBuilder.Demo.Shop.Contracts;
using XModelBuilder.Demo.Shop.IntegrationTests.Contexts;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Drivers;

/// <summary>Switches the acting user/role for the scenario (header-based test auth, so no real login).</summary>
public sealed class AuthenticationDriver(CurrentUserContext user)
{
    public void SignInAs(string email, string role)
    {
        user.Email = email;
        user.Role = role;
    }

    public void SignOut()
    {
        user.Email = null;
        user.Role = null;
    }
}

/// <summary>SPECIFIC driver for the order endpoints.</summary>
public sealed class OrderApiDriver(HttpClient client, CurrentUserContext user, HttpResponseContext response)
    : ApiDriver(client, user, response)
{
    public Task<ApiResponse> PlaceOrder(PlaceOrderRequest request) => PostAsync("/api/orders", request);

    public Task<ApiResponse> Pay(int orderId) => PostAsync($"/api/orders/{orderId}/pay");

    public Task<ApiResponse> Ship(int orderId) => PostAsync($"/api/orders/{orderId}/ship");

    public Task<ApiResponse> GetOrder(int orderId) => GetAsync($"/api/orders/{orderId}");

    public Task<ApiResponse> GetCustomerOrders(string email) => GetAsync($"/api/customers/{email}/orders");
}

/// <summary>SPECIFIC driver for the catalog endpoints.</summary>
public sealed class CatalogApiDriver(HttpClient client, CurrentUserContext user, HttpResponseContext response)
    : ApiDriver(client, user, response)
{
    public Task<ApiResponse> AddProduct(CreateProductRequest request) => PostAsync("/api/products", request);

    public Task<ApiResponse> GetProducts() => GetAsync("/api/products");
}

/// <summary>
/// AGGREGATE driver: composes the specific drivers, injected into steps that orchestrate several
/// endpoints in one flow (e.g. place → pay → ship). Steps that touch one area inject that driver directly.
/// </summary>
public sealed class ShopDriver(AuthenticationDriver authentication, OrderApiDriver orders, CatalogApiDriver catalog)
{
    public AuthenticationDriver Authentication => authentication;
    public OrderApiDriver Orders => orders;
    public CatalogApiDriver Catalog => catalog;
}
