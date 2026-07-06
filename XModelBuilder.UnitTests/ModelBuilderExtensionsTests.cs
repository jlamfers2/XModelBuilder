using Microsoft.Extensions.DependencyInjection;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.UnitTests;

public class ModelBuilderExtensionsTests
{
    public sealed class Item
    {
        public string Name { get; set; } = string.Empty;
        public int Index { get; set; }
    }

    private static IModelBuilderProvider CreateProvider() =>
        new ServiceCollection()
            .AddXModelBuilder()
            .BuildServiceProvider()
            .GetRequiredService<IModelBuilderProvider>();

    [Fact]
    public void BuildMany_WithPerIndexConfigure_varies_per_instance_and_keeps_base_config()
    {
        // Arrange
        var xprovider = CreateProvider();
        var builder = xprovider.For<Item>().With(x => x.Name, "base");

        // Act
        var items = builder.BuildMany(3, (b, i) => b.With(x => x.Index, i));

        // Assert
        Assert.Equal(3, items.Count);
        Assert.Equal([0, 1, 2], items.Select(x => x.Index).ToArray());
        Assert.All(items, x => Assert.Equal("base", x.Name)); // base configuration shared across all
    }

    [Fact]
    public void BuildMany_WithPerIndexConfigure_count_zero_returns_empty()
    {
        // Arrange
        var xprovider = CreateProvider();

        // Act
        var items = xprovider.For<Item>().BuildMany(0, (b, _) => b);

        // Assert
        Assert.Empty(items);
    }

    [Fact]
    public void BuildMany_WithPerIndexConfigure_null_configure_throws()
    {
        // Arrange
        var builder = CreateProvider().For<Item>();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => builder.BuildMany(3, null!));
    }

    [Fact]
    public void BuildMany_WithPerIndexConfigure_negative_count_throws()
    {
        // Arrange
        var builder = CreateProvider().For<Item>();

        // Act & Assert
        Assert.Throws<ArgumentOutOfRangeException>(() => builder.BuildMany(-1, (b, _) => b));
    }

    [Fact]
    public void BuildMany_count_overload_still_builds_requested_count()
    {
        // Arrange
        var xprovider = CreateProvider();

        // Act
        var items = xprovider.For<Item>().With(x => x.Name, "x").BuildMany(2);

        // Assert
        Assert.Equal(2, items.Count);
        Assert.All(items, x => Assert.Equal("x", x.Name));
    }
}
