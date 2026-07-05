using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Reqnroll;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.Reqnroll.UnitTests;

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

    // Custom builder met een default (Land = "NL"). Als Extend deze builder zou gebruiken, zou de default
    // opnieuw worden toegepast - dat willen we juist NIET.
    [ModelBuilder("default")]
    public sealed class KlantBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels)
        : ModelBuilder<KlantBuilder, Klant>(options, xmodels)
    {
        protected override void SetDefaults() => With(k => k.Land, "NL");
    }

    private static IModelBuilderProvider CreateProvider() =>
        new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuilder<KlantBuilder>()
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
        var klant = CreateProvider().For<Klant>()
            .With(k => k.Naam, "Bob")
            .WithValue(k => k.Adres, AdresTabel())
            .Build();

        Assert.Equal("Bob", klant.Naam);
        Assert.Equal("NL", klant.Land);              // custom-builder default draait wél bij een gewone Build
        Assert.Equal("Hoofdstraat", klant.Adres.Straat);
        Assert.Equal("Amsterdam", klant.Adres.Plaats);
    }

    [Fact]
    public void Extend_SetsNestedMemberOntoExistingInstance_ViaPlainBuilder()
    {
        var provider = CreateProvider();

        var klant = provider.For<Klant>().With(k => k.Naam, "Alice").Build();
        klant.Land = "BE"; // afwijkend van de custom-builder default

        var extended = provider.Extend(klant, k => k.Adres, AdresTabel());

        Assert.Same(klant, extended);
        Assert.Equal("Hoofdstraat", extended.Adres.Straat); // geneste member gezet
        Assert.Equal("Amsterdam", extended.Adres.Plaats);
        Assert.Equal("Alice", extended.Naam);               // bestaand veld behouden
        Assert.Equal("BE", extended.Land);                  // NIET teruggezet naar "NL" -> kale builder gebruikt
    }
}
