using Microsoft.Extensions.Options;
using XModelBuilder.Demo.Shop.Domain;
using XModelBuilder.Fakers.Bogus;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Domains.Customers;

/// <summary>A <see cref="Customer"/> with the warehouse-operator role; same model type, other defaults.</summary>
[ModelBuilder("warehouse")]
public sealed class WarehouseCustomerBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
    : ModelBuilder<WarehouseCustomerBuilder, Customer>(options, xprovider)
{
    protected override void SetDefaults()
    {
        With(c => c.Role, UserRole.WarehouseOperator);
        With(c => c.FullName, x => x.Faker<BogusFaker>().Bogus.Name.FullName());
        With(c => c.Email, x => x.Faker<BogusFaker>().Bogus.Internet.Email());
    }
}
