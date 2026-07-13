using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XModelBuilder.Default;

namespace XModelBuilder.UnitTests;

public class DefaultModelBuilderProviderTests
{
    public class Gadget
    {
        public string Name { get; set; } = null!;
    }

    [ModelBuilder("alpha")]
    public sealed class GadgetBuilderAlpha(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<GadgetBuilderAlpha, Gadget>(options, xprovider)
    {
        protected override void SetDefaults()
        {
            With(x => x.Name, "Alpha");
        }
    }

    [ModelBuilder("beta")]
    public sealed class GadgetBuilderBeta(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<GadgetBuilderBeta, Gadget>(options, xprovider)
    {
        protected override void SetDefaults()
        {
            With(x => x.Name, "Beta");
        }
    }

    [ModelBuilder("gamma")]
    public sealed class GadgetBuilderGamma(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<GadgetBuilderGamma, Gadget>(options, xprovider)
    {
        protected override void SetDefaults()
        {
            With(x => x.Name, "Gamma");
        }
    }

    public class MultiBuilderGadget
    {
        public string Name { get; set; } = null!;
    }

    [ModelBuilder("first")]
    public sealed class FirstGadgetBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<FirstGadgetBuilder, MultiBuilderGadget>(options, xprovider)
    {
        protected override void SetDefaults()
        {
            With(x => x.Name, "First");
        }
    }

    [ModelBuilder("second")]
    public sealed class SecondGadgetBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<SecondGadgetBuilder, MultiBuilderGadget>(options, xprovider)
    {
        protected override void SetDefaults()
        {
            With(x => x.Name, "Second");
        }
    }

    [Fact]
    public void For_ReturnsBaseLayer_NotASpecificBuilder_RegardlessOfRegistrationOrder()
    {
        // Arrange
        DefaultModelBuilderProvider.Current
            .AddModelBuilder<GadgetBuilderBeta>()
            .AddModelBuilder<GadgetBuilderGamma>()
            .AddModelBuilder<GadgetBuilderAlpha>();

        // Act
        var gadget = DefaultModelBuilderProvider.Current.For<Gadget>().Build();

        // Assert
        // For<T>() is always the (do-nothing) base here, never a registered specific builder.
        Assert.Null(gadget.Name);
    }

    [Fact]
    public void For_MultipleBuilders_ReturnsBaseLayer_DoesNotThrow()
    {
        // Arrange
        DefaultModelBuilderProvider.Current
            .AddModelBuilder<FirstGadgetBuilder>()
            .AddModelBuilder<SecondGadgetBuilder>();

        // Act
        var gadget = DefaultModelBuilderProvider.Current.For<MultiBuilderGadget>().Build();

        // Assert
        // Several specific builders no longer make For<T>() ambiguous.
        Assert.Null(gadget.Name);
    }

    [Fact]
    public void For_With_Name_Resolves_Explicitly_Named_Builder()
    {
        // Arrange
        DefaultModelBuilderProvider.Current
            .AddModelBuilder<GadgetBuilderAlpha>()
            .AddModelBuilder<GadgetBuilderBeta>();

        // Act
        var gadget = DefaultModelBuilderProvider.Current.For<Gadget>("beta").Build();

        // Assert
        Assert.Equal("Beta", gadget.Name);
    }

    [Fact]
    public void For_With_Name_Resolves_Explicitly_Typed_Builder()
    {
        // Arrange
        DefaultModelBuilderProvider.Current
            .AddModelBuilder<GadgetBuilderAlpha>()
            .AddModelBuilder<GadgetBuilderBeta>();

        // Act
        var gadget = DefaultModelBuilderProvider.Current.Use<GadgetBuilderBeta>().Build();

        // Assert
        Assert.Equal("Beta", gadget.Name);
    }


    [Fact]
    public void For_With_Unknown_Name_Throws()
    {
        // Arrange
        DefaultModelBuilderProvider.Current.AddModelBuilder<GadgetBuilderAlpha>();

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(
            () => DefaultModelBuilderProvider.Current.For<Gadget>("does-not-exist"));
    }

    // A model type with NO registered builder (resolved via the open-generic fallback), so these
    // faker tests don't depend on a default being configured for the multi-builder Gadget.
    public class FakerGadget
    {
        public string Name { get; set; } = null!;
    }

    public class GadgetFakers : IFaker
    {
        public string RandomGadgetName() => "RandomGadget";
    }

    public class GadgetMarkerService
    {
        public string Value => "gadget-marker";
    }

    public class InjectedDependencyFakers(GadgetMarkerService service) : IFaker
    {
        public string FromService() => service.Value;
    }

    [Fact]
    public void AddFaker_Instance_IsUsableViaToken()
    {
        // Arrange
        DefaultModelBuilderProvider.Current.AddFaker(new GadgetFakers());

        // Act
        var gadget = DefaultModelBuilderProvider.Current.For<FakerGadget>().With("Name", "RandomGadgetName()").Build();

        // Assert
        Assert.Equal("RandomGadget", gadget.Name);
    }

    [Fact]
    public void AddFaker_Type_LetsContainerInjectItsOwnDependencies()
    {
        // Arrange
        DefaultModelBuilderProvider.Current
            .AddServices(s => s.AddSingleton<GadgetMarkerService>())
            .AddFaker<InjectedDependencyFakers>();

        // Act
        var gadget = DefaultModelBuilderProvider.Current.For<FakerGadget>().With("Name", "FromService()").Build();

        // Assert
        Assert.Equal("gadget-marker", gadget.Name);
    }

    [Fact]
    public void Faker_ResolvesTypedFakerDirectly()
    {
        // Arrange
        DefaultModelBuilderProvider.Current.AddFaker(new GadgetFakers());

        // Act
        var faker = DefaultModelBuilderProvider.Current.Faker<GadgetFakers>();

        // Assert
        Assert.Equal("RandomGadget", faker.RandomGadgetName());
    }

    [Fact]
    public void UseFaker_StaticFacade_ResolvesSameAsProvider()
    {
        // Arrange
        DefaultModelBuilderProvider.Current.AddFaker(new GadgetFakers());

        // Act
        var faker = Use.Faker<GadgetFakers>();

        // Assert
        Assert.Equal("RandomGadget", faker.RandomGadgetName());
    }

    public class CultureGadget
    {
        public decimal Price { get; set; }
    }

    [Fact]
    public void AddOptions_Configure_TakesEffect()
    {
        // Arrange
        // nl-NL uses ',' as the decimal separator - InvariantCulture (the default) would throw a
        // FormatException trying to parse this as a decimal. This used to be silently ignored due
        // to a bug where the configure delegate was applied to the options instance about to be
        // discarded, instead of the one that became live.
        DefaultModelBuilderProvider.Current.AddOptions(o => o.DefaultCulture = System.Globalization.CultureInfo.GetCultureInfo("nl-NL"));

        // Act
        var gadget = DefaultModelBuilderProvider.Current.For<CultureGadget>().With("Price", "1234,56").Build();

        // Assert
        Assert.Equal(1234.56m, gadget.Price);
    }
}
