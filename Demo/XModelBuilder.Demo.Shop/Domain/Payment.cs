namespace XModelBuilder.Demo.Shop.Domain;

/// <summary>The payment captured for an <see cref="Order"/>.</summary>
public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public PaymentMethodType Method { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime? CapturedAt { get; set; }
}
