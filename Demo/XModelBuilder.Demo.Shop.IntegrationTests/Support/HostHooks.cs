using Reqnroll;
using XModelBuilder.Demo.Shop.IntegrationTests.Support.Infrastructure;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Support;

/// <summary>
/// Run-wide host lifecycle: the expensive <see cref="ShopTestHost"/> (factory + seeded database) is
/// built once for the whole assembly and disposed at the end. The single instance is exposed to the
/// scenario container via <see cref="Instance"/>.
/// </summary>
[Binding]
public sealed class HostHooks
{
    private static ShopTestHost? _host;

    public static ShopTestHost Instance =>
        _host ?? throw new InvalidOperationException("ShopTestHost has not been initialized.");

    [BeforeTestRun]
    public static void InitializeHost() => _host = ShopTestHost.Create();

    [AfterTestRun]
    public static void TearDownHost() => _host?.Dispose();
}
