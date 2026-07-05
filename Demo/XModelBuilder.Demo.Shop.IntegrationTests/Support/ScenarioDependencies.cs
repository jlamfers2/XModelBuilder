using Microsoft.Extensions.DependencyInjection;
using Reqnroll.Microsoft.Extensions.DependencyInjection;
using XModelBuilder.Demo.Shop.IntegrationTests.Contexts;
using XModelBuilder.Demo.Shop.IntegrationTests.Drivers;
using XModelBuilder.Demo.Shop.IntegrationTests.ModelBuilders;
using XModelBuilder.Demo.Shop.IntegrationTests.Support.Infrastructure;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Support;

/// <summary>
/// The SCENARIO-SPECIFIC DI composition root (Reqnroll's Microsoft.Extensions.DependencyInjection
/// plugin). A fresh container/scope is created per scenario; binding (step) classes are auto-registered
/// and get their typed contexts and drivers injected. The run-wide host is exposed as a singleton, and
/// XModelBuilder is registered again here (separate seed) for building request models inside steps.
/// </summary>
public static class ScenarioDependencies
{
    private const int ScenarioSeed = 987654321;

    [ScenarioDependencies]
    public static IServiceCollection CreateServices()
    {
        var services = new ServiceCollection();

        services.AddSingleton(_ => HostHooks.Instance);
        services.AddScoped(sp => sp.GetRequiredService<ShopTestHost>().CreateClient());

        services.AddShopModelBuilders(ScenarioSeed);

        // Typed, per-scenario contexts (specific + one aggregate).
        services.AddScoped<CurrentUserContext>();
        services.AddScoped<HttpResponseContext>();
        services.AddScoped<OrderContext>();
        services.AddScoped<CatalogContext>();
        services.AddScoped<ScenarioState>();

        // Drivers (generic base is abstract; specific + one aggregate), all constructor-injected.
        services.AddScoped<AuthenticationDriver>();
        services.AddScoped<OrderApiDriver>();
        services.AddScoped<CatalogApiDriver>();
        services.AddScoped<ShopDriver>();

        return services;
    }
}
