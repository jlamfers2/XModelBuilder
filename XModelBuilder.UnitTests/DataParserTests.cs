using XModelBuilder.Core;

namespace XModelBuilder.UnitTests;

public class DataParserTests
{
    private readonly DataParser _sut = new();

    [Fact]
    public void Parse_String_All_Escape_Branches()
    {
        // \\, \", \n, \r, \t en unknown escape -> backslash+char blijft staan
        var input = "\"a\\\\b\\\"c\\nd\\re\\tfg\"";
        var result = _sut.Parse(input);

        Assert.Equal("a\\b\"c\nd\re\tfg", result);
    }

    [Fact]
    public void Parse_Empty_String()
    {
        var result = _sut.Parse("\"\"");
        Assert.Equal(string.Empty, result);
    }

    [Fact]
    public void Parse_Token_With_Whitespace_And_Newlines()
    {
        // SkipInsignificants slikt spaties/tabs/nl/cr
        var result = _sut.Parse(" \n\t  hello\r\n ");
        Assert.Equal("hello", result);
    }

    [Fact]
    public void Parse_Array_Empty()
    {
        var result = _sut.Parse("[]");
        var arr = Assert.IsType<object?[]>(result);
        Assert.Empty(arr);
    }

    [Fact]
    public void Parse_Array_Mixed_And_Nested()
    {
        var result = _sut.Parse("[\"x\",y,[\"z\"],{a:1,\"b\":\"2\"}]");

        var arr = Assert.IsType<object?[]>(result);
        Assert.Equal(4, arr.Length);

        Assert.Equal("x", arr[0]);
        Assert.Equal("y", arr[1]);

        var inner = Assert.IsType<object?[]>(arr[2]);
        Assert.Single(inner);
        Assert.Equal("z", inner[0]);

        var obj = Assert.IsType<Dictionary<string, object>>(arr[3]);
        Assert.Equal("1", obj["a"]);
        Assert.Equal("2", obj["b"]);
    }

    [Fact]
    public void Parse_Object_Empty()
    {
        var result = _sut.Parse("{}");
        var obj = Assert.IsType<Dictionary<string, object>>(result);
        Assert.Empty(obj);
    }

    [Fact]
    public void Parse_Object_Unquoted_And_Quoted_Keys()
    {
        var result = _sut.Parse("{a:1,\"b\":2}");

        var obj = Assert.IsType<Dictionary<string, object>>(result);
        Assert.Equal("1", obj["a"]);
        Assert.Equal("2", obj["b"]);
    }

    [Fact]
    public void Parse_Object_Nested()
    {
        var result = _sut.Parse("{outer:{inner:[1,2,\"3\"]}}");

        var obj = Assert.IsType<Dictionary<string, object>>(result);
        var outer = Assert.IsType<Dictionary<string, object>>(obj["outer"]);

        var inner = Assert.IsType<object?[]>(outer["inner"]);
        Assert.Equal(3, inner.Length);
        Assert.Equal("1", inner[0]);
        Assert.Equal("2", inner[1]);
        Assert.Equal("3", inner[2]);
    }

    [Fact]
    public void Parse_Rejects_Trailing_Garbage_After_Value()
    {
        Assert.Throws<FormatException>(() => _sut.Parse("\"ok\" trailing"));
    }

    [Fact]
    public void Parse_Unterminated_String_Throws_FormatException()
    {
        Assert.Throws<FormatException>(() => _sut.Parse("\"unterminated"));
    }

    [Fact]
    public void Parse_Array_Missing_EndBracket_Throws_FormatException()
    {
        Assert.Throws<FormatException>(() => _sut.Parse("[1,2"));
    }

    [Fact]
    public void Parse_Object_Missing_EndBrace_Throws_FormatException()
    {
        Assert.Throws<FormatException>(() => _sut.Parse("{a:1"));
    }

    [Fact]
    public void Parse_Object_Missing_Assignment_Throws_FormatException()
    {
        Assert.Throws<FormatException>(() => _sut.Parse("{a 1}"));
    }

    [Fact]
    public void Parse_Array_Missing_Separator_Throws_FormatException()
    {
        // Een ontbrekend scheidingsteken tussen twee gestructureerde elementen is nog steeds een fout.
        Assert.Throws<FormatException>(() => _sut.Parse("[{a:1}{b:2}]"));
    }

    [Fact]
    public void Parse_Array_BareValueWithSpaces_IsSingleElement()
    {
        // Whitespace is GEEN scheidingsteken meer (alleen ',' is dat): een unquoted waarde mag interne
        // spaties bevatten, met getrimde randen. Zo werkt de "non-verbose" tabelvorm ("Clean Code").
        var result = (object[])_sut.Parse("[1 2]");

        Assert.Single(result);
        Assert.Equal("1 2", result[0]);
    }

    [Fact]
    public void Parse_BareObject_WithoutBraces_ParsesKeyValues()
    {
        // ParseObject accepteert ook een "bare" object zonder { } op het top-niveau.
        var result = _sut.ParseObject("Straat:Hoofdstraat,Postcode:1234 AB")
            .ToDictionary(kv => kv.Key, kv => kv.Value);

        Assert.Equal("Hoofdstraat", result["Straat"]);
        Assert.Equal("1234 AB", result["Postcode"]);
    }
}
