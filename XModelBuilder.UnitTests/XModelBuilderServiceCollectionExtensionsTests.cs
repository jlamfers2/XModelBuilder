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
    public sealed class WidgetBuilderJoe(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels)
        : ModelBuilder<WidgetBuilderJoe, Widget>(options, xmodels)
    {
        protected override void SetDefaults()
        {
            With(x => x.Name, "John Doe");
        }
    }

    [ModelBuilder("jane")]
    public sealed class WidgetBuilderJane(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels)
        : ModelBuilder<WidgetBuilderJane, Widget>(options, xmodels)
    {
        protected override void SetDefaults()
        {
            With(x => x.Name, "Jane Doe");
        }
    }

    [ModelBuilder("extra")]
    public sealed class WidgetBuilderExtra(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels)
        : ModelBuilder<WidgetBuilderExtra, Widget>(options, xmodels)
    {
        protected override void SetDefaults()
        {
            With(x => x.Name, "Extra");
        }
    }

    [Fact]
    public void For_Resolves_ConfiguredDefault_RegardlessOfRegistrationOrder()
    {
        var sp = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuildersFromAssembly(typeof(WidgetBuilderJoe).Assembly)
            .UseAsDefaultModelBuilder<WidgetBuilderJoe>()
            .BuildServiceProvider();

        var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

        var builder = xmodels.For<Widget>();

        Assert.IsType<WidgetBuilderJoe>(builder);
        Assert.Equal("John Doe", builder.Build().Name);
    }

    [Fact]
    public void For_MultipleBuilders_NoDefaultConfigured_Throws()
    {
        var sp = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuilder<WidgetBuilderJoe>()
            .AddModelBuilder<WidgetBuilderJane>()
            .BuildServiceProvider();

        var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

        Assert.Throws<InvalidOperationException>(() => xmodels.For<Widget>());
    }

    [Fact]
    public void For_With_Name_Resolves_Explicitly_Named_Builder()
    {
        var sp = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuildersFromAssembly(typeof(WidgetBuilderJoe).Assembly)
            .BuildServiceProvider();

        var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

        var widget = xmodels.For<Widget>("jane").Build();

        Assert.Equal("Jane Doe", widget.Name);
    }

    [Fact]
    public void For_With_Unknown_Name_Throws()
    {
        var sp = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuildersFromAssembly(typeof(WidgetBuilderJoe).Assembly)
            .BuildServiceProvider();

        var xmodels = sp.GetRequiredService<IModelBuilderProvider>();

        Assert.Throws<KeyNotFoundException>(() => xmodels.For<Widget>("does-not-exist"));
    }

    [Fact]
    public void Validate_Passes_When_NamesUnique_And_DefaultConfigured()
    {
        var services = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuilder<WidgetBuilderJoe>()
            .AddModelBuilder<WidgetBuilderJane>()
            .UseAsDefaultModelBuilder<WidgetBuilderJoe>();

        // Does not throw.
        services.ValidateXModelBuilderRegistrations();
    }

    [Fact]
    public void Validate_Throws_When_MultipleBuilders_And_No_Default()
    {
        var services = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuilder<WidgetBuilderJoe>()
            .AddModelBuilder<WidgetBuilderJane>();

        var ex = Assert.Throws<InvalidOperationException>(() => services.ValidateXModelBuilderRegistrations());
        Assert.Contains("no default is configured", ex.Message);
    }

    [Fact]
    public void Validate_SingleBuilder_NeedsNoDefault()
    {
        var services = new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuilder<WidgetBuilderJoe>();

        // A single builder is unambiguously the default; no configuration required.
        services.ValidateXModelBuilderRegistrations();
    }
}
