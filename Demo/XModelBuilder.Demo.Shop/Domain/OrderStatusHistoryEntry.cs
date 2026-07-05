namespace XModelBuilder.Demo.Shop.Domain;

/// <summary>One transition in an <see cref="Order"/>'s status history.</summary>
public class OrderStatusHistoryEntry
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime ChangedAt { get; set; }
}
