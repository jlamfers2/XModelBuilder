using XModelBuilder.Demo.Shop.Contracts;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Domains.Ordering;

/// <summary>The order(s) touched by the scenario. The Ordering domain's own scenario context.</summary>
public sealed class OrderContext
{
    public OrderResponse? Current { get; set; }

    public int CurrentId =>
        Current?.Id ?? throw new InvalidOperationException("No order has been placed yet in this scenario.");
}
