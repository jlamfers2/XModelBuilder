using System.Globalization;
using XModelBuilder.Core;
using XModelBuilder.Default;

namespace XModelBuilder.UnitTests;

public class ValueConverterTests
{
    private readonly IModelBuilderProvider _provider = DefaultModelBuilderProvider.Current;

    private readonly CultureInfo _enUs = new CultureInfo("en-US");
    private readonly CultureInfo _frFr = new CultureInfo("fr-FR");

    // Simple class used for new()/default() and named-builder tests
    private class SampleClass
    {
        public int Value { get; set; }
    }

    [Fact]
    public void Convert_NullInputToString_ReturnsNull()
    {
        var result = ValueConverter.Convert(null, typeof(string), _enUs, _enUs, _provider);

        Assert.Null(result);
    }

    [Fact]
    public void Convert_TokenNullToNullableInt_ReturnsNull()
    {
        var result = ValueConverter.Convert("null()", typeof(int?), null, _provider);

        Assert.Null(result);
    }

    [Fact]
    public void Convert_TokenDefaultToNonNullableInt_ReturnsDefault()
    {
        var result = ValueConverter.Convert("default()", typeof(int), null, _provider);

        Assert.Equal(0, result);
    }

    [Fact]
    public void Convert_TokenDefaultToString_ReturnsNull()
    {
        var result = ValueConverter.Convert("default()", typeof(string), null, _provider);

        Assert.Null(result);
    }

    [Fact]
    public void Convert_TokenNew_CreatesNewInstance()
    {
        var result = ValueConverter.Convert("new()", typeof(SampleClass), null, _provider);

        Assert.NotNull(result);
        Assert.IsType<SampleClass>(result);
    }

    [Fact]
    public void Convert_TokenDefault_BuildsViaModelBuilderProvider()
    {
        var result = ValueConverter.Convert("default()", typeof(SampleClass), null, _provider);

        Assert.Equal(typeof(SampleClass), result!.GetType());
    }

    [Fact]
    public void Convert_EscapedNullToString_ReturnsLiteralToken()
    {
        var result = ValueConverter.Convert("@null()", typeof(string), null, _provider);

        Assert.Equal("null()", result);
    }

    [Fact]
    public void Convert_EscapedDefaultToString_ReturnsLiteralToken()
    {
        var result = ValueConverter.Convert("@default()", typeof(string), null, _provider);

        Assert.Equal("default()", result);
    }

    [Fact]
    public void Convert_EscapedNewToString_ReturnsLiteralToken()
    {
        var result = ValueConverter.Convert("@new()", typeof(string), null, _provider);

        Assert.Equal("new()", result);
    }

    [Fact]
    public void Convert_EscapedNamedBuilderReferenceToString_ReturnsLiteralToken()
    {
        var result = ValueConverter.Convert("@my-builder-name", typeof(string), null, _provider);

        Assert.Equal("my-builder-name", result);
    }

    [Fact]
    public void Convert_UnknownNamedBuilderReference_Throws()
    {
        Assert.Throws<KeyNotFoundException>(
            () => ValueConverter.Convert("does-not-exist", typeof(SampleClass), null, _provider));
    }

    [Fact]
    public void Convert_StringTargetType_ReturnsTrimmedString()
    {
        var result = ValueConverter.Convert("  hello  ", typeof(string), null, _provider);

        Assert.Equal("hello", result);
    }

    [Fact]
    public void Convert_EmptyStringToNullableInt_ReturnsNull()
    {
        var result = ValueConverter.Convert("   ", typeof(int?), null, _provider);

        Assert.Null(result);
    }

    [Fact]
    public void Convert_EmptyStringToNonNullableInt_ThrowsArgumentException()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => ValueConverter.Convert("   ", typeof(int), null, _provider));

        Assert.Contains("Cannot convert", ex.Message);
    }

    [Fact]
    public void Convert_ArrayOfInts_ReturnsIntArray()
    {
        var result = (int[])ValueConverter.Convert("1,2,3", typeof(int[]), null, _provider)!;

        Assert.Equal(new[] { 1, 2, 3 }, result);
    }

    [Fact]
    public void Convert_ListOfInts_ReturnsListOfInts()
    {
        var result = (List<int>)ValueConverter.Convert("1,2,3", typeof(List<int>), null, _provider)!;

        Assert.Equal(new List<int> { 1, 2, 3 }, result);
    }

    [Fact]
    public void Convert_IEnumerableOfInts_ReturnsListOfInts()
    {
        var result = (IEnumerable<int>)ValueConverter.Convert("4,5,6", typeof(IEnumerable<int>), null, _provider)!;

        Assert.Equal(new[] { 4, 5, 6 }, result);
    }

    [Fact]
    public void Convert_IReadOnlyListOfInts_ReturnsListOfInts()
    {
        var result = (IReadOnlyList<int>)ValueConverter.Convert("4,5,6", typeof(IReadOnlyList<int>), null, _provider)!;

        Assert.Equal(new[] { 4, 5, 6 }, result);
    }

    [Fact]
    public void Convert_IReadOnlyCollectionOfObjectLiterals_BuildsEachElement()
    {
        var result = (IReadOnlyCollection<SampleClass>)ValueConverter.Convert(
            "[{Value:1},{Value:2}]", typeof(IReadOnlyCollection<SampleClass>), null, _provider)!;

        Assert.Equal(new[] { 1, 2 }, result.Select(x => x.Value));
    }

    [Fact]
    public void Convert_NestedArrayOfArrays_ReturnsJaggedArray()
    {
        var result = (int[][])ValueConverter.Convert("[[1,2],[3,4,5]]", typeof(int[][]), null, _provider)!;

        Assert.Equal(new[] { 1, 2 }, result[0]);
        Assert.Equal(new[] { 3, 4, 5 }, result[1]);
    }

    [Fact]
    public void Convert_ArrayOfObjectLiterals_BuildsEachElement()
    {
        var result = (SampleClass[])ValueConverter.Convert("[{Value:1},{Value:2}]", typeof(SampleClass[]), null, _provider)!;

        Assert.Equal(2, result.Length);
        Assert.Equal(1, result[0].Value);
        Assert.Equal(2, result[1].Value);
    }

    [Fact]
    public void Convert_Dictionary_FromObjectLiteral_BuildsDictionary()
    {
        var result = (Dictionary<string, int>)ValueConverter.Convert("{a:1,b:2}", typeof(Dictionary<string, int>), null, _provider)!;

        Assert.Equal(1, result["a"]);
        Assert.Equal(2, result["b"]);
    }

    [Fact]
    public void Convert_IDictionaryInterface_BuildsDictionary()
    {
        var result = (IDictionary<string, int>)ValueConverter.Convert("{a:1}", typeof(IDictionary<string, int>), null, _provider)!;

        Assert.Equal(1, result["a"]);
    }

    [Fact]
    public void Convert_Dictionary_WithObjectLiteralValues_ConvertsValuesRecursively()
    {
        var result = (Dictionary<string, SampleClass>)ValueConverter.Convert(
            "{first:{Value:1},second:{Value:2}}", typeof(Dictionary<string, SampleClass>), null, _provider)!;

        Assert.Equal(1, result["first"].Value);
        Assert.Equal(2, result["second"].Value);
    }

    [Fact]
    public void Convert_Dictionary_WithoutObjectLiteral_Throws()
    {
        Assert.Throws<FormatException>(
            () => ValueConverter.Convert("not-an-object", typeof(Dictionary<string, int>), null, _provider));
    }

    [Fact]
    public void Convert_HashSet_FromArraySyntax_BuildsHashSet()
    {
        var result = (HashSet<int>)ValueConverter.Convert("[1,2,3]", typeof(HashSet<int>), null, _provider)!;

        Assert.Equal(new HashSet<int> { 1, 2, 3 }, result);
    }

    [Fact]
    public void Convert_HashSet_FromBareCommaList_BuildsHashSet()
    {
        var result = (HashSet<int>)ValueConverter.Convert("1,2,3", typeof(HashSet<int>), null, _provider)!;

        Assert.Equal(3, result.Count);
    }

    [Fact]
    public void Convert_ISetInterface_BuildsHashSet()
    {
        var result = (ISet<string>)ValueConverter.Convert("[\"x\",\"y\"]", typeof(ISet<string>), null, _provider)!;

        Assert.Contains("x", result);
        Assert.Contains("y", result);
    }

    [Fact]
    public void Convert_ObjectLiteral_BuildsInstanceWithMembersSet()
    {
        var result = (SampleClass)ValueConverter.Convert("{Value:42}", typeof(SampleClass), null, _provider)!;

        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Convert_BareObjectLiteral_WithoutBraces_BuildsInstanceForComplexTarget()
    {
        // "non-verbose": geen { } nodig op het top-niveau. De top-level ':' onderscheidt dit van een
        // builder-naam (zoals "does-not-exist", die zonder ':' juist als builder-referentie geldt).
        var result = (SampleClass)ValueConverter.Convert("Value:42", typeof(SampleClass), null, _provider)!;

        Assert.Equal(42, result.Value);
    }

    [Fact]
    public void Convert_NullableInt_ExercisesGenericNonCollectionBranch()
    {
        var result = ValueConverter.Convert("7", typeof(int?), null, _provider);

        Assert.Equal(7, result);
    }

    [Fact]
    public void Convert_EnumByName_ReturnsEnumValue()
    {
        var result = ValueConverter.Convert("Friday", typeof(DayOfWeek), null, _provider);

        Assert.Equal(DayOfWeek.Friday, result);
    }

    [Fact]
    public void Convert_KnownType_Int_UsesKnownTypeConverter()
    {
        var result = ValueConverter.Convert("42", typeof(int), null, _provider);

        Assert.Equal(42, result);
    }

    [Fact]
    public void Convert_FallbackToChangeType_UsesChangeType()
    {
        var result = ValueConverter.Convert("5", typeof(sbyte), null, _provider);

        Assert.Equal((sbyte)5, result);
    }

    [Fact]
    public void Convert_Overload_UsesDateTimeCultureForDateTime()
    {
        // This date is valid in fr-FR (day/month/year) but not in en-US.
        var dateString = "31/12/2023";

        var result = ValueConverter.Convert(
            dateString,
            typeof(DateTime),
            _frFr,
            _enUs,
            _provider);

        var dt = Assert.IsType<DateTime>(result);
        Assert.Equal(2023, dt.Year);
        Assert.Equal(12, dt.Month);
        Assert.Equal(31, dt.Day);
    }

    [Fact]
    public void Convert_Overload_UsesDefaultCultureForNonDateTypes()
    {
        // Use thousands separator that is valid for en-US but not fr-FR.
        var numberString = "1,234";

        var result = ValueConverter.Convert(
            numberString,
            typeof(int),
            _frFr,
            _enUs,
            _provider);

        Assert.Equal(1234, result);
    }
}
