using Microsoft.Extensions.DependencyInjection;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.UnitTests;

public class FakerDeepPathAndOptionalTests
{
    public class Widget
    {
        public string Name { get; set; } = "";
    }

    public class Inner
    {
        public string Hello() => "hello";
        public string Greeting => "hi-property"; // property, not a method
        public string Echo(string text, string suffix = "!") => text + suffix;
    }

    public class RootFakers : IFaker
    {
        public Inner Sub => new();          // property -> nested object
        public Inner Make() => new();        // parameterless method -> nested object
        public string Greet(string name, string greeting = "Hello") => $"{greeting}, {name}";
    }

    private static IModelBuilderProvider CreateProvider() =>
        new ServiceCollection()
            .AddXModelBuilder()
            .AddFaker<RootFakers>()
            .BuildServiceProvider()
            .GetRequiredService<IModelBuilderProvider>();

    private static string Build(IModelBuilderProvider xmodels, string token) =>
        xmodels.For<Widget>().With("Name", token).Build().Name;

    [Fact]
    public void DeepPath_Property_Then_Method()
    {
        Assert.Equal("hello", Build(CreateProvider(), "Sub.Hello()"));
    }

    [Fact]
    public void DeepPath_IntermediateParameterlessMethod()
    {
        Assert.Equal("hello", Build(CreateProvider(), "Make.Hello()"));
    }

    [Fact]
    public void DeepPath_TerminalProperty_Fallback()
    {
        // "Greeting" is a property on Inner, reached through the method-then-property fallback.
        Assert.Equal("hi-property", Build(CreateProvider(), "Sub.Greeting()"));
    }

    [Fact]
    public void DeepPath_UnknownSegment_Throws()
    {
        Assert.Throws<KeyNotFoundException>(() => Build(CreateProvider(), "Sub.DoesNotExist()"));
    }

    [Fact]
    public void Optional_Omitted_UsesDefault()
    {
        Assert.Equal("Hello, World", Build(CreateProvider(), "Greet(World)"));
    }

    [Fact]
    public void Optional_Provided_Overrides()
    {
        Assert.Equal("Hi, World", Build(CreateProvider(), "Greet(World,Hi)"));
    }

    [Fact]
    public void Optional_OnDeepPathMethod()
    {
        Assert.Equal("a!", Build(CreateProvider(), "Sub.Echo(a)"));
        Assert.Equal("aX", Build(CreateProvider(), "Sub.Echo(a,X)"));
    }
}
