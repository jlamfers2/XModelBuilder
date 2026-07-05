using Microsoft.EntityFrameworkCore;
using XModelBuilder.Demo.Shop.Contracts;
using XModelBuilder.Demo.Shop.Data;
using XModelBuilder.Demo.Shop.Domain;

namespace XModelBuilder.Demo.Shop.Services;

public sealed class CatalogService(ShopDbContext db)
{
    public async Task<ProductResponse> AddProductAsync(CreateProductRequest request)
    {
        if (await db.Products.AnyAsync(p => p.Sku == request.Sku))
        {
            throw new BusinessRuleException($"A product with SKU '{request.Sku}' already exists.");
        }

        var category = await db.Categories.FirstOrDefaultAsync(c => c.Name == request.Category)
            ?? new Category { Name = request.Category };

        var product = new Product
        {
            Sku = request.Sku,
            Name = request.Name,
            UnitPrice = request.UnitPrice,
            StockQuantity = request.StockQuantity,
            Category = category,
        };

        db.Products.Add(product);
        await db.SaveChangesAsync();

        return ToResponse(product);
    }

    public async Task<IReadOnlyList<ProductResponse>> GetProductsAsync()
    {
        var products = await db.Products.Include(p => p.Category).OrderBy(p => p.Sku).ToListAsync();
        return products.Select(ToResponse).ToList();
    }

    private static ProductResponse ToResponse(Product p) => new()
    {
        Sku = p.Sku,
        Name = p.Name,
        UnitPrice = p.UnitPrice,
        StockQuantity = p.StockQuantity,
        Category = p.Category.Name,
    };
}
