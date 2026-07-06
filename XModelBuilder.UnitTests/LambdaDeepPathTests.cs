using System.Collections;
using System.Linq.Expressions;
using Microsoft.Extensions.DependencyInjection;
using XModelBuilder.Core;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.UnitTests;

// Exercises the strongly-typed expression deep-path setter (LambdaPathSetter): auto-vivification of
// nested members, growing arrays and lists, descending into elements, plus the unsupported-shape and
// argument-validation branches of the expression-tree parser.
public class LambdaDeepPathTests
{
    public sealed class Node
    {
        public string Name { get; set; } = "";
    }

    public sealed class Matrix
    {
        public int this[int a, int b] { get => 0; set { } }
    }

    public sealed class Root
    {
        public Node? Child { get; set; }
        public int[] Numbers { get; set; } = [];
        public Node[] Items { get; set; } = [];
        public List<Node> List { get; set; } = [];
        public List<Node>? NullableList { get; set; }
        public IList<Node>? InterfaceList { get; set; }
        public IList? RawList { get; set; }
        public int Value { get; set; }
        public Matrix Grid { get; set; } = new();

        public int Compute() => Value;
    }

    private static IModelBuilderProvider Provider() =>
        new ServiceCollection().AddXModelBuilder().BuildServiceProvider().GetRequiredService<IModelBuilderProvider>();

    private static IModelBuilder<Root> Builder() => Provider().For<Root>();

    [Fact]
    public void Nested_member_is_auto_vivified_and_set()
    {
        // Act
        var root = Builder().With(x => x.Child!.Name, "vivified").Build();

        // Assert
        Assert.NotNull(root.Child);
        Assert.Equal("vivified", root.Child!.Name);
    }

    [Fact]
    public void Array_grows_to_fit_the_index_and_sets_the_value()
    {
        // Act
        var root = Builder().With(x => x.Numbers[3], 42).Build();

        // Assert
        Assert.Equal(4, root.Numbers.Length);
        Assert.Equal(42, root.Numbers[3]);
    }

    [Fact]
    public void Array_growth_copies_existing_elements()
    {
        // Act
        var root = Builder()
            .With(x => x.Numbers, [10, 20])
            .With(x => x.Numbers[3], 42)
            .Build();

        // Assert
        Assert.Equal([10, 20, 0, 42], root.Numbers);
    }

    [Fact]
    public void Array_of_references_builds_and_descends_into_missing_element()
    {
        // Act
        var root = Builder().With(x => x.Items[1].Name, "built").Build();

        // Assert
        Assert.Equal(2, root.Items.Length);
        Assert.Equal("built", root.Items[1].Name);
    }

    [Fact]
    public void Array_element_that_already_exists_is_reused_not_rebuilt()
    {
        // Act
        var root = Builder()
            .With(x => x.Items[0].Name, "first")
            .With(x => x.Items[0].Name, "second")
            .Build();

        // Assert
        Assert.Single(root.Items);
        Assert.Equal("second", root.Items[0].Name); // same element mutated, not replaced
    }

    [Fact]
    public void Existing_list_grows_and_builds_intermediate_elements()
    {
        // Act
        var root = Builder().With(x => x.List[2].Name, "z").Build();

        // Assert
        Assert.True(root.List.Count >= 3);
        Assert.Equal("z", root.List[2].Name);
    }

    [Fact]
    public void Null_concrete_list_is_created_via_activator()
    {
        // Act
        var root = Builder().With(x => x.NullableList![1].Name, "q").Build();

        // Assert
        Assert.NotNull(root.NullableList);
        Assert.Equal("q", root.NullableList![1].Name);
    }

    [Fact]
    public void Null_generic_interface_list_is_materialized_as_list()
    {
        // Act
        var root = Builder().With(x => x.InterfaceList![0].Name, "iface").Build();

        // Assert
        Assert.NotNull(root.InterfaceList);
        Assert.Equal("iface", root.InterfaceList![0].Name);
    }

    [Fact]
    public void Null_non_generic_IList_is_materialized()
    {
        // Act
        var root = Builder().With(x => x.RawList![0], (object)new Node { Name = "raw" }).Build();

        // Assert
        Assert.NotNull(root.RawList);
        Assert.Equal("raw", ((Node)root.RawList![0]!).Name);
    }

    [Fact]
    public void Non_constant_array_index_is_not_supported()
    {
        // Arrange
        int j = 2;

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => Builder().With(x => x.Numbers[j], 5).Build());
    }

    [Fact]
    public void Non_constant_list_index_is_not_supported()
    {
        // Arrange
        int j = 1;

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => Builder().With(x => x.List[j].Name, "x").Build());
    }

    [Fact]
    public void Non_indexer_method_call_is_not_supported()
    {
        // Act & Assert
        Assert.Throws<NotSupportedException>(() => Builder().With(x => x.Compute(), 5).Build());
    }

    [Fact]
    public void Multi_argument_indexer_is_not_supported()
    {
        // Act & Assert
        Assert.Throws<NotSupportedException>(() => Builder().With(x => x.Grid[1, 2], 5).Build());
    }

    [Fact]
    public void Unsupported_expression_node_is_rejected()
    {
        // Act & Assert
        Assert.Throws<NotSupportedException>(() => Builder().With(x => x.Value + 1, 5).Build());
    }

    [Fact]
    public void Empty_path_expression_throws_ArgumentException()
    {
        // Arrange
        Expression<Func<Root, Root>> identity = x => x;

        // Act & Assert
        Assert.Throws<ArgumentException>(
            () => LambdaPathSetter.SetMemberValueByLambdaUntyped(new Root(), identity, new Root(), Provider()));
    }

    [Fact]
    public void Null_target_and_null_path_are_rejected()
    {
        // Arrange
        Expression<Func<Root, string>> path = x => x.Child!.Name;

        // Act & Assert
        Assert.Throws<ArgumentNullException>(
            () => LambdaPathSetter.SetMemberValueByLambdaUntyped(null!, path, "v", Provider()));
        Assert.Throws<ArgumentNullException>(
            () => LambdaPathSetter.SetMemberValueByLambdaUntyped(new Root(), null!, "v", Provider()));
    }

    public sealed class SingleIndexable
    {
        public int this[int i] { get => 0; set { } }
    }

    public sealed class IndexableHost
    {
        public SingleIndexable Custom { get; set; } = new();
    }

    [Fact]
    public void Indexing_a_member_that_is_not_a_collection_is_not_supported()
    {
        // Act & Assert - Custom has an indexer but is not an array/IList.
        Assert.Throws<NotSupportedException>(
            () => Provider().For<IndexableHost>().With(x => x.Custom[0], 5).Build());
    }

    [Fact]
    public void IndexExpression_with_single_constant_index_sets_the_element()
    {
        // Arrange - C# emits get_Item calls for indexers, so build an IndexExpression by hand.
        var param = Expression.Parameter(typeof(Root), "x");
        var listMember = Expression.Property(param, nameof(Root.List));
        var itemProperty = typeof(List<Node>).GetProperty("Item")!;
        var indexExpr = Expression.MakeIndex(listMember, itemProperty, [Expression.Constant(0)]);
        var lambda = Expression.Lambda(indexExpr, param);
        var root = new Root();

        // Act
        LambdaPathSetter.SetMemberValueByLambdaUntyped(root, lambda, new Node { Name = "byindex" }, Provider());

        // Assert
        Assert.Equal("byindex", root.List[0].Name);
    }

    [Fact]
    public void Strongly_typed_SetMemberValueByLambda_sets_the_value()
    {
        // Arrange
        var root = new Root();

        // Act
        LambdaPathSetter.SetMemberValueByLambda(root, x => x.Child!.Name, "typed", Provider());

        // Assert
        Assert.Equal("typed", root.Child!.Name);
    }

    [Fact]
    public void IndexExpression_with_multiple_arguments_is_not_supported()
    {
        // Arrange - a two-argument indexer expressed as an IndexExpression.
        var param = Expression.Parameter(typeof(Root), "x");
        var gridMember = Expression.Property(param, nameof(Root.Grid));
        var itemProperty = typeof(Matrix).GetProperty("Item")!;
        var indexExpr = Expression.MakeIndex(gridMember, itemProperty, [Expression.Constant(0), Expression.Constant(1)]);
        var lambda = Expression.Lambda(indexExpr, param);

        // Act & Assert
        Assert.Throws<NotSupportedException>(
            () => LambdaPathSetter.SetMemberValueByLambdaUntyped(new Root(), lambda, 5, Provider()));
    }

    [Fact]
    public void IndexExpression_with_non_constant_index_is_not_supported()
    {
        // Arrange
        var param = Expression.Parameter(typeof(Root), "x");
        var listMember = Expression.Property(param, nameof(Root.List));
        var itemProperty = typeof(List<Node>).GetProperty("Item")!;
        var nonConstant = Expression.Add(Expression.Constant(0), Expression.Constant(0));
        var indexExpr = Expression.MakeIndex(listMember, itemProperty, [nonConstant]);
        var lambda = Expression.Lambda(indexExpr, param);

        // Act & Assert
        Assert.Throws<NotSupportedException>(
            () => LambdaPathSetter.SetMemberValueByLambdaUntyped(new Root(), lambda, new Node(), Provider()));
    }
}
