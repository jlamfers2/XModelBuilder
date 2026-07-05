namespace XModelBuilder.Demo.Shop.Contracts;

/// <summary>One requested order line: a product SKU and a quantity.</summary>
public class OrderLineRequest
{
    public string Sku { get; set; } = null!;
    public int Quantity { get; set; }
}
