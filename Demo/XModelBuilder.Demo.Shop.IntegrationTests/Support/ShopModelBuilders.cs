using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
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
///
/// <para>
/// It also wires the CROSS-CUTTING layer (<see cref="EntityDefaults{TModel}"/>): a single open-generic
/// builder, applied to every build, that stamps a deterministic audit <c>CreatedAt</c> on every
/// <see cref="Domain.IAuditable"/> entity (README chapter 5). Its clock is a frozen
/// <see cref="FixedTimeProvider"/> - replacing the app's system clock in tests - so that timestamp, the
/// server's order timestamps and XFaker's age tokens are all reproducible.
/// </para>
/// </summary>
public static class ShopModelBuilders
{
    /// <summary>The instant the suite's frozen clock reports, so every clock-bound value is reproducible.</summary>
    private static readonly DateTimeOffset FrozenNow = new(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);

    public static IServiceCollection AddShopModelBuilders(this IServiceCollection services, int seed)
    {
        // Freeze the clock for the whole suite (both DI layers): drives the cross-cutting CreatedAt,
        // the server's PlacedAt/OrderNumber and XFaker's age tokens deterministically. Replaces the
        // app's TimeProvider.System (registered in Program.cs) when this runs in ConfigureTestServices.
        services.RemoveAll<TimeProvider>();
        services.AddSingleton<TimeProvider>(new FixedTimeProvider(FrozenNow));

        services
            .AddXModelBuilder()
            .AddXFaker(seed)
            .AddBogusFaker(seed, "nl")
            .AddCrossCuttingModelBuilder(typeof(EntityDefaults<>))
            .AddModelBuilder<CustomerBuilder>()
            .AddModelBuilder<WarehouseCustomerBuilder>()
            .AddModelBuilder<AdminCustomerBuilder>()
            .AddModelBuilder<AddressBuilder>()
            .AddModelBuilder<ProductBuilder>()
            .AddModelBuilder<AddressRequestBuilder>()
            .AddModelBuilder<PlaceOrderRequestBuilder>()
            .ValidateXModelBuilderRegistrations();

        return services;
    }
}
