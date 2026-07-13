using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using TechTalk.SpecFlow;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.SpecFlow.UnitTests;

public class WithValueAndExtendTests
{
    public class Adres
    {
        public string Straat { get; set; } = "";
        public string Plaats { get; set; } = "";
    }

    public class Klant
    {
        public string Naam { get; set; } = "";
        public string Land { get; set; } = "";
        public Adres Adres { get; set; } = new();
    }

    // Cross-cutting laag met een default (Land = "NL"), toegepast op elke Build. Als Extend de cross-cutting laag
    // zou gebruiken, zou de default opnieuw worden toegepast - dat willen we juist NIET (Extend gebruikt
    // een kale builder).
    public sealed class KlantDefaults<TModel>(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<KlantDefaults<TModel>, TModel>(options, xprovider)
        where TModel : class
    {
        protected override void SetDefaults()
        {
            if (typeof(TModel) == typeof(Klant))
            {
                With("Land", "NL");
            }
        }
    }

    private static IModelBuilderProvider CreateProvider() =>
        new ServiceCollection()
            .AddXModelBuilder()
            .AddCrossCuttingModelBuilder(typeof(KlantDefaults<>))
            .BuildServiceProvider()
            .GetRequiredService<IModelBuilderProvider>();

    private static Table AdresTabel()
    {
        var table = new Table("Field", "Value");
        table.AddRow("Straat", "Hoofdstraat");
        table.AddRow("Plaats", "Amsterdam");
        return table;
    }

    [Fact]
    public void WithValue_FillsNestedMemberFromItsOwnTable_DuringBuild()
    {
        // Arrange & Act
        var klant = CreateProvider().For<Klant>()
            .With(k => k.Naam, "Bob")
            .WithValue(k => k.Adres, AdresTabel())
            .Build();

        // Assert
        Assert.Equal("Bob", klant.Naam);
        Assert.Equal("NL", klant.Land);              // custom-builder default draait wél bij een gewone Build
        Assert.Equal("Hoofdstraat", klant.Adres.Straat);
        Assert.Equal("Amsterdam", klant.Adres.Plaats);
    }

    [Fact]
    public void Extend_SetsNestedMemberOntoExistingInstance_ViaPlainBuilder()
    {
        // Arrange
        var provider = CreateProvider();

        var klant = provider.For<Klant>().With(k => k.Naam, "Alice").Build();
        klant.Land = "BE"; // afwijkend van de custom-builder default

        // Act
        var extended = provider.Extend(klant, k => k.Adres, AdresTabel());

        // Assert
        Assert.Same(klant, extended);
        Assert.Equal("Hoofdstraat", extended.Adres.Straat); // geneste member gezet
        Assert.Equal("Amsterdam", extended.Adres.Plaats);
        Assert.Equal("Alice", extended.Naam);               // bestaand veld behouden
        Assert.Equal("BE", extended.Land);                  // NIET teruggezet naar "NL" -> kale builder gebruikt
    }
}
