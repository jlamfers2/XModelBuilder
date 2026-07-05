using Microsoft.Extensions.DependencyInjection;
using XModelBuilder.DependencyInjection;
using XModelBuilder.Fakers.XFaker;

namespace XModelBuilder.Demo.Shop.IntegrationTests.ModelBuilders;

/// <summary>
/// One reusable registration of the whole XModelBuilder setup (provider + seeded XFaker + all shop
/// builders). Deliberately shared by BOTH DI layers: the test-base container (used by the seeder,
/// server-side) and the per-scenario container (used by the step definitions). Passing a different
/// <paramref name="seed"/> keeps the two independent.
/// </summary>
public static class ShopModelBuilders
{
    public static IServiceCollection AddShopModelBuilders(this IServiceCollection services, int seed)
    {
        services
            .AddXModelBuilder()
            .AddXFaker(seed)
            .AddModelBuilder<CustomerBuilder>()
            .AddModelBuilder<WarehouseCustomerBuilder>()
            .AddModelBuilder<AdminCustomerBuilder>()
            .AddModelBuilder<ProductBuilder>()
            .AddModelBuilder<AddressRequestBuilder>()
            .AddModelBuilder<PlaceOrderRequestBuilder>()
            .UseAsDefaultModelBuilder<CustomerBuilder>()
            .ValidateXModelBuilderRegistrations();

        return services;
    }
}
