using Microsoft.Extensions.DependencyInjection;
using XModelBuilder.Demo.Shop.IntegrationTests.Domains.Catalog;
using XModelBuilder.Demo.Shop.IntegrationTests.Domains.Customers;
using XModelBuilder.Demo.Shop.IntegrationTests.Domains.Ordering;
using XModelBuilder.DependencyInjection;
using XModelBuilder.Fakers.Bogus;
using XModelBuilder.Fakers.XFaker;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Support;

/// <summary>
/// One reusable registration of the whole XModelBuilder setup (provider + both fakers + every shop
/// builder). Deliberately shared by BOTH DI layers: the test-base container (used by the seeder,
/// server-side) and the per-scenario container (used by the step definitions). Passing a different
/// <paramref name="seed"/> keeps the two independent.
///
/// <para>
/// Two fakers are registered side by side to show they coexist without colliding (their tokens are
/// namespaced): the dependency-free <c>XFaker</c> (used for product SKUs) and <c>Bogus</c> in locale
/// "nl" (used for realistic Dutch names and addresses).
/// </para>
/// </summary>
public static class ShopModelBuilders
{
    public static IServiceCollection AddShopModelBuilders(this IServiceCollection services, int seed)
    {
        services
            .AddXModelBuilder()
            .AddXFaker(seed)
            .AddBogusFaker(seed, "nl")
            .AddModelBuilder<CustomerBuilder>()
            .AddModelBuilder<WarehouseCustomerBuilder>()
            .AddModelBuilder<AdminCustomerBuilder>()
            .AddModelBuilder<AddressBuilder>()
            .AddModelBuilder<ProductBuilder>()
            .AddModelBuilder<AddressRequestBuilder>()
            .AddModelBuilder<PlaceOrderRequestBuilder>()
            .UseAsDefaultModelBuilder<CustomerBuilder>()
            .ValidateXModelBuilderRegistrations();

        return services;
    }
}
