namespace XModelBuilder.Demo.Shop.Domain;

/// <summary>A sellable product. <see cref="Sku"/> is the natural key used in order requests.</summary>
public class Product : IAuditable
{
    public int Id { get; set; }
    public string Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int StockQuantity { get; set; }
    public DateTime CreatedAt { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}
