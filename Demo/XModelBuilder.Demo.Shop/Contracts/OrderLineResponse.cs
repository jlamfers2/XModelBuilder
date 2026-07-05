namespace XModelBuilder.Demo.Shop.Contracts;

/// <summary>One line on an <see cref="OrderResponse"/>.</summary>
public class OrderLineResponse
{
    public string Sku { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}
