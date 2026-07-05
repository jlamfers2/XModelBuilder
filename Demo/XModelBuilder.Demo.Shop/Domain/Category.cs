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
