using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.UnitTests;

// Extend(instance): bouwt ONTO een bestaande instance i.p.v. een nieuwe, als een one-shot terminal net als
// Build - zonder de interne builder-state te veranderen. Handig om een model over meerdere datasets op te
// bouwen.
public class ExtendTests
{
    public class Order
    {
        public Order(Guid id) => Id = id;             // ctor-only (backing field)

        public Guid Id { get; }
        public string KlantNaam { get; init; } = "";  // init-only
        public string? Stad { get; set; }             // gewone setter
        public int AantalRegels { get; init; }
        public decimal Totaal { get; private set; }   // berekend in de Build-override

        public void ZetTotaal(decimal t) => Totaal = t;
    }

    [ModelBuilder("default")]
    public sealed class OrderBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels)
        : ModelBuilder<OrderBuilder, Order>(options, xmodels)
    {
        protected override void SetDefaults() { }

        public override Order Build()
        {
            var order = base.Build();
            SetMember(order, o => o.Totaal, order.AantalRegels * 10m); // afgeleid, óók bij Extend herberekend
            return order;
        }
    }

    private static IModelBuilder<Order> NewBuilder() =>
        new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuilder<OrderBuilder>()
            .BuildServiceProvider()
            .GetRequiredService<IModelBuilderProvider>()
            .For<Order>();

    [Fact]
    public void Extend_AppliesValuesOntoTheSameInstance_AndReturnsIt()
    {
        var basis = NewBuilder().With(o => o.Id, Guid.NewGuid()).With(o => o.KlantNaam, "Alice").Build();

        var extended = NewBuilder().With(o => o.Stad, "Amsterdam").Extend(basis);

        Assert.Same(basis, extended);              // dezelfde instance
        Assert.Equal("Amsterdam", extended.Stad);  // nieuw veld toegevoegd
        Assert.Equal("Alice", extended.KlantNaam); // bestaand veld behouden
    }

    [Fact]
    public void Extend_RunsBuildOverride_SoDerivedMembersAreRecomputed()
    {
        var basis = NewBuilder().With(o => o.Id, Guid.NewGuid()).With(o => o.AantalRegels, 2).Build();
        Assert.Equal(20m, basis.Totaal);

        // Extend met een builder die AantalRegels op 5 zet -> Build-override herberekent Totaal.
        var extended = NewBuilder().With(o => o.AantalRegels, 5).Extend(basis);

        Assert.Equal(5, extended.AantalRegels);
        Assert.Equal(50m, extended.Totaal);
    }

    [Fact]
    public void Extend_AppliesCtorConfiguredValue_ViaBackingField()
    {
        var basis = NewBuilder().With(o => o.Id, Guid.NewGuid()).Build();
        var nieuweId = Guid.NewGuid();

        // Id is een ctor-only member; bij Extend wordt het toch gezet (via het backing field).
        var extended = NewBuilder().With(o => o.Id, nieuweId).Extend(basis);

        Assert.Equal(nieuweId, extended.Id);
    }

    [Fact]
    public void Extend_UnspecifiedCtorOnlyMember_KeepsExistingValue()
    {
        var basis = NewBuilder().With(o => o.Id, Guid.NewGuid()).Build();
        var origineleId = basis.Id;

        var extended = NewBuilder().With(o => o.Stad, "Utrecht").Extend(basis);

        Assert.Equal(origineleId, extended.Id); // niet opgegeven -> behouden
    }

    [Fact]
    public void Extend_DoesNotChangeBuilderState_BuildBeforeAndAfterMakesNewInstances()
    {
        var builder = NewBuilder().With(o => o.Id, Guid.NewGuid()).With(o => o.KlantNaam, "Bob");

        var first = builder.Build();               // verse instance
        var bestaand = NewBuilder().With(o => o.Id, Guid.NewGuid()).Build();
        var extended = builder.Extend(bestaand);   // tussendoor Extend
        var second = builder.Build();              // weer een verse instance

        Assert.Same(bestaand, extended);
        Assert.NotSame(first, second);             // Build blijft nieuwe instances maken
        Assert.Equal("Bob", second.KlantNaam);     // instellingen intact na Extend
        Assert.Equal("Bob", extended.KlantNaam);   // Extend paste de instellingen ook toe
    }
}
