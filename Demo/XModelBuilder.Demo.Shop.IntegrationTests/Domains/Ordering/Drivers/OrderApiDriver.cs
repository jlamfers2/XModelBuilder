using XModelBuilder.Demo.Shop.Contracts;
using XModelBuilder.Demo.Shop.IntegrationTests.Common;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Domains.Ordering;

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
