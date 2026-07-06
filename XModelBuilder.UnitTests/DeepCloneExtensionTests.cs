using XModelBuilder.Core;

namespace XModelBuilder.UnitTests;

public class DeepCloneExtensionTests
{
    // ---- Helper types -------------------------------------------------------

    public sealed class Leaf
    {
        public int Value { get; set; }
    }

    public sealed class Parent
    {
        public Leaf? Child { get; set; }
        public int Number { get; set; }
        public string Text { get; set; } = string.Empty;
    }

    public sealed class TwoRefs
    {
        public Leaf? First { get; set; }
        public Leaf? Second { get; set; }
    }

    public sealed class Node
    {
        public Node? Self { get; set; }
        public int Id { get; set; }
    }

    public sealed class WithCallback
    {
        public Action? Callback { get; set; }
        public int Value { get; set; }
    }

    public class BaseEntity
    {
        private int _secret;
        private Leaf? _tag;

        protected BaseEntity(int secret, Leaf? tag)
        {
            _secret = secret;
            _tag = tag;
        }

        public int Secret => _secret;
        public Leaf? Tag => _tag;
    }

    public sealed class DerivedEntity : BaseEntity
    {
        public string Name { get; set; }

        public DerivedEntity(int secret, Leaf? tag, string name) : base(secret, tag) => Name = name;
    }

    public struct StructWithArray
    {
        public int[] Data;
        public int Count;
    }

    public struct PointStruct
    {
        public int X;
        public int Y;
    }

    // ---- IsImmutablePrimitive ----------------------------------------------

    [Theory]
    [InlineData(typeof(string), true)]
    [InlineData(typeof(int), true)]
    [InlineData(typeof(bool), true)]
    [InlineData(typeof(double), true)]
    [InlineData(typeof(char), true)]
    [InlineData(typeof(byte), true)]
    [InlineData(typeof(decimal), true)]
    [InlineData(typeof(DateTime), true)]
    [InlineData(typeof(DateTimeOffset), true)]
    [InlineData(typeof(TimeSpan), true)]
    [InlineData(typeof(Guid), true)]
    [InlineData(typeof(DayOfWeek), true)]      // enum
    [InlineData(typeof(object), false)]
    [InlineData(typeof(int[]), false)]
    [InlineData(typeof(Leaf), false)]          // reference type
    [InlineData(typeof(PointStruct), false)]   // custom (mutable) struct
    public void IsImmutablePrimitive_classifies_types(Type type, bool expected)
    {
        // Arrange (inputs come from [InlineData])
        // Act
        var result = type.IsImmutablePrimitive();

        // Assert
        Assert.Equal(expected, result);
    }

    // ---- Null handling ------------------------------------------------------

    [Fact]
    public void DeepClone_null_object_returns_null()
    {
        // Arrange
        object? original = null;

        // Act
        var clone = original.DeepClone();

        // Assert
        Assert.Null(clone);
    }

    [Fact]
    public void DeepClone_null_typed_returns_null()
    {
        // Arrange
        Leaf? original = null;

        // Act
        var clone = original.DeepClone();

        // Assert
        Assert.Null(clone);
    }

    // ---- Immutable values are shared, not copied ---------------------------

    [Fact]
    public void DeepClone_string_returns_same_instance()
    {
        // Arrange
        var text = new string("hello".ToCharArray()); // avoid interning

        // Act
        var clone = text.DeepClone();

        // Assert
        Assert.Same(text, clone);
    }

    [Fact]
    public void DeepClone_boxed_primitive_returns_equal_value()
    {
        // Arrange
        object boxed = 42;

        // Act
        var clone = boxed.DeepClone();

        // Assert
        Assert.Equal(42, clone);
    }

    [Fact]
    public void DeepClone_shares_immutable_reference_fields()
    {
        // Arrange
        var original = new Parent { Text = new string("shared".ToCharArray()), Number = 1 };

        // Act
        var clone = original.DeepClone()!;

        // Assert
        Assert.NotSame(original, clone);
        Assert.Same(original.Text, clone.Text); // strings are immutable -> shared
    }

    // ---- Simple object graph -----------------------------------------------

    [Fact]
    public void DeepClone_copies_scalar_members()
    {
        // Arrange
        var original = new Parent { Number = 7, Text = "abc" };

        // Act
        var clone = original.DeepClone()!;

        // Assert
        Assert.NotSame(original, clone);
        Assert.Equal(7, clone.Number);
        Assert.Equal("abc", clone.Text);
    }

    [Fact]
    public void DeepClone_nested_reference_is_independent()
    {
        // Arrange
        var original = new Parent { Child = new Leaf { Value = 1 } };

        // Act
        var clone = original.DeepClone()!;

        // Assert
        Assert.NotSame(original.Child, clone.Child);
        Assert.Equal(1, clone.Child!.Value);

        clone.Child.Value = 99;
        Assert.Equal(1, original.Child!.Value); // original untouched
    }

    [Fact]
    public void DeepClone_preserves_shared_reference_identity()
    {
        // Arrange
        var shared = new Leaf { Value = 7 };
        var original = new TwoRefs { First = shared, Second = shared };

        // Act
        var clone = original.DeepClone()!;

        // Assert
        Assert.NotSame(shared, clone.First);
        Assert.Same(clone.First, clone.Second); // one shared node stays one node
        Assert.Equal(7, clone.First!.Value);
    }

    // ---- Collections --------------------------------------------------------

    [Fact]
    public void DeepClone_list_is_independent_deep_copy()
    {
        // Arrange
        var original = new List<Leaf> { new() { Value = 1 }, new() { Value = 2 } };

        // Act
        var clone = original.DeepClone()!;

        // Assert
        Assert.NotSame(original, clone);
        Assert.Equal(2, clone.Count);
        Assert.NotSame(original[0], clone[0]);

        clone[0].Value = 50;
        clone.Add(new Leaf { Value = 3 });

        Assert.Equal(1, original[0].Value); // element untouched
        Assert.Equal(2, original.Count);    // list untouched
    }

    [Fact]
    public void DeepClone_dictionary_is_independent_deep_copy()
    {
        // Arrange
        var original = new Dictionary<string, Leaf> { ["a"] = new() { Value = 1 } };

        // Act
        var clone = original.DeepClone()!;

        // Assert
        Assert.NotSame(original, clone);
        Assert.NotSame(original["a"], clone["a"]);
        Assert.Equal(1, clone["a"].Value);

        clone["a"].Value = 42;
        Assert.Equal(1, original["a"].Value);
    }

    // ---- Arrays -------------------------------------------------------------

    [Fact]
    public void DeepClone_primitive_array_copies_values()
    {
        // Arrange
        var original = new[] { 1, 2, 3 };

        // Act
        var clone = original.DeepClone()!;

        // Assert
        Assert.NotSame(original, clone);
        Assert.Equal(original, clone);

        clone[0] = 99;
        Assert.Equal(1, original[0]);
    }

    [Fact]
    public void DeepClone_reference_array_deep_copies_elements()
    {
        // Arrange
        var original = new[] { new Leaf { Value = 1 }, new Leaf { Value = 2 } };

        // Act
        var clone = original.DeepClone()!;

        // Assert
        Assert.NotSame(original, clone);
        Assert.NotSame(original[0], clone[0]);
        Assert.Equal(2, clone[1].Value);
    }

    [Fact]
    public void DeepClone_empty_reference_array_succeeds()
    {
        // Arrange
        var original = Array.Empty<Leaf>();

        // Act
        var clone = original.DeepClone()!;

        // Assert
        Assert.NotNull(clone);
        Assert.Empty(clone);
    }

    [Fact]
    public void DeepClone_multidimensional_array_is_deep_copied()
    {
        // Arrange
        var original = new int[,] { { 1, 2 }, { 3, 4 } };

        // Act
        var clone = original.DeepClone()!;

        // Assert
        Assert.NotSame(original, clone);
        Assert.Equal(1, clone[0, 0]);
        Assert.Equal(4, clone[1, 1]);

        clone[0, 0] = 99;
        Assert.Equal(1, original[0, 0]);
    }

    [Fact]
    public void DeepClone_multidimensional_reference_array_deep_copies_elements()
    {
        // Arrange
        var original = new Leaf[,] { { new Leaf { Value = 1 } }, { new Leaf { Value = 2 } } };

        // Act
        var clone = original.DeepClone()!;

        // Assert
        Assert.NotSame(original[0, 0], clone[0, 0]);
        Assert.Equal(2, clone[1, 0].Value);
    }

    [Fact]
    public void DeepClone_array_with_non_zero_lower_bound_throws()
    {
        // Arrange
        var array = Array.CreateInstance(typeof(object), [2], [1]); // lower bound 1

        // Act & Assert
        Assert.Throws<NotSupportedException>(() => array.DeepClone());
    }

    // ---- Cyclic graphs ------------------------------------------------------

    [Fact]
    public void DeepClone_self_referencing_object_resolves_to_the_clone()
    {
        // Arrange
        var original = new Node { Id = 5 };
        original.Self = original;

        // Act
        var clone = original.DeepClone()!;

        // Assert
        Assert.NotSame(original, clone);
        Assert.Same(clone, clone.Self); // cycle points at the clone, not the original
        Assert.Equal(5, clone.Id);
    }

    [Fact]
    public void DeepClone_self_referencing_array_resolves_to_the_clone()
    {
        // Arrange
        var original = new object[1];
        original[0] = original;

        // Act
        var clone = (object[])original.DeepClone()!;

        // Assert
        Assert.NotSame(original, clone);
        Assert.Same(clone, clone[0]);
    }

    // ---- Private and base-type private fields ------------------------------

    [Fact]
    public void DeepClone_copies_private_and_base_type_private_fields()
    {
        // Arrange
        var original = new DerivedEntity(secret: 11, tag: new Leaf { Value = 3 }, name: "x");

        // Act
        var clone = original.DeepClone()!;

        // Assert
        Assert.NotSame(original, clone);
        Assert.Equal(11, clone.Secret);          // private field on base type
        Assert.Equal("x", clone.Name);
        Assert.NotSame(original.Tag, clone.Tag);  // private reference field is deep-copied
        Assert.Equal(3, clone.Tag!.Value);
    }

    // ---- Delegates ----------------------------------------------------------

    [Fact]
    public void DeepClone_sets_delegate_fields_to_null()
    {
        // Arrange
        var invoked = 0;
        var original = new WithCallback { Callback = () => invoked++, Value = 3 };

        // Act
        var clone = original.DeepClone()!;

        // Assert
        Assert.Null(clone.Callback);
        Assert.Equal(3, clone.Value);
        Assert.NotNull(original.Callback); // original keeps its delegate
    }

    // ---- Value types (structs) ---------------------------------------------

    [Fact]
    public void DeepClone_struct_deep_copies_reference_fields()
    {
        // Arrange
        var original = new StructWithArray { Data = [1, 2, 3], Count = 3 };

        // Act
        var clone = original.DeepClone();

        // Assert
        Assert.NotSame(original.Data, clone.Data);
        Assert.Equal(new[] { 1, 2, 3 }, clone.Data);
        Assert.Equal(3, clone.Count);

        clone.Data[0] = 99;
        Assert.Equal(1, original.Data[0]);
    }

    // ---- Generic overload returns the requested type -----------------------

    [Fact]
    public void DeepClone_generic_returns_typed_clone()
    {
        // Arrange
        var original = new Parent { Number = 1, Child = new Leaf { Value = 2 } };

        // Act
        Parent clone = original.DeepClone()!;

        // Assert
        Assert.IsType<Parent>(clone);
        Assert.Equal(2, clone.Child!.Value);
    }
}
