namespace XModelBuilder.Demo.Shop.Domain;

/// <summary>
/// An order: the deep part of the graph. It has line items (each a snapshot of a product), an owned
/// billing and shipping address (value objects captured at purchase time), a payment and a status
/// history.
/// </summary>
public class Order
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = null!;

    public int CustomerId { get; set; }
    public Customer Customer { get; set; } = null!;

    public List<OrderLine> Lines { get; set; } = [];

    public OrderAddress ShippingAddress { get; set; } = null!;
    public OrderAddress BillingAddress { get; set; } = null!;

    public Payment Payment { get; set; } = null!;
    public List<OrderStatusHistoryEntry> StatusHistory { get; set; } = [];

    public OrderStatus Status { get; set; }
    public DateTime PlacedAt { get; set; }

    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
}
