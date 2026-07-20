namespace XModelBuilder.UnitTests;

// Tests for the reusable, country-agnostic check-digit algorithms in Checksums.
public class ChecksumsTests
{
    [Fact]
    public void Mod11WeightedSum_IsZero_ForValidIsbn10()
    {
        // Arrange
        // ISBN-10 0-306-40615-2 uses weights 10..1 and is valid (weighted sum divisible by 11).
        var weights = new[] { 10, 9, 8, 7, 6, 5, 4, 3, 2, 1 };

        // Act
        var result = Checksums.Mod11WeightedSum("0306406152", weights);

        // Assert
        Assert.Equal(0, result);
        Assert.True(Checksums.Mod11IsValid("0306406152", weights));
    }

    [Fact]
    public void Mod11WeightedSum_HandlesNegativeWeights_ForValidBsn()
    {
        // Arrange
        // 123456782 is a classic valid test BSN (last digit weight -1).
        var weights = new[] { 9, 8, 7, 6, 5, 4, 3, 2, -1 };

        // Act
        var result = Checksums.Mod11WeightedSum("123456782", weights);

        // Assert
        Assert.Equal(0, result);
    }

    [Fact]
    public void Mod11WeightedSum_Throws_OnLengthMismatch()
    {
        // Arrange
        var weights = new[] { 9, 8, 7 };

        // Act & Assert
        Assert.Throws<ArgumentException>(() => Checksums.Mod11WeightedSum("1234", weights));
    }

    [Fact]
    public void Luhn_ValidatesKnownNumber_AndComputesCheckDigit()
    {
        // Arrange
        // 79927398713 is a well-known Luhn-valid number; its body is 7992739871 with check digit 3.
        // Act & Assert
        Assert.True(Checksums.LuhnIsValid("79927398713"));
        Assert.Equal(3, Checksums.LuhnCheckDigit("7992739871"));
        Assert.False(Checksums.LuhnIsValid("79927398710"));
    }

    [Fact]
    public void Gs1_ValidatesKnownEan13_AndComputesCheckDigit()
    {
        // Arrange
        // 4006381333931 is a valid EAN-13; its body is 400638133393 with check digit 1.
        // Act & Assert
        Assert.True(Checksums.Gs1IsValid("4006381333931"));
        Assert.Equal(1, Checksums.Gs1CheckDigit("400638133393"));
        Assert.False(Checksums.Gs1IsValid("4006381333930"));
    }

    [Fact]
    public void Mod97_ReturnsOne_ForValidIban()
    {
        // Arrange
        // A valid IBAN validates to 1 when the first four chars move to the end and letters expand.
        var rearranged = "ABNA0417164300" + "NL91";

        // Act
        var result = Checksums.Mod97(rearranged);

        // Assert
        Assert.Equal(1, result);
    }

    [Fact]
    public void Mod97_Throws_OnInvalidCharacter()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentException>(() => Checksums.Mod97("AB-12"));
    }
}
