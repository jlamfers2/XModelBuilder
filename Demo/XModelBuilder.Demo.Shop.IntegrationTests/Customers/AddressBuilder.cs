using Microsoft.Extensions.Options;
using XModelBuilder.Demo.Shop.Domain;
using XModelBuilder.Fakers.Bogus;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Customers;

/// <summary>
/// Builder for a customer's <see cref="Address"/> (their address book entry). Every field defaults from
/// the seeded <see cref="BogusFaker"/> (locale "nl"), so a scenario only fills in what it wants to
/// assert on. Used by the aggregated "build a customer step by step" feature.
/// </summary>
[ModelBuilder("customerAddress")]
public sealed class AddressBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels)
    : ModelBuilder<AddressBuilder, Address>(options, xmodels)
{
    protected override void SetDefaults()
    {
        With(a => a.Kind, AddressKind.Shipping);
        With(a => a.Street, x => x.Faker<BogusFaker>().Bogus.Address.StreetName());
        With(a => a.HouseNumber, x => x.Faker<BogusFaker>().Bogus.Address.BuildingNumber());
        With(a => a.PostalCode, x => x.Faker<BogusFaker>().Bogus.Address.ZipCode());
        With(a => a.City, x => x.Faker<BogusFaker>().Bogus.Address.City());
        With(a => a.Country, "NL");
    }
}
