using System.Collections;
using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using XModelBuilder.Core;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.UnitTests;

// Exercises the string deep-path setter (StringPathSetter): member vivification, growing arrays,
// materializing fixed-size/read-only collections into growable lists, descending into elements, and
// the segment-parsing / member-resolution error branches.
public class StringDeepPathTests
{
    public sealed class Leaf
    {
        public string Name { get; set; } = "";
        public int Count { get; set; }
    }

    public sealed class Bag
    {
        public Leaf? Child { get; set; }
        public int[] Numbers { get; set; } = [];
        public Leaf[] Items { get; set; } = [];
        public List<int> GrowList { get; set; } = [];
        public IList<int> FixedList { get; set; } = [1, 2]; // default collection-expression is an int[] -> fixed size
        public IList<int> ArrayBacked { get; set; } = new int[] { 7, 8 }; // genuinely fixed-size array behind an IList
        public List<Leaf> Leaves { get; set; } = [];
    }

    private static IModelBuilderProvider Provider() =>
        new ServiceCollection().AddXModelBuilder().BuildServiceProvider().GetRequiredService<IModelBuilderProvider>();

    private static IModelBuilder<Bag> Builder() => Provider().For<Bag>();

    [Fact]
    public void Nested_member_is_vivified_and_final_value_is_converted()
    {
        // Act
        var bag = Builder().With("Child.Count", "7").Build();

        // Assert
        Assert.NotNull(bag.Child);
        Assert.Equal(7, bag.Child!.Count);
    }

    [Fact]
    public void Array_grows_to_fit_index_and_converts_value()
    {
        // Act
        var bag = Builder().With("Numbers[3]", "9").Build();

        // Assert
        Assert.Equal([0, 0, 0, 9], bag.Numbers);
    }

    [Fact]
    public void Array_growth_copies_existing_elements()
    {
        // Act
        var bag = Builder().With("Numbers", "[1,2]").With("Numbers[3]", "9").Build();

        // Assert
        Assert.Equal([1, 2, 0, 9], bag.Numbers);
    }

    [Fact]
    public void Array_of_references_builds_and_descends_into_missing_element()
    {
        // Act
        var bag = Builder().With("Items[1].Name", "hi").Build();

        // Assert
        Assert.Equal(2, bag.Items.Length);
        Assert.Equal("hi", bag.Items[1].Name);
    }

    [Fact]
    public void Array_element_that_exists_is_reused_and_descended()
    {
        // Act
        var bag = Builder().With("Items[0].Name", "a").With("Items[0].Name", "b").Build();

        // Assert
        Assert.Single(bag.Items);
        Assert.Equal("b", bag.Items[0].Name);
    }

    [Fact]
    public void Growable_list_is_used_in_place()
    {
        // Act
        var bag = Builder().With("GrowList[2]", "9").Build();

        // Assert
        Assert.Equal([0, 0, 9], bag.GrowList);
    }

    [Fact]
    public void Fixed_size_collection_is_materialized_into_a_growable_list_copying_existing()
    {
        // Act
        var bag = Builder().With("FixedList[3]", "9").Build();

        // Assert
        Assert.Equal([1, 2, 0, 9], bag.FixedList);
    }

    [Fact]
    public void Fixed_size_array_behind_an_ilist_is_materialized_copying_existing_elements()
    {
        // Act
        var bag = Builder().With("ArrayBacked[3]", "9").Build();

        // Assert
        Assert.Equal([7, 8, 0, 9], bag.ArrayBacked);
    }

    [Fact]
    public void List_of_references_builds_and_descends_into_missing_element()
    {
        // Act
        var bag = Builder().With("Leaves[1].Name", "L").Build();

        // Assert
        Assert.True(bag.Leaves.Count >= 2);
        Assert.Equal("L", bag.Leaves[1].Name);
    }

    [Fact]
    public void List_element_that_exists_is_reused_and_descended()
    {
        // Act
        var bag = Builder().With("Leaves[0].Name", "a").With("Leaves[0].Name", "b").Build();

        // Assert
        Assert.Single(bag.Leaves);
        Assert.Equal("b", bag.Leaves[0].Name);
    }

    [Fact]
    public void Unknown_member_throws_InvalidOperation()
    {
        // Act & Assert
        Assert.Throws<InvalidOperationException>(() => Builder().With("Ghost", "x").Build());
    }

    [Fact]
    public void Unknown_nested_member_throws_InvalidOperation()
    {
        // Act & Assert - Child vivifies, then Ghost is not a member of Leaf.
        Assert.Throws<InvalidOperationException>(() => Builder().With("Child.Ghost", "x").Build());
    }

    [Fact]
    public void Missing_closing_bracket_throws_FormatException()
    {
        // Act & Assert
        Assert.Throws<FormatException>(() => Builder().With("Items[0", "x").Build());
    }

    [Fact]
    public void Empty_path_and_null_arguments_are_rejected()
    {
        // Arrange
        var bag = new Bag();
        var inv = CultureInfo.InvariantCulture;
        var provider = Provider();

        // Act & Assert
        Assert.Throws<ArgumentException>(() => bag.SetMemberValueByString("", "x", inv, inv, provider));
        Assert.Throws<ArgumentNullException>(() => ((object)null!).SetMemberValueByString("Child", "x", inv, inv, provider));
        Assert.Throws<ArgumentNullException>(() => bag.SetMemberValueByString(null!, "x", inv, inv, provider));
    }
}
