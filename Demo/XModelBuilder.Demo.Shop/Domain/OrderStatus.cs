namespace XModelBuilder.Demo.Shop.Domain;

/// <summary>The lifecycle of an order through the place → pay → ship state machine.</summary>
public enum OrderStatus
{
    Pending,
    Paid,
    Shipped,
    Cancelled,
}
