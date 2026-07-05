using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XModelBuilder.Demo.Shop.Contracts;
using XModelBuilder.Demo.Shop.Domain;
using XModelBuilder.Demo.Shop.Services;

namespace XModelBuilder.Demo.Shop.Controllers;

[ApiController]
[Route("api/orders")]
public sealed class OrdersController(OrderService orders) : ControllerBase
{
    [HttpPost]
    [Authorize(Roles = Roles.Customer)]
    public async Task<ActionResult<OrderResponse>> Place([FromBody] PlaceOrderRequest request)
    {
        var order = await orders.PlaceOrderAsync(request);
        return CreatedAtAction(nameof(Get), new { id = order.Id }, order);
    }

    [HttpGet("{id:int}")]
    [Authorize]
    public async Task<ActionResult<OrderResponse>> Get(int id) => await orders.GetOrderAsync(id);

    [HttpPost("{id:int}/pay")]
    [Authorize(Roles = Roles.Customer)]
    public async Task<ActionResult<OrderResponse>> Pay(int id) => await orders.PayOrderAsync(id);

    [HttpPost("{id:int}/ship")]
    [Authorize(Roles = $"{Roles.WarehouseOperator},{Roles.Admin}")]
    public async Task<ActionResult<OrderResponse>> Ship(int id) => await orders.ShipOrderAsync(id);
}
