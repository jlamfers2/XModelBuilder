using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.EntityFrameworkCore;

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

/// <summary>A single line on an <see cref="Order"/>, snapshotting the product name and price.</summary>
public class OrderLine
{
    public int Id { get; set; }
    public int OrderId { get; set; }

    public int ProductId { get; set; }
    public string Sku { get; set; } = null!;
    public string ProductName { get; set; } = null!;
    public decimal UnitPrice { get; set; }
    public int Quantity { get; set; }

    [NotMapped]
    public decimal LineTotal => UnitPrice * Quantity;
}

/// <summary>An owned value object: the address as captured on the order (not a customer address row).</summary>
[Owned]
public class OrderAddress
{
    public string Street { get; set; } = null!;
    public string HouseNumber { get; set; } = null!;
    public string PostalCode { get; set; } = null!;
    public string City { get; set; } = null!;
    public string Country { get; set; } = "NL";
}

public class Payment
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public PaymentMethodType Method { get; set; }
    public decimal Amount { get; set; }
    public PaymentStatus Status { get; set; }
    public DateTime? CapturedAt { get; set; }
}

public class OrderStatusHistoryEntry
{
    public int Id { get; set; }
    public int OrderId { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime ChangedAt { get; set; }
}
