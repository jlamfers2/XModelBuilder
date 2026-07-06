using XModelBuilder.Core;

namespace XModelBuilder.UnitTests;

// Direct tests for the low-level mini-language scanner (peek/next/expect/skip + error reporting).
public class CharScannerTests
{
    [Fact]
    public void Null_source_is_treated_as_empty()
    {
        // Arrange
        var scanner = new CharScanner(null!);

        // Act & Assert
        Assert.True(scanner.Eof());
        Assert.Equal(CharScanner.EOF, scanner.Peek());
        Assert.Equal(CharScanner.EOF, scanner.Next());
    }

    [Fact]
    public void Peek_and_Eof_respect_offset_and_bounds()
    {
        // Arrange
        var scanner = new CharScanner("ab");

        // Act & Assert
        Assert.Equal('a', scanner.Peek());
        Assert.Equal('b', scanner.Peek(1));
        Assert.Equal(CharScanner.EOF, scanner.Peek(2));
        Assert.False(scanner.Eof(1));
        Assert.True(scanner.Eof(2));
    }

    [Fact]
    public void Next_advances_then_returns_eof_at_end()
    {
        // Arrange
        var scanner = new CharScanner("hi");

        // Act & Assert
        Assert.Equal('h', scanner.Next());
        Assert.Equal('i', scanner.Next());
        Assert.Equal(CharScanner.EOF, scanner.Next());
    }

    [Fact]
    public void Expect_matches_or_throws()
    {
        // Arrange
        var scanner = new CharScanner("[");

        // Act & Assert
        Assert.Same(scanner, scanner.Expect('['));
        Assert.Throws<FormatException>(() => new CharScanner("x").Expect('['));
    }

    [Fact]
    public void ExpectEof_passes_at_end_and_throws_when_input_remains()
    {
        // Arrange
        var empty = new CharScanner("");

        // Act & Assert
        Assert.Same(empty, empty.ExpectEof());
        Assert.Throws<FormatException>(() => new CharScanner("x").ExpectEof());
    }

    [Fact]
    public void ExpectAnyOf_no_args_is_noop_otherwise_matches_or_throws()
    {
        // Arrange
        var scanner = new CharScanner("b");

        // Act & Assert
        Assert.Same(scanner, scanner.ExpectAnyOf()); // no expected values -> nothing consumed
        Assert.Same(scanner, scanner.ExpectAnyOf('a', 'b')); // consumes 'b'
        Assert.Throws<FormatException>(() => new CharScanner("z").ExpectAnyOf('a', 'b'));
    }

    [Fact]
    public void NextAnyOf_matches_primary_or_alternative_or_throws()
    {
        // Arrange & Act & Assert
        Assert.Equal('a', new CharScanner("a").NextAnyOf('a', 'b'));
        Assert.Equal('b', new CharScanner("b").NextAnyOf('a', 'b'));
        Assert.Throws<FormatException>(() => new CharScanner("z").NextAnyOf('a', 'b'));
    }

    [Fact]
    public void PeekIsAnyOf_reports_membership_without_advancing()
    {
        // Arrange
        var scanner = new CharScanner("a");

        // Act & Assert
        Assert.False(scanner.PeekIsAnyOf()); // no expected values
        Assert.True(scanner.PeekIsAnyOf('x', 'a'));
        Assert.False(scanner.PeekIsAnyOf('x', 'y'));
        Assert.Equal('a', scanner.Peek()); // not advanced
        Assert.True(new CharScanner("ab").PeekIsAnyOfWithOffset(1, 'b'));
    }

    [Fact]
    public void SkipInsignificants_skips_all_whitespace_kinds_and_stops()
    {
        // Arrange
        var scanner = new CharScanner(" \t\r\n x");

        // Act
        scanner.SkipInsignificants();

        // Assert
        Assert.Equal('x', scanner.Peek());
        // Skipping at end of input stops cleanly at EOF.
        var atEnd = new CharScanner("   ");
        atEnd.SkipInsignificants();
        Assert.True(atEnd.Eof());
    }

    [Fact]
    public void ParseError_includes_position_and_fragment()
    {
        // Arrange
        var scanner = new CharScanner("abcdef");
        scanner.Next();
        scanner.Next();
        scanner.Next();

        // Act
        var error = scanner.ParseError("boom");

        // Assert
        Assert.Contains("pos 3", error.Message);
        Assert.Contains("boom", error.Message);
    }

    [Fact]
    public void ToCharString_renders_eof_and_regular_characters()
    {
        // Act & Assert
        Assert.Equal("EOF", CharScanner.ToCharString(CharScanner.EOF));
        Assert.Equal("A", CharScanner.ToCharString('A'));
    }

    [Fact]
    public void Source_and_Pos_expose_scanner_state()
    {
        // Arrange
        var scanner = new CharScanner("xy");

        // Act
        scanner.Next();

        // Assert
        Assert.Equal("xy", scanner.Source);
        Assert.Equal(1, scanner.Pos);
    }
}
