using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.UnitTests;

public class XModelBuilderServiceCollectionExtensionsTests
{
    public class Widget
    {
        public string? Name { get; set; }
    }

    [ModelBuilder("joe")]
    public sealed class WidgetBuilderJoe(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<WidgetBuilderJoe, Widget>(options, xprovider)
    {
        protected override void SetDefaults()
        {
            With(x => x.Name, "John Doe");
        }
    }

    [ModelBuilder("jane")]
    public sealed class WidgetBuilderJane(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<WidgetBuilderJane, Widget>(options, xprovider)
    {
        protected override void SetDefaults()
        {
            With(x => x.Name, "Jane Doe");
        }
    }

    [ModelBuilder("extra")]
    public sealed class WidgetBuilderExtra(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<WidgetBuilderExtra, Widget>(options, xprovider)
    {
        protected override void SetDefaults()
        {
            With(x => x.Name, "Extra");
        }
    }

    // A stand-in for a real cross-cutting layer: sets a value on every Widget build.
    public sealed class NamingDefaults<TModel>(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<NamingDefaults<TModel>, TModel>(options, xprovider)
        where TModel : class
    {
        protected override void SetDefaults()
        {
            if (typeof(TModel) == typeof(Widget))
            {
                With("Name", "Default Layer");
            }
        }
    }

    [Fact]
    public void For_ReturnsBaseLayer_NotASpecificBuilder()
    {
        // Arrange
        var sp = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuildersFromAssembly(typeof(WidgetBuilderJoe).Assembly)
            .BuildServiceProvider();

        var xprovider = sp.GetRequiredService<IModelBuilderProvider>();

        // Act
        var widget = xprovider.For<Widget>().Build();

        // Assert
        // For<T>() is always the base (the do-nothing DefaultModelBuilder) plus the optional
        // cross-cutting layer - never a registered specific builder - so no name is set.
        Assert.Null(widget.Name);
    }

    [Fact]
    public void For_MultipleBuilders_ReturnsBaseLayer_DoesNotThrow()
    {
        // Arrange
        var sp = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuilder<WidgetBuilderJoe>()
            .AddModelBuilder<WidgetBuilderJane>()
            .BuildServiceProvider();

        var xprovider = sp.GetRequiredService<IModelBuilderProvider>();

        // Act
        var widget = xprovider.For<Widget>().Build();

        // Assert
        // Several specific builders no longer make For<T>() ambiguous: it is the base layer.
        Assert.Null(widget.Name);
    }

    [Fact]
    public void For_With_Name_Resolves_Explicitly_Named_Builder()
    {
        // Arrange
        var sp = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuildersFromAssembly(typeof(WidgetBuilderJoe).Assembly)
            .BuildServiceProvider();

        var xprovider = sp.GetRequiredService<IModelBuilderProvider>();

        // Act
        var widget = xprovider.For<Widget>("jane").Build();

        // Assert
        Assert.Equal("Jane Doe", widget.Name);
    }

    [Fact]
    public void Use_Resolves_Specific_Builder_By_Type()
    {
        // Arrange
        var sp = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuildersFromAssembly(typeof(WidgetBuilderJoe).Assembly)
            .BuildServiceProvider();

        var xprovider = sp.GetRequiredService<IModelBuilderProvider>();

        // Act
        var widget = xprovider.Use<WidgetBuilderJoe>().Build();

        // Assert
        Assert.Equal("John Doe", widget.Name);
    }

    [Fact]
    public void For_With_Unknown_Name_Throws()
    {
        // Arrange
        var sp = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuildersFromAssembly(typeof(WidgetBuilderJoe).Assembly)
            .BuildServiceProvider();

        var xprovider = sp.GetRequiredService<IModelBuilderProvider>();

        // Act & Assert
        Assert.Throws<KeyNotFoundException>(() => xprovider.For<Widget>("does-not-exist"));
    }

    [Fact]
    public void AddCrossCuttingModelBuilder_Applies_To_For_And_Underlies_NamedBuilders()
    {
        // Arrange
        var sp = new ServiceCollection()
            .AddXModelBuilder()
            .AddCrossCuttingModelBuilder(typeof(NamingDefaults<>))
            .AddModelBuilder<WidgetBuilderJane>()
            .BuildServiceProvider();

        var xprovider = sp.GetRequiredService<IModelBuilderProvider>();

        // Act
        var fromBaseLayer = xprovider.For<Widget>().Build();
        var fromNamedBuilder = xprovider.For<Widget>("jane").Build();

        // Assert
        // The cross-cutting layer runs for For<T>(); a specific builder layers on top and overrides it.
        Assert.Equal("Default Layer", fromBaseLayer.Name);
        Assert.Equal("Jane Doe", fromNamedBuilder.Name);
    }

    [Fact]
    public void ForEmpty_Bypasses_The_CrossCutting_Layer()
    {
        // Arrange
        var sp = new ServiceCollection()
            .AddXModelBuilder()
            .AddCrossCuttingModelBuilder(typeof(NamingDefaults<>))
            .BuildServiceProvider();

        var xprovider = sp.GetRequiredService<IModelBuilderProvider>();

        // Act
        var widget = xprovider.ForEmpty<Widget>().Build();

        // Assert
        // ForEmpty skips the always-applied cross-cutting layer, yielding the pristine base.
        Assert.Null(widget.Name);
    }

    [Fact]
    public void Validate_Passes_When_NamesUnique_MultipleBuilders_NoDefaultNeeded()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuilder<WidgetBuilderJoe>()
            .AddModelBuilder<WidgetBuilderJane>();

        // Act & Assert
        // Multiple specific builders are fine as long as their names are unique; there is no
        // "default among them" to configure.
        services.ValidateXModelBuilderRegistrations();
    }

    [Fact]
    public void Validate_SingleBuilder_Ok()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuilder<WidgetBuilderJoe>();

        // Act & Assert
        // A single named builder needs no additional configuration.
        services.ValidateXModelBuilderRegistrations();
    }
}
