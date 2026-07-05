using XModelBuilder.Demo.Shop.Contracts;
using XModelBuilder.Demo.Shop.Domain;

namespace XModelBuilder.Demo.Shop.Services;

internal static class OrderMapper
{
    public static OrderResponse ToResponse(Order order, string customerEmail) => new()
    {
        Id = order.Id,
        OrderNumber = order.OrderNumber,
        CustomerEmail = customerEmail,
        Status = order.Status,
        PaymentStatus = order.Payment.Status,
        SubtotalAmount = order.SubtotalAmount,
        DiscountAmount = order.DiscountAmount,
        TotalAmount = order.TotalAmount,
        Lines = order.Lines.Select(l => new OrderLineResponse
        {
            Sku = l.Sku,
            ProductName = l.ProductName,
            UnitPrice = l.UnitPrice,
            Quantity = l.Quantity,
            LineTotal = l.LineTotal,
        }).ToList(),
    };
}
