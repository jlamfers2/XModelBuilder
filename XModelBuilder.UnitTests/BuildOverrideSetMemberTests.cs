using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.UnitTests;

public class BuildOverrideSetMemberTests
{
    public class Bundle
    {
        public Bundle(decimal price) => Price = price; // ctor-only (read-only, backing field)

        public decimal Price { get; }                  // getter-only, gezet via de constructor
        public decimal? PriceWithVat { get; init; }    // init-only
        public string Code { get; } = "";              // getter-only met initializer (backing field)
    }

    // Berekende cross-field defaults in een Build-override: na base.Build() staan alle With/ctor-waarden
    // er, en SetMember zet de afgeleide velden - óók de init-only en de getter-only-met-backing-field.
    [ModelBuilder("default")]
    public sealed class BundleBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels)
        : ModelBuilder<BundleBuilder, Bundle>(options, xmodels)
    {
        protected override void SetDefaults() { }

        public override Bundle Build()
        {
            var bundle = base.Build();

            if (bundle.PriceWithVat is null)                              // alleen als niet opgegeven
            {
                SetMember(bundle, x => x.PriceWithVat, bundle.Price * 1.21m); // init-only via helper
            }

            SetMember(bundle, x => x.Code, $"BUNDLE-{bundle.Price}");     // getter-only / backing field

            return bundle;
        }
    }

    private static IModelBuilderProvider CreateProvider() =>
        new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuilder<BundleBuilder>()
            .BuildServiceProvider()
            .GetRequiredService<IModelBuilderProvider>();

    [Fact]
    public void ComputedDefaults_AreApplied_ToReadOnly_Init_And_BackingFieldMembers()
    {
        // Arrange & Act
        var bundle = CreateProvider().For<Bundle>().With("Price", "100").Build();

        // Assert
        Assert.Equal(100m, bundle.Price);
        Assert.Equal(121m, bundle.PriceWithVat);     // init-only, berekend uit Price
        Assert.Equal("BUNDLE-100", bundle.Code);     // backing field, berekend uit Price
    }

    [Fact]
    public void ExplicitValue_IsNotOverwritten_ByTheComputedDefault()
    {
        // Arrange & Act
        var bundle = CreateProvider().For<Bundle>()
            .With("Price", "100")
            .With("PriceWithVat", "150")             // expliciet opgegeven
            .Build();

        // Assert
        Assert.Equal(150m, bundle.PriceWithVat);     // blijft 150, niet herrekend
    }
}
