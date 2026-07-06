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

    [ModelBuilder("gadget")]
    public sealed class GadgetBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<GadgetBuilder, Gadget>(options, xprovider)
    {
        // A default that ForEmpty must NOT apply (it bypasses this builder entirely).
        protected override void SetDefaults() => With(g => g.Color, "red");
    }

    private static IModelBuilderProvider CreateProvider() =>
        new ServiceCollection()
            .AddXModelBuilder()
            .AddModelBuilder<GadgetBuilder>()
            .BuildServiceProvider()
            .GetRequiredService<IModelBuilderProvider>();

    [Fact]
    public void ForEmpty_BypassesRegisteredCustomBuilder_AndAppliesOnlyGivenValues()
    {
        // Arrange
        var xprovider = CreateProvider();

        // Act
        var viaFor = xprovider.For<Gadget>().With(g => g.Name, "a").Build();        // custom builder -> SetDefaults runs
        var viaEmpty = xprovider.ForEmpty<Gadget>().With(g => g.Name, "a").Build(); // bare builder -> no defaults

        // Assert
        Assert.Equal("red", viaFor.Color); // custom builder applied its default
        Assert.Equal("", viaEmpty.Color);  // ForEmpty applied ONLY the given value; no default ran
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
