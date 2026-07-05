using Microsoft.Extensions.DependencyInjection;
using XModelBuilder.Demo.Shop.Data;
using XModelBuilder.Demo.Shop.IntegrationTests.Support.Seeding;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Support.Infrastructure;

/// <summary>
/// The expensive, run-wide singleton: it owns the <see cref="TestDatabase"/> and the
/// <see cref="CustomWebApplicationFactory"/>, and it seeds the committed baseline once. Built in
/// <c>[BeforeTestRun]</c>, disposed in <c>[AfterTestRun]</c>. Individual scenarios only begin/roll
/// back a transaction and create an <see cref="System.Net.Http.HttpClient"/>.
/// </summary>
public sealed class ShopTestHost : IDisposable
{
    public TestDatabase Database { get; }
    public CustomWebApplicationFactory Factory { get; }

    private ShopTestHost()
    {
        Database = new TestDatabase();
        Database.RecreateSchema();
        Database.OpenSharedConnection();

        Factory = new CustomWebApplicationFactory(Database);

        SeedBaseline();
    }

    public static ShopTestHost Create() => new();

    public HttpClient CreateClient() => Factory.CreateClient();

    /// <summary>Runs the seeder through the SERVER container, with no scenario transaction active, so it commits.</summary>
    private void SeedBaseline()
    {
        using var scope = Factory.Services.CreateScope();
        var provider = scope.ServiceProvider.GetRequiredService<IModelBuilderProvider>();
        var db = scope.ServiceProvider.GetRequiredService<ShopDbContext>();
        DatabaseSeeder.Seed(db, provider);
    }

    public void Dispose()
    {
        Factory.Dispose();
        Database.Dispose();
    }
}
