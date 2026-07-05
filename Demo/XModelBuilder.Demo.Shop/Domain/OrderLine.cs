using System.ComponentModel.DataAnnotations.Schema;

namespace XModelBuilder.Demo.Shop.Domain;

/// <summary>A single line on an <see cref="Order"/>, snapshotting the product name and price.</summary>
public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }

    public int ProductId { get; set; }
    public string Sku { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }

    [NotMapped]
    public decimal LineTotal => UnitPrice * Quantity;
}
