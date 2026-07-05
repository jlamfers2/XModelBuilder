using XModelBuilder.Demo.Shop.Domain;

namespace XModelBuilder.Demo.Shop.Contracts;

/// <summary>
/// Request/response DTOs shared with the integration tests. The tests build the <em>request</em>
/// shapes (notably <see cref="PlaceOrderRequest"/>, a small object graph of its own) with XModelBuilder.
/// </summary>
public class AddressRequest
{
    public string Street { get; set; } = null!;
    public string HouseNumber { get; set; } = null!;
    public string PostalCode { get; set; } = null!;
    public string City { get; set; } = null!;
    public string Country { get; set; } = "NL";
}

public class OrderLineRequest
{
    public string Sku { get; set; } = null!;
    public int Quantity { get; set; }
}

public class PlaceOrderRequest
{
    public List<OrderLineRequest> Lines { get; set; } = [];
    public AddressRequest ShippingAddress { get; set; } = new();
    public AddressRequest BillingAddress { get; set; } = new();
    public PaymentMethodType PaymentMethod { get; set; }
    public string? DiscountCode { get; set; }
}

public class CreateProductRequest
{
    public string Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int StockQuantity { get; set; }
    public string Category { get; set; } = null!;
}

public class OrderLineResponse
{
    public string Sku { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }
    public decimal LineTotal { get; set; }
}

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

public class ProductResponse
{
    public string Sku { get; set; } = null!;
    public string Name { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int StockQuantity { get; set; }
    public string Category { get; set; } = null!;
}
