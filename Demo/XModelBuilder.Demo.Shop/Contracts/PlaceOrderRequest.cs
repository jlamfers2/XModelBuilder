using XModelBuilder.Demo.Shop.Domain;

namespace XModelBuilder.Demo.Shop.Contracts;

/// <summary>
/// The request to place an order: a small object graph of lines + shipping/billing address + payment.
/// The integration tests build this shape with XModelBuilder from a compact Gherkin table.
/// </summary>
public class PlaceOrderRequest
{
    public List<OrderLineRequest> Lines { get; set; } = [];
    public AddressRequest ShippingAddress { get; set; } = new();
    public AddressRequest BillingAddress { get; set; } = new();
    public PaymentMethodType PaymentMethod { get; set; }
    public string? DiscountCode { get; set; }
}
