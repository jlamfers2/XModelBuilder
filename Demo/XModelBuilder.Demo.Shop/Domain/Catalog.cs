namespace XModelBuilder.Demo.Shop.Domain;

/// <summary>A product category. Self-referencing (<see cref="ParentCategory"/>) to give the graph depth.</summary>
public class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;

    public int? ParentCategoryId { get; set; }
    public Category? ParentCategory { get; set; }

    public List<Product> Products { get; set; } = [];
}

/// <summary>A sellable product. <see cref="Sku"/> is the natural key used in order requests.</summary>
public class Product
{
    public int Id { get; set; }
    public string Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int StockQuantity { get; set; }

    public int CategoryId { get; set; }
    public Category Category { get; set; } = null!;
}
