using Microsoft.Extensions.Options;
using XModelBuilder.Demo.Shop.Domain;
using XModelBuilder.Fakers.Bogus;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Domains.Customers;

/// <summary>A <see cref="Customer"/> with the admin role; same model type, other defaults.</summary>
[ModelBuilder("admin")]
public sealed class AdminCustomerBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels)
    : ModelBuilder<AdminCustomerBuilder, Customer>(options, xmodels)
{
    protected override void SetDefaults()
    {
        With(c => c.Role, UserRole.Admin);
        With(c => c.FullName, x => x.Faker<BogusFaker>().Bogus.Name.FullName());
        With(c => c.Email, x => x.Faker<BogusFaker>().Bogus.Internet.Email());
    }
}
