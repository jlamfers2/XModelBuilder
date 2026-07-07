using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.UnitTests;

public class BuildRecursionGuardTests
{
    // ---- Models ---------------------------------------------------------------------------------

    // Direct self-reference: a Vader has a Kind, a Kind has a Vader (Vader -> Kind -> Vader).
    public class Vader { public Kind? Kind { get; set; } }
    public class Kind { public Vader? Vader { get; set; } }

    // A type whose member is itself (used across the different triggering mechanisms).
    public class Node { public string? Name { get; set; } public Node? Next { get; set; } }
    public class TokenNode { public TokenNode? Next { get; set; } }
    public class NamedRefNode { public NamedRefNode? Next { get; set; } }

    // Non-cyclic graph with two same-typed siblings (must NOT be seen as a cycle).
    public class Leaf { public string? Value { get; set; } }
    public class Holder { public Leaf? A { get; set; } public Leaf? B { get; set; } }

    // ---- Builders -------------------------------------------------------------------------------

    [ModelBuilder("vader")]
    public sealed class VaderBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<VaderBuilder, Vader>(options, xprovider)
    {
        protected override void SetDefaults() => WithDefault(v => v.Kind);
    }

    [ModelBuilder("kind")]
    public sealed class KindBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<KindBuilder, Kind>(options, xprovider)
    {
        protected override void SetDefaults() => WithDefault(k => k.Vader);
    }

    // Cycle via WithDefault on the same type.
    [ModelBuilder("node")]
    public sealed class NodeWithDefaultBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<NodeWithDefaultBuilder, Node>(options, xprovider)
    {
        protected override void SetDefaults() => WithDefault(n => n.Next);
    }

    // Cycle via WithBuilder referencing its own registered name.
    [ModelBuilder("nodeWb")]
    public sealed class NodeWithBuilderBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<NodeWithBuilderBuilder, Node>(options, xprovider)
    {
        protected override void SetDefaults() => WithBuilder(n => n.Next, "nodeWb");
    }

    // Cycle via the "default()" string token.
    [ModelBuilder("tokenNode")]
    public sealed class TokenNodeBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<TokenNodeBuilder, TokenNode>(options, xprovider)
    {
        protected override void SetDefaults() => With("Next", "default()");
    }

    // Cycle via a named-builder string reference to its own name.
    [ModelBuilder("namedRefNode")]
    public sealed class NamedRefNodeBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<NamedRefNodeBuilder, NamedRefNode>(options, xprovider)
    {
        protected override void SetDefaults() => With("Next", "namedRefNode");
    }

    [ModelBuilder("leaf")]
    public sealed class LeafBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<LeafBuilder, Leaf>(options, xprovider)
    {
        protected override void SetDefaults() => With(l => l.Value, "leaf");
    }

    [ModelBuilder("holder")]
    public sealed class HolderBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<HolderBuilder, Holder>(options, xprovider)
    {
        protected override void SetDefaults()
        {
            WithDefault(h => h.A);
            WithDefault(h => h.B);
        }
    }

    // ---- Helpers --------------------------------------------------------------------------------

    private static IModelBuilderProvider Provider(params Type[] builderTypes)
    {
        var services = new ServiceCollection().AddXModelBuilder();
        foreach (var builderType in builderTypes)
        {
            services.AddModelBuilder(builderType);
        }
        return services.BuildServiceProvider().GetRequiredService<IModelBuilderProvider>();
    }

    // ---- Tests ----------------------------------------------------------------------------------

    [Fact]
    public void Mutual_Cycle_Vader_Kind_Vader_Throws_Instead_Of_StackOverflow()
    {
        // Arrange
        var xprovider = Provider(typeof(VaderBuilder), typeof(KindBuilder));

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => xprovider.For<Vader>().Build());

        // Assert
        Assert.Contains("Cyclic model build detected", ex.Message);
        Assert.Contains("Vader", ex.Message);
        Assert.Contains("Kind", ex.Message);
    }

    [Fact]
    public void Self_Cycle_Via_WithDefault_Throws()
    {
        // Arrange
        var xprovider = Provider(typeof(NodeWithDefaultBuilder));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => xprovider.For<Node>().Build());
    }

    [Fact]
    public void Self_Cycle_Via_WithBuilder_Throws()
    {
        // Arrange
        var xprovider = Provider(typeof(NodeWithBuilderBuilder));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => xprovider.For<Node>("nodeWb").Build());
    }

    [Fact]
    public void Self_Cycle_Via_Default_Token_Throws()
    {
        // Arrange
        var xprovider = Provider(typeof(TokenNodeBuilder));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => xprovider.For<TokenNode>().Build());
    }

    [Fact]
    public void Self_Cycle_Via_Named_Builder_Reference_Throws()
    {
        // Arrange
        var xprovider = Provider(typeof(NamedRefNodeBuilder));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => xprovider.For<NamedRefNode>().Build());
    }

    [Fact]
    public void Two_SameTyped_Siblings_Are_Not_A_Cycle()
    {
        // Arrange
        var xprovider = Provider(typeof(HolderBuilder), typeof(LeafBuilder));

        // Act
        var holder = xprovider.For<Holder>().Build();

        // Assert
        Assert.Equal("leaf", holder.A!.Value);
        Assert.Equal("leaf", holder.B!.Value);
    }

    [Fact]
    public void Build_Chain_Recovers_After_A_Cyclic_Build_Throws()
    {
        // Arrange
        var xprovider = Provider(typeof(NodeWithDefaultBuilder), typeof(LeafBuilder), typeof(HolderBuilder));

        // Act
        Assert.Throws<InvalidOperationException>(() => xprovider.For<Node>().Build());
        var holder = xprovider.For<Holder>().Build(); // guard state must be clean again on this thread

        // Assert
        Assert.NotNull(holder.A);
        Assert.Equal("leaf", holder.A!.Value);
    }

    [Fact]
    public void BuildMany_On_Provider_Builds_Each_With_Nested_WithDefault_No_False_Cycle()
    {
        // Arrange
        var xprovider = Provider(typeof(HolderBuilder), typeof(LeafBuilder));

        // Act
        var holders = xprovider.BuildMany<Holder>(3);

        // Assert
        Assert.Equal(3, holders.Count);
        Assert.All(holders, h => Assert.Equal("leaf", h.A!.Value));
        Assert.All(holders, h => Assert.Equal("leaf", h.B!.Value));
    }

    [Fact]
    public void BuildMany_On_Builder_Repeats_Same_Type_Without_False_Cycle()
    {
        // Arrange
        var xprovider = Provider(typeof(HolderBuilder), typeof(LeafBuilder));

        // Act
        var holders = xprovider.For<Holder>().BuildMany(3);

        // Assert
        Assert.Equal(3, holders.Count);
        Assert.All(holders, h => Assert.NotNull(h.A));
    }

    [Fact]
    public void BuildMany_Of_A_Cyclic_Type_Throws_Instead_Of_StackOverflow()
    {
        // Arrange
        var xprovider = Provider(typeof(NodeWithDefaultBuilder));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => xprovider.BuildMany<Node>(2));
    }
}
