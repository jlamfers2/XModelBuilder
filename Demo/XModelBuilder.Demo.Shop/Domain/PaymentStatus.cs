namespace XModelBuilder.Demo.Shop.Domain;

/// <summary>The lifecycle of a payment on an order.</summary>
public enum PaymentStatus
{
    Pending,
    Captured,
    Failed,
}
