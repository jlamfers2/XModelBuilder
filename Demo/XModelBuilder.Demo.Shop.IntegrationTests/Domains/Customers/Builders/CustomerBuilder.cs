using Microsoft.Extensions.Options;
using XModelBuilder.Demo.Shop.Domain;
using XModelBuilder.Fakers.Bogus;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Domains.Customers;

/// <summary>
/// The default <see cref="Customer"/> builder (role Customer). This is the showcase for
/// <c>[ModelBuilder(name)]</c> + <c>UseAsDefaultModelBuilder</c> together with the
/// <see cref="WarehouseCustomerBuilder"/> and <see cref="AdminCustomerBuilder"/> variants for the same
/// model type. Filler name/email come from the seeded <see cref="BogusFaker"/> (locale "nl"), so they
/// are realistic yet deterministic.
/// </summary>
[ModelBuilder("customer")]
public sealed class CustomerBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
    : ModelBuilder<CustomerBuilder, Customer>(options, xprovider)
{
    protected override void SetDefaults()
    {
        With(c => c.Role, UserRole.Customer);
        With(c => c.FullName, x => x.Faker<BogusFaker>().Bogus.Name.FullName());
        With(c => c.Email, x => x.Faker<BogusFaker>().Bogus.Internet.Email());
    }
}
