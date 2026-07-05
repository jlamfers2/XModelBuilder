using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using XModelBuilder.Demo.Shop.Contracts;
using XModelBuilder.Demo.Shop.Domain;
using XModelBuilder.Demo.Shop.Services;

namespace XModelBuilder.Demo.Shop.Controllers;

[ApiController]
[Route("api/products")]
public sealed class ProductsController(CatalogService catalog) : ControllerBase
{
    [HttpGet]
    [AllowAnonymous]
    public async Task<ActionResult<IReadOnlyList<ProductResponse>>> List() =>
        Ok(await catalog.GetProductsAsync());

    [HttpPost]
    [Authorize(Roles = Roles.Admin)]
    public async Task<ActionResult<ProductResponse>> Create([FromBody] CreateProductRequest request)
    {
        var product = await catalog.AddProductAsync(request);
        return CreatedAtAction(nameof(List), null, product);
    }
}
