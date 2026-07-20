using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.UnitTests;

public class BuildRecursionGuardTests
{
    // ---- Models ---------------------------------------------------------------------------------

    // Direct self-reference: a Father has a Child, a Child has a Father (Father -> Child -> Father).
    public class Father { public Child? Child { get; set; } }
    public class Child { public Father? Father { get; set; } }

    // A type whose member is itself (used across the different triggering mechanisms).
    public class Node { public string? Name { get; set; } public Node? Next { get; set; } }
    public class TokenNode { public TokenNode? Next { get; set; } }
    public class NamedRefNode { public NamedRefNode? Next { get; set; } }

    // Non-cyclic graph with two same-typed siblings (must NOT be seen as a cycle).
    public class Leaf { public string? Value { get; set; } }
    public class Holder { public Leaf? A { get; set; } public Leaf? B { get; set; } }

    // ---- Cross-cutting layer --------------------------------------------------------------------------

    // The always-applied cross-cutting layer carries the per-type defaults that For<T>() (no name) triggers:
    // the self-/mutual-referencing graphs used to prove the reentrancy guard, and the non-cyclic
    // Holder/Leaf graph used to prove same-typed siblings are NOT a false cycle. Every mechanism
    // (default() token, WithDefault, WithBuilder, named reference) funnels through a builder's Build(),
    // so the single guard covers them all; here the nested resolution is via the "default()" token,
    // which resolves through the cross-cutting layer (chapter 5).
    public sealed class GraphDefaults<TModel>(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<GraphDefaults<TModel>, TModel>(options, xprovider)
        where TModel : class
    {
        protected override void SetDefaults()
        {
            var t = typeof(TModel);
            if (t == typeof(Father)) With("Child", "default()");
            else if (t == typeof(Child)) With("Father", "default()");
            else if (t == typeof(Node)) With("Next", "default()");
            else if (t == typeof(TokenNode)) With("Next", "default()");
            else if (t == typeof(Leaf)) With("Value", "leaf");
            else if (t == typeof(Holder))
            {
                With("A", "default()");
                With("B", "default()");
            }
        }
    }

    // ---- Named builders (for the name-addressed cycle mechanisms) -------------------------------

    // Cycle via WithBuilder referencing its own registered name.
    [ModelBuilder("nodeWb")]
    public sealed class NodeWithBuilderBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<NodeWithBuilderBuilder, Node>(options, xprovider)
    {
        protected override void SetDefaults() => WithBuilder(n => n.Next, "nodeWb");
    }

    // Cycle via a named-builder string reference to its own name.
    [ModelBuilder("namedRefNode")]
    public sealed class NamedRefNodeBuilder(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider)
        : ModelBuilder<NamedRefNodeBuilder, NamedRefNode>(options, xprovider)
    {
        protected override void SetDefaults() => With("Next", "namedRefNode");
    }

    // ---- Helpers --------------------------------------------------------------------------------

    private static IModelBuilderProvider Provider(params Type[] builderTypes)
    {
        var services = new ServiceCollection()
            .AddXModelBuilder()
            .AddCrossCuttingModelBuilder(typeof(GraphDefaults<>));
        foreach (var builderType in builderTypes)
        {
            services.AddModelBuilder(builderType);
        }
        return services.BuildServiceProvider().GetRequiredService<IModelBuilderProvider>();
    }

    // ---- Tests ----------------------------------------------------------------------------------

    [Fact]
    public void Mutual_Cycle_Father_Child_Father_Throws_Instead_Of_StackOverflow()
    {
        // Arrange
        var xprovider = Provider();

        // Act
        var ex = Assert.Throws<InvalidOperationException>(() => xprovider.For<Father>().Build());

        // Assert
        Assert.Contains("Cyclic model build detected", ex.Message);
        Assert.Contains("Father", ex.Message);
        Assert.Contains("Child", ex.Message);
    }

    [Fact]
    public void Self_Cycle_Via_Default_Layer_Throws()
    {
        // Arrange
        var xprovider = Provider();

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
        var xprovider = Provider();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => xprovider.For<TokenNode>().Build());
    }

    [Fact]
    public void Self_Cycle_Via_Named_Builder_Reference_Throws()
    {
        // Arrange
        var xprovider = Provider(typeof(NamedRefNodeBuilder));

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => xprovider.For<NamedRefNode>("namedRefNode").Build());
    }

    [Fact]
    public void Two_SameTyped_Siblings_Are_Not_A_Cycle()
    {
        // Arrange
        var xprovider = Provider();

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
        var xprovider = Provider();

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
        var xprovider = Provider();

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
        var xprovider = Provider();

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
        var xprovider = Provider();

        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => xprovider.BuildMany<Node>(2));
    }
}
