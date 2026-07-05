namespace XModelBuilder.Demo.Shop.Contracts;

/// <summary>The API view of a catalog product.</summary>
public class ProductResponse
{
    public string Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int StockQuantity { get; set; }
    public string Category { get; set; } = null!;
}
