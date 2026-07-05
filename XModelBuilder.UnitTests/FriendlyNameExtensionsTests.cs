using System.Collections;
using XModelBuilder.Core;

namespace XModelBuilder.UnitTests;

public class FriendlyNameExtensionsTests
{
    // Helper types used in tests
    public class SampleClass
    {
        public int Field = -1;
        public string Property { get; set; } = string.Empty;

        public int IntMethod()
        {
            Field += 1;
            return Field;
        }

        public static int MethodReturningInt() => 42;

        public static IDictionary GenericMethod<TKey, TValue>(Dictionary<TKey, TValue> dict) where TKey : notnull => dict;
    }

    [Fact]
    public void GetFriendlyName_TypeNull_ThrowsArgumentNullException()
    {
        // Arrange
        Type type = null!;

        // Act
        var exception = Assert.Throws<ArgumentNullException>(
            () => type.GetFriendlyName());

        // Assert
        Assert.Equal("type", exception.ParamName);
    }

    [Fact]
    public void GetFriendlyName_NullableValueType_ReturnsAliasedTypeWithQuestionMark()
    {
        // Act
        var friendlyName = typeof(int?).GetFriendlyName();

        // Assert
        Assert.Equal("int?", friendlyName);
    }

    [Fact]
    public void GetFriendlyName_SingleDimensionalArray_ReturnsElementNameWithBrackets()
    {
        // Act
        var friendlyName = typeof(string[]).GetFriendlyName();

        // Assert
        Assert.Equal("string[]", friendlyName);
    }

    [Fact]
    public void GetFriendlyName_MultiDimensionalArray_ReturnsElementNameWithCorrectRank()
    {
        // Act
        var friendlyName = typeof(int[,,]).GetFriendlyName();

        // Assert
        Assert.Equal("int[,,]", friendlyName);
    }

    [Fact]
    public void GetFriendlyName_SimpleAliasedType_ReturnsCSharpAlias()
    {
        // Act
        var friendlyName = typeof(int).GetFriendlyName();

        // Assert
        Assert.Equal("int", friendlyName);
    }

    [Fact]
    public void GetFriendlyName_SimpleNonAliasedType_WithAndWithoutNamespace()
    {
        // Act
        var withoutNamespace = typeof(FriendlyNameExtensions).GetFriendlyName();
        var withNamespace = typeof(FriendlyNameExtensions).GetFriendlyName(includeNamespace: true);

        // Assert
        Assert.Equal("FriendlyNameExtensions", withoutNamespace);
        Assert.Equal("XModelBuilder.Core.FriendlyNameExtensions", withNamespace);
    }

    [Fact]
    public void GetFriendlyName_ClosedGenericType_WithoutNamespace_FormatsCorrectly()
    {
        // Act
        var friendlyName = typeof(List<string>).GetFriendlyName();

        // Assert
        Assert.Equal("List<string>", friendlyName);
    }

    [Fact]
    public void GetFriendlyName_ClosedGenericType_WithNamespace_FormatsCorrectly()
    {
        // Act
        var friendlyName = typeof(Dictionary<string, int>).GetFriendlyName(includeNamespace: true);

        // Assert
        Assert.Equal("System.Collections.Generic.Dictionary<string,int>", friendlyName);
    }

    [Fact]
    public void GetFriendlyName_OpenGenericTypeDefinition_WithoutNamespace_UsesGenericParameterNames()
    {
        // Act
        var friendlyName = typeof(Dictionary<,>).GetFriendlyName();

        // Assert
        Assert.Equal("Dictionary<TKey,TValue>", friendlyName);
    }

    [Fact]
    public void GetFriendlyName_OpenGenericTypeDefinition_WithNamespace_UsesGenericParameterNames()
    {
        // Act
        var friendlyName = typeof(Dictionary<,>).GetFriendlyName(includeNamespace: true);

        // Assert
        Assert.Equal("System.Collections.Generic.Dictionary<TKey,TValue>", friendlyName);
    }

    [Fact]
    public void GetFriendlyName_IntMethod_ReturnsFullFriendlySignature()
    {
        // Arrange
        var method = typeof(SampleClass).GetMethod(nameof(SampleClass.IntMethod))!;

        // Act
        var friendlyName = method.GetFriendlyName();

        // Assert
        Assert.Equal("int SampleClass.IntMethod()", friendlyName);
    }

    [Fact]
    public void GetFriendlyName_MethodWithReturnType_ReturnsFullFriendlySignature()
    {
        // Arrange
        var method = typeof(SampleClass).GetMethod(nameof(SampleClass.MethodReturningInt))!;

        // Act
        var friendlyName = method.GetFriendlyName();

        // Assert
        Assert.Equal("int SampleClass.MethodReturningInt()", friendlyName);
    }

    [Fact]
    public void GetFriendlyName_Generic_Method_ReturnsFullFriendlySignature()
    {
        // Arrange
        var method = typeof(SampleClass).GetMethod(nameof(SampleClass.GenericMethod))!;

        // Act
        var friendlyName = method.GetFriendlyName();

        // Assert
        Assert.Equal("IDictionary SampleClass.GenericMethod<TKey,TValue>(Dictionary<TKey,TValue> dict)", friendlyName);
    }


    [Fact]
    public void GetFriendlyName_Field_ReturnsFullFriendlySignature()
    {
        // Arrange
        var field = typeof(SampleClass).GetField(nameof(SampleClass.Field))!;

        // Act
        var friendlyName = field.GetFriendlyName();

        // Assert
        Assert.Equal("int SampleClass.Field", friendlyName);
    }

    [Fact]
    public void GetFriendlyName_Property_ReturnsFullFriendlySignature()
    {
        // Arrange
        var property = typeof(SampleClass).GetProperty(nameof(SampleClass.Property))!;

        // Act
        var friendlyName = property.GetFriendlyName();

        // Assert
        Assert.Equal("string SampleClass.Property", friendlyName);
    }
}
