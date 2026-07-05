using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XModelBuilder.Demo.Shop.Data;
using XModelBuilder.Demo.Shop.IntegrationTests.ModelBuilders;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Support.Infrastructure;

/// <summary>
/// The TEST-BASE DI layer. It boots the real application (<c>Program</c>) but overrides three things
/// application-wide: the <c>ShopDbContext</c> is rewired onto the shared test connection + current
/// transaction, authentication is swapped for <see cref="TestAuthHandler"/>, and XModelBuilder is
/// registered so the seeder can build entities. Everything else is the production wiring.
/// </summary>
public sealed class CustomWebApplicationFactory(TestDatabase database) : WebApplicationFactory<Program>
{
    /// <summary>A distinct seed from the scenario-side provider, so seeding and step-building stay independent.</summary>
    private const int SeederSeed = 20240501;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");

        builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<DbContextOptions<ShopDbContext>>();
            services.RemoveAll<ShopDbContext>();
            services.AddScoped(_ =>
            {
                var options = new DbContextOptionsBuilder<ShopDbContext>()
                    .UseSqlServer(database.Connection)
                    .Options;

                var context = new ShopDbContext(options);
                if (database.CurrentTransaction is not null)
                {
                    context.Database.UseTransaction(database.CurrentTransaction);
                }

                return context;
            });

            services
                .AddAuthentication(TestAuthHandler.SchemeName)
                .AddScheme<AuthenticationSchemeOptions, TestAuthHandler>(TestAuthHandler.SchemeName, _ => { });

            services.AddShopModelBuilders(SeederSeed);
        });
    }
}
