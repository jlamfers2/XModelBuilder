using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.UnitTests;

public class XModelBuilderServiceCollectionExtensionsTests
{
    public class Widget
    {
        public string Name { get; set; } = null!;
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

    [Fact]
    public void For_Resolves_ConfiguredDefault_RegardlessOfRegistrationOrder()
    {
        // Arrange
        var sp = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuildersFromAssembly(typeof(WidgetBuilderJoe).Assembly)
            .UseAsDefaultModelBuilder<WidgetBuilderJoe>()
            .BuildServiceProvider();

        var xprovider = sp.GetRequiredService<IModelBuilderProvider>();

        // Act
        var builder = xprovider.For<Widget>();

        // Assert
        Assert.IsType<WidgetBuilderJoe>(builder);
        Assert.Equal("John Doe", builder.Build().Name);
    }

    [Fact]
    public void For_MultipleBuilders_NoDefaultConfigured_Throws()
    {
        // Arrange
        var sp = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuilder<WidgetBuilderJoe>()
            .AddModelBuilder<WidgetBuilderJane>()
            .BuildServiceProvider();

        var xprovider = sp.GetRequiredService<IModelBuilderProvider>();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => xprovider.For<Widget>());
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
    public void Validate_Passes_When_NamesUnique_And_DefaultConfigured()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuilder<WidgetBuilderJoe>()
            .AddModelBuilder<WidgetBuilderJane>()
            .UseAsDefaultModelBuilder<WidgetBuilderJoe>();

        // Act & Assert
        // Does not throw.
        services.ValidateXModelBuilderRegistrations();
    }

    [Fact]
    public void Validate_Throws_When_MultipleBuilders_And_No_Default()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuilder<WidgetBuilderJoe>()
            .AddModelBuilder<WidgetBuilderJane>();

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => services.ValidateXModelBuilderRegistrations());

        // Assert
        Assert.Contains("no default is configured", ex.Message);
    }

    [Fact]
    public void Validate_SingleBuilder_NeedsNoDefault()
    {
        // Arrange
        var services = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuilder<WidgetBuilderJoe>();

        // Act & Assert
        // A single builder is unambiguously the default; no configuration required.
        services.ValidateXModelBuilderRegistrations();
    }
}
