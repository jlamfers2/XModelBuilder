using Microsoft.EntityFrameworkCore;
using XModelBuilder.Demo.Shop.Contracts;
using XModelBuilder.Demo.Shop.Data;
using XModelBuilder.Demo.Shop.Domain;

namespace XModelBuilder.Demo.Shop.Services;

/// <summary>
/// The order use-cases and their business rules: placing (stock check + discount), paying and
/// shipping (a small state machine), plus ownership-aware reads. Roles are enforced at the API
/// boundary; ownership ("a customer only sees their own orders") is enforced here.
/// </summary>
public sealed class OrderService(ShopDbContext db, ICurrentUser currentUser, TimeProvider clock)
{
    private static readonly Dictionary<string, decimal> DiscountCodes =
        new(StringComparer.OrdinalIgnoreCase) { ["WELCOME10"] = 0.10m };

    public async Task<OrderResponse> PlaceOrderAsync(PlaceOrderRequest request)
    {
        if (request.Lines.Count == 0)
        {
            throw new BusinessRuleException("An order must contain at least one line.");
        }

        var customer = await RequireCurrentCustomerAsync();
        var now = clock.GetUtcNow().UtcDateTime;

        var order = new Order
        {
            CustomerId = customer.Id,
            Status = OrderStatus.Pending,
            PlacedAt = now,
            OrderNumber = await NextOrderNumberAsync(now),
            ShippingAddress = ToOrderAddress(request.ShippingAddress),
            BillingAddress = ToOrderAddress(request.BillingAddress),
            StatusHistory = [new OrderStatusHistoryEntry { Status = OrderStatus.Pending, ChangedAt = now }],
        };

        foreach (var lineRequest in request.Lines)
        {
            var product = await db.Products.FirstOrDefaultAsync(p => p.Sku == lineRequest.Sku)
                ?? throw new NotFoundException($"Unknown product '{lineRequest.Sku}'.");

            if (lineRequest.Quantity <= 0)
            {
                throw new BusinessRuleException($"Quantity for '{lineRequest.Sku}' must be positive.");
            }

            if (product.StockQuantity < lineRequest.Quantity)
            {
                throw new BusinessRuleException(
                    $"Insufficient stock for '{product.Sku}': requested {lineRequest.Quantity}, available {product.StockQuantity}.");
            }

            product.StockQuantity -= lineRequest.Quantity;
            order.Lines.Add(new OrderLine
            {
                ProductId = product.Id,
                Sku = product.Sku,
                ProductName = product.Name,
                UnitPrice = product.UnitPrice,
                Quantity = lineRequest.Quantity,
            });
        }

        order.SubtotalAmount = order.Lines.Sum(l => l.LineTotal);
        order.DiscountAmount = ResolveDiscount(request.DiscountCode, order.SubtotalAmount);
        order.TotalAmount = order.SubtotalAmount - order.DiscountAmount;

        order.Payment = new Payment
        {
            Method = request.PaymentMethod,
            Amount = order.TotalAmount,
            Status = PaymentStatus.Pending,
        };

        db.Orders.Add(order);
        await db.SaveChangesAsync();

        return OrderMapper.ToResponse(order, customer.Email);
    }

    public async Task<OrderResponse> PayOrderAsync(int orderId)
    {
        var order = await LoadOwnedOrderAsync(orderId);

        if (order.Status != OrderStatus.Pending)
        {
            throw new BusinessRuleException($"Order '{order.OrderNumber}' cannot be paid from status {order.Status}.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        order.Payment.Status = PaymentStatus.Captured;
        order.Payment.CapturedAt = now;
        order.Status = OrderStatus.Paid;
        order.StatusHistory.Add(new OrderStatusHistoryEntry { Status = OrderStatus.Paid, ChangedAt = now });

        await db.SaveChangesAsync();
        return OrderMapper.ToResponse(order, order.Customer.Email);
    }

    public async Task<OrderResponse> ShipOrderAsync(int orderId)
    {
        var order = await LoadOrderGraphAsync(orderId)
            ?? throw new NotFoundException($"Order '{orderId}' was not found.");

        if (order.Status != OrderStatus.Paid)
        {
            throw new BusinessRuleException($"Order '{order.OrderNumber}' cannot be shipped from status {order.Status}.");
        }

        var now = clock.GetUtcNow().UtcDateTime;
        order.Status = OrderStatus.Shipped;
        order.StatusHistory.Add(new OrderStatusHistoryEntry { Status = OrderStatus.Shipped, ChangedAt = now });

        await db.SaveChangesAsync();
        return OrderMapper.ToResponse(order, order.Customer.Email);
    }

    public async Task<OrderResponse> GetOrderAsync(int orderId)
    {
        var order = await LoadOwnedOrderAsync(orderId);
        return OrderMapper.ToResponse(order, order.Customer.Email);
    }

    public async Task<IReadOnlyList<OrderResponse>> GetCustomerOrdersAsync(string email)
    {
        if (!IsPrivileged() && !string.Equals(email, currentUser.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("You may only view your own orders.");
        }

        var orders = await LoadOrderGraph(db.Orders)
            .Where(o => o.Customer.Email == email)
            .ToListAsync();

        return orders.Select(o => OrderMapper.ToResponse(o, o.Customer.Email)).ToList();
    }

    // --- helpers ---

    private bool IsPrivileged() =>
        currentUser.IsInRole(Roles.Admin) || currentUser.IsInRole(Roles.WarehouseOperator);

    private async Task<Order> LoadOwnedOrderAsync(int orderId)
    {
        var order = await LoadOrderGraphAsync(orderId)
            ?? throw new NotFoundException($"Order '{orderId}' was not found.");

        if (!IsPrivileged() && !string.Equals(order.Customer.Email, currentUser.Email, StringComparison.OrdinalIgnoreCase))
        {
            throw new ForbiddenException("You may only access your own orders.");
        }

        return order;
    }

    private Task<Order?> LoadOrderGraphAsync(int orderId) =>
        LoadOrderGraph(db.Orders).FirstOrDefaultAsync(o => o.Id == orderId);

    private static IQueryable<Order> LoadOrderGraph(IQueryable<Order> orders) =>
        orders.Include(o => o.Customer)
              .Include(o => o.Lines)
              .Include(o => o.Payment)
              .Include(o => o.StatusHistory);

    private async Task<Customer> RequireCurrentCustomerAsync()
    {
        var email = currentUser.Email
            ?? throw new ForbiddenException("No authenticated customer.");

        return await db.Customers.FirstOrDefaultAsync(c => c.Email == email)
            ?? throw new NotFoundException($"Unknown customer '{email}'.");
    }

    private static decimal ResolveDiscount(string? code, decimal subtotal)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            return 0m;
        }

        if (!DiscountCodes.TryGetValue(code, out var rate))
        {
            throw new BusinessRuleException($"Unknown discount code '{code}'.");
        }

        return Math.Round(subtotal * rate, 2);
    }

    private static OrderAddress ToOrderAddress(AddressRequest a) => new()
    {
        Street = a.Street,
        HouseNumber = a.HouseNumber,
        PostalCode = a.PostalCode,
        City = a.City,
        Country = a.Country,
    };

    private async Task<string> NextOrderNumberAsync(DateTime now)
    {
        var todaysCount = await db.Orders.CountAsync(o => o.PlacedAt.Date == now.Date);
        return $"ORD-{now:yyyyMMdd}-{todaysCount + 1:D4}";
    }
}
