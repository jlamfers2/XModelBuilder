using System.Text.RegularExpressions;

namespace XModelBuilder.UnitTests;

// Tests for the seeded faker building blocks in RandomExtensions.
public class RandomExtensionsTests
{
    [Fact]
    public void Digits_ProducesRequestedCount_OfDigitCharacters()
    {
        // Arrange
        var random = new Random(1);

        // Act
        var value = random.Digits(7);

        // Assert
        Assert.Equal(7, value.Length);
        Assert.Matches(new Regex(@"^\d{7}$"), value);
    }

    [Fact]
    public void Digits_Zero_ReturnsEmpty_AndNegative_Throws()
    {
        // Arrange
        var random = new Random(1);

        // Act & Assert
        Assert.Equal("", random.Digits(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => random.Digits(-1));
    }

    [Fact]
    public void Digits_SameSeed_IsDeterministic()
    {
        // Arrange
        var a = new Random(42);
        var b = new Random(42);

        // Act & Assert
        Assert.Equal(a.Digits(20), b.Digits(20));
    }

    [Fact]
    public void PickFrom_ReturnsAnElement_AndThrowsOnEmpty()
    {
        // Arrange
        var random = new Random(1);
        var items = new[] { "a", "b", "c" };

        // Act
        var picked = random.PickFrom(items);

        // Assert
        Assert.Contains(picked, items);
        Assert.Throws<ArgumentException>(() => random.PickFrom(Array.Empty<string>()));
    }

    [Fact]
    public void FromPattern_FillsDigitsAndLetters_AndKeepsLiterals()
    {
        // Arrange
        var random = new Random(1);

        // Act
        var value = random.FromPattern("??-###-?", "BDF");

        // Assert
        Assert.Matches(new Regex(@"^[BDF]{2}-\d{3}-[BDF]$"), value);
    }

    [Fact]
    public void FromPattern_DefaultAlphabet_IsUppercaseLetters()
    {
        // Arrange
        var random = new Random(7);

        // Act
        var value = random.FromPattern("???");

        // Assert
        Assert.Matches(new Regex(@"^[A-Z]{3}$"), value);
    }
}
