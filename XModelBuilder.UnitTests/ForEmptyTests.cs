using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.UnitTests;

public class ForEmptyTests
{
    public class Gadget
    {
        public string Name { get; set; } = "";
        public string Color { get; set; } = "";
    }

    // The cross-cutting layer: a default that For<T>() applies and ForEmpty must NOT.
    public sealed class GadgetDefaults<TModel>(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<GadgetDefaults<TModel>, TModel>(options, xprovider)
        where TModel : class
    {
        protected override void SetDefaults()
        {
            if (typeof(TModel) == typeof(Gadget))
            {
                With("Color", "red");
            }
        }
    }

    private static IModelBuilderProvider CreateProvider() =>
        new ServiceCollection()
            .AddXModelBuilder()
            .AddCrossCuttingModelBuilder(typeof(GadgetDefaults<>))
            .BuildServiceProvider()
            .GetRequiredService<IModelBuilderProvider>();

    [Fact]
    public void ForEmpty_BypassesTheCrossCuttingLayer_AndAppliesOnlyGivenValues()
    {
        // Arrange
        var xprovider = CreateProvider();

        // Act
        var viaFor = xprovider.For<Gadget>().With(g => g.Name, "a").Build();        // base + cross-cutting layer
        var viaEmpty = xprovider.ForEmpty<Gadget>().With(g => g.Name, "a").Build(); // bare base -> no defaults

        // Assert
        Assert.Equal("red", viaFor.Color); // the cross-cutting layer applied its default
        Assert.Equal("", viaEmpty.Color);  // ForEmpty applied ONLY the given value; no layer ran
        Assert.Equal("a", viaEmpty.Name);
    }

    [Fact]
    public void ForEmpty_ReturnsAFreshBuilderEachCall()
    {
        // Arrange
        var xprovider = CreateProvider();

        // Act
        var first = xprovider.ForEmpty<Gadget>();
        var second = xprovider.ForEmpty<Gadget>();

        // Assert
        Assert.NotSame(first, second);
    }
}
