using XModelBuilder.Demo.Shop.Domain;

namespace XModelBuilder.Demo.Shop.Contracts;

/// <summary>The API view of an order returned after placing, paying or shipping it.</summary>
public class OrderResponse
{
    public int Id { get; set; }
    public string OrderNumber { get; set; } = null!;
    public string CustomerEmail { get; set; } = null!;
    public OrderStatus Status { get; set; }
    public PaymentStatus PaymentStatus { get; set; }
    public decimal SubtotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TotalAmount { get; set; }
    public List<OrderLineResponse> Lines { get; set; } = [];
}
