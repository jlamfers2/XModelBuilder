using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.UnitTests;

// Covers the PROVIDER-level BuildMany overloads (ModelBuilderProviderExtensions): each instance is
// built through its OWN fresh builder (a fresh For<T>()), as opposed to the builder-level BuildMany
// (ModelBuilderExtensions) which reuses one configured builder.
public class ProviderBuildManyTests
{
    public sealed class Widget
    {
        public int Index { get; set; }
        public string Tag { get; set; } = "default-tag";
    }

    [ModelBuilder("tagged")]
    public sealed class TaggedWidgetBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<TaggedWidgetBuilder, Widget>(options, xprovider)
    {
        protected override void SetDefaults() => With(x => x.Tag, "tagged");
    }

    private static IModelBuilderProvider CreateProvider() =>
        new ServiceCollection().AddXModelBuilder().BuildServiceProvider().GetRequiredService<IModelBuilderProvider>();

    private static IModelBuilderProvider CreateProviderWithNamedBuilder() =>
        new ServiceCollection().AddXModelBuilder().AddModelBuilder<TaggedWidgetBuilder>()
            .BuildServiceProvider().GetRequiredService<IModelBuilderProvider>();

    [Fact]
    public void BuildMany_count_builds_requested_number_of_independent_instances()
    {
        // Arrange
        var xprovider = CreateProvider();

        // Act
        var widgets = xprovider.BuildMany<Widget>(3);

        // Assert
        Assert.Equal(3, widgets.Count);
        Assert.Equal(3, widgets.Distinct().Count()); // each a fresh, distinct instance
        Assert.All(widgets, w => Assert.Equal("default-tag", w.Tag)); // fallback builder applies no defaults
    }

    [Fact]
    public void BuildMany_count_with_perIndex_configure_varies_by_index_on_fresh_builders()
    {
        // Arrange
        var xprovider = CreateProvider();

        // Act
        var widgets = xprovider.BuildMany<Widget>(3, (b, i) => b.With(x => x.Index, i));

        // Assert
        Assert.Equal([0, 1, 2], widgets.Select(w => w.Index).ToArray());
    }

    [Fact]
    public void BuildMany_count_name_resolves_the_named_builder_for_every_instance()
    {
        // Arrange
        var xprovider = CreateProviderWithNamedBuilder();

        // Act
        var widgets = xprovider.BuildMany<Widget>(2, "tagged");

        // Assert
        Assert.Equal(2, widgets.Count);
        Assert.All(widgets, w => Assert.Equal("tagged", w.Tag)); // named builder's SetDefaults applied each time
    }

    [Fact]
    public void BuildMany_count_name_configure_combines_named_builder_with_perIndex_config()
    {
        // Arrange
        var xprovider = CreateProviderWithNamedBuilder();

        // Act
        var widgets = xprovider.BuildMany<Widget>(2, "tagged", (b, i) => b.With(x => x.Index, i));

        // Assert
        Assert.Equal([0, 1], widgets.Select(w => w.Index).ToArray());
        Assert.All(widgets, w => Assert.Equal("tagged", w.Tag));
    }

    [Fact]
    public void BuildMany_count_zero_returns_empty_for_all_overloads()
    {
        // Arrange
        var xprovider = CreateProviderWithNamedBuilder();

        // Act & Assert
        Assert.Empty(xprovider.BuildMany<Widget>(0));
        Assert.Empty(xprovider.BuildMany<Widget>(0, (b, _) => b));
        Assert.Empty(xprovider.BuildMany<Widget>(0, "tagged"));
        Assert.Empty(xprovider.BuildMany<Widget>(0, "tagged", (b, _) => b));
    }

    [Fact]
    public void BuildMany_negative_count_throws_ArgumentOutOfRange()
    {
        // Arrange
        var xprovider = CreateProvider();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => xprovider.BuildMany<Widget>(-1, (b, _) => b));
    }

    [Fact]
    public void BuildMany_null_configure_throws_ArgumentNull()
    {
        // Arrange
        var xprovider = CreateProvider();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => xprovider.BuildMany<Widget>(2, (Func<IModelBuilder<Widget>, int, IModelBuilder<Widget>>)null!));
    }

    [Fact]
    public void BuildMany_null_provider_throws_ArgumentNull()
    {
        // Arrange
        IModelBuilderProvider xprovider = null!;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => xprovider.BuildMany<Widget>(2));
        Assert.Throws<ArgumentNullException>(() => xprovider.BuildMany<Widget>(2, "tagged"));
    }
}
