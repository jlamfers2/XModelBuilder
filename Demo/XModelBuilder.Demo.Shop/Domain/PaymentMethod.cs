namespace XModelBuilder.Demo.Shop.Domain;

/// <summary>A stored payment method belonging to a <see cref="Customer"/>.</summary>
public class PaymentMethod
{
    public int Id { get; set; }
    public PaymentMethodType Type { get; set; }
    public string Display { get; set; } = null!;

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;
}
