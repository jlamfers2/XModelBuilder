namespace XModelBuilder.Demo.Shop.Contracts;

/// <summary>The request to add a product to the catalog (admin-only).</summary>
public class CreateProductRequest
{
    public string Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int StockQuantity { get; set; }
    public string Category { get; set; } = null!;
}
