using Microsoft.Extensions.Options;
using XModelBuilder.Demo.Shop.Contracts;
using XModelBuilder.Fakers.Bogus;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Domains.Ordering;

/// <summary>
/// A default NL address for the shipping and billing side of an order request. Filler values come from
/// the seeded <see cref="BogusFaker"/> (locale "nl"), so streets/cities look realistic yet stay
/// deterministic.
/// </summary>
[ModelBuilder("address")]
public sealed class AddressRequestBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
    : ModelBuilder<AddressRequestBuilder, AddressRequest>(options, xprovider)
{
    protected override void SetDefaults()
    {
        With(a => a.Street, x => x.Faker<BogusFaker>().Bogus.Address.StreetName());
        With(a => a.HouseNumber, x => x.Faker<BogusFaker>().Bogus.Address.BuildingNumber());
        With(a => a.PostalCode, x => x.Faker<BogusFaker>().Bogus.Address.ZipCode());
        With(a => a.City, x => x.Faker<BogusFaker>().Bogus.Address.City());
        With(a => a.Country, "NL");
    }
}
