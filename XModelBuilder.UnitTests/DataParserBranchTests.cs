using XModelBuilder.Core;

namespace XModelBuilder.UnitTests;

// Top-up coverage for the mini-language parser: the LooksLikeBareObject discriminator (used to tell a
// bare object literal from a builder name) and the string-escape branches.
public class DataParserBranchTests
{
    [Theory]
    [InlineData("Street:Foo", true)]                 // top-level ':' -> bare object
    [InlineData("just-a-builder-name", false)]        // no ':' -> looks like a name
    [InlineData("[1:2]", false)]                      // ':' nested inside '[ ]' (depth > 0)
    [InlineData("{a:1}", false)]                       // ':' nested inside '{ }' (depth > 0)
    [InlineData("\"key:value\"", false)]              // ':' inside a string
    [InlineData("\"a\\\"b\":1", true)]                // escaped quote inside string, then top-level ':'
    public void LooksLikeBareObject_detects_top_level_assignment_only(string text, bool expected)
    {
        // Act
        var result = DataParser.LooksLikeBareObject(text);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void Parse_reads_a_top_level_bare_and_quoted_string()
    {
        // Arrange
        var parser = new DataParser();

        // Act & Assert
        Assert.Equal("hello world", parser.Parse("hello world")); // bare value keeps internal spaces
        Assert.Equal("quoted", new DataParser().Parse("\"quoted\""));
    }

    [Fact]
    public void ReadString_resolves_every_valid_escape_sequence()
    {
        // Arrange
        var parser = new DataParser();

        // Act
        var result = (string)parser.Parse("\"a\\n\\r\\t\\\\\\\"b\"");

        // Assert
        Assert.Equal("a\n\r\t\\\"b", result);
    }

    [Fact]
    public void ReadString_rejects_an_invalid_escape_sequence()
    {
        // Arrange
        var parser = new DataParser();

        // Act & Assert
        Assert.Throws<FormatException>(() => parser.Parse("\"\\x\""));
    }
}
