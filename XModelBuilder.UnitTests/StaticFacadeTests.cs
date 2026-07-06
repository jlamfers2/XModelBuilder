using Microsoft.Extensions.Options;
using XModelBuilder.Default;

namespace XModelBuilder.UnitTests;

// Covers the static convenience facades (Create / For / Use) over the standalone
// DefaultModelBuilderProvider.Current. Each concern uses its OWN model/builder types so registrations
// on the process-wide singleton cannot collide across tests.
public class StaticFacadeTests
{
    // No builder is ever registered for this type, so it always resolves via the open-generic fallback.
    public sealed class FallbackModel
    {
        public int Index { get; set; }
        public string Name { get; set; } = "fallback";
    }

    public sealed class NamedModel
    {
        public int Index { get; set; }
        public string Name { get; set; } = "";
    }

    [ModelBuilder("facade-named")]
    public sealed class NamedModelBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<NamedModelBuilder, NamedModel>(options, xprovider)
    {
        protected override void SetDefaults() => With(x => x.Name, "named");
    }

    public sealed class DirectModel
    {
        public string Name { get; set; } = "";
    }

    [ModelBuilder("facade-direct")]
    public sealed class DirectModelBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<DirectModelBuilder, DirectModel>(options, xprovider)
    {
        protected override void SetDefaults() => With(x => x.Name, "direct");
    }

    [Fact]
    public void Create_Model_builds_a_single_instance_via_the_fallback_builder()
    {
        // Arrange & Act
        var model = Create.Model<FallbackModel>();

        // Assert
        Assert.Equal("fallback", model.Name);
    }

    [Fact]
    public void Create_Models_count_builds_the_requested_number()
    {
        // Arrange & Act
        var models = Create.Models<FallbackModel>(3);

        // Assert
        Assert.Equal(3, models.Count);
        Assert.Equal(3, models.Distinct().Count());
    }

    [Fact]
    public void Create_Models_count_configure_applies_per_index_configuration()
    {
        // Arrange & Act
        var models = Create.Models<FallbackModel>(3, (b, i) => b.With(x => x.Index, i));

        // Assert
        Assert.Equal([0, 1, 2], models.Select(m => m.Index).ToArray());
    }

    [Fact]
    public void Create_Models_count_name_builds_via_the_named_builder()
    {
        // Arrange
        DefaultModelBuilderProvider.Current.AddModelBuilder<NamedModelBuilder>();

        // Act
        var models = Create.Models<NamedModel>(2, "facade-named");

        // Assert
        Assert.Equal(2, models.Count);
        Assert.All(models, m => Assert.Equal("named", m.Name));
    }

    [Fact]
    public void Create_Models_count_name_configure_combines_named_builder_and_per_index_config()
    {
        // Arrange
        DefaultModelBuilderProvider.Current.AddModelBuilder<NamedModelBuilder>();

        // Act
        var models = Create.Models<NamedModel>(2, "facade-named", (b, i) => b.With(x => x.Index, i));

        // Assert
        Assert.Equal([0, 1], models.Select(m => m.Index).ToArray());
        Assert.All(models, m => Assert.Equal("named", m.Name));
    }

    [Fact]
    public void For_Model_returns_a_fluent_builder_that_honors_With()
    {
        // Arrange & Act
        var model = For.Model<FallbackModel>().With(x => x.Name, "custom").Build();

        // Assert
        Assert.Equal("custom", model.Name);
    }

    [Fact]
    public void Use_Builder_resolves_a_compile_time_known_builder_directly()
    {
        // Arrange
        DefaultModelBuilderProvider.Current.AddModelBuilder<DirectModelBuilder>();

        // Act
        var model = Use.Builder<DirectModelBuilder>().Build();

        // Assert
        Assert.Equal("direct", model.Name);
    }
}
