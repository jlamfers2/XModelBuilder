using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XModelBuilder.Demo.Shop.Contracts;
using XModelBuilder.Demo.Shop.Services;

namespace XModelBuilder.Demo.Shop.Controllers;

[ApiController]
[Route("api/customers")]
public sealed class CustomersController(OrderService orders) : ControllerBase
{
    [HttpGet("{email}/orders")]
    [Authorize]
    public async Task<ActionResult<IReadOnlyList<OrderResponse>>> GetOrders(string email) =>
        Ok(await orders.GetCustomerOrdersAsync(email));
}
