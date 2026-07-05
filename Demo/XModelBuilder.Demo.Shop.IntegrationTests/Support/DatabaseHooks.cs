using Reqnroll;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Support;

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
