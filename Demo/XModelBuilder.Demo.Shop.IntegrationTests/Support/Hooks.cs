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

/// <summary>
/// Per-scenario transaction boundary: begin before the first step, roll back after the last. This is
/// the "reset to the initial seed" mechanism - and because the seed was committed and the scenario's
/// own writes are not, they stay inspectable in SSMS via READ UNCOMMITTED while paused at a breakpoint.
/// </summary>
[Binding]
public sealed class DatabaseHooks
{
    [BeforeScenario(Order = 0)]
    public void BeginTransaction() => HostHooks.Instance.Database.BeginScenarioTransaction();

    [AfterScenario(Order = 0)]
    public void RollbackTransaction() => HostHooks.Instance.Database.RollbackScenarioTransaction();
}
