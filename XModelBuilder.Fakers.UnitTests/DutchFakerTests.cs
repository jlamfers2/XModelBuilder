using System.Text.RegularExpressions;
using Microsoft.Extensions.DependencyInjection;
using XModelBuilder.DependencyInjection;
using XModelBuilder.Fakers.Dutch;
using XModelBuilder.Fakers.XFaker;

namespace XModelBuilder.Fakers.UnitTests;

public class DutchFakerTests
{
    public class Party
    {
        public string Bsn { get; set; } = "";
        public string Postcode { get; set; } = "";
    }

    // The ServiceProvider is the determinism/isolation boundary: a fresh provider per call gets its own
    // seeded Random, so two providers with the same seed reproduce each other exactly.
    private static IModelBuilderProvider CreateProvider(int seed = 8675309) =>
        new ServiceCollection()
            .AddXModelBuilder()
            .AddDutchFaker(seed)
            .BuildServiceProvider()
            .GetRequiredService<IModelBuilderProvider>();

    private static DutchFakerApi NL(int seed = 8675309) => CreateProvider(seed).NL();

    // The official BSN/RSIN 11-test: 9*d1+8*d2+...+2*d8-1*d9 must be divisible by 11.
    private static bool PassesBsnElfproef(string number)
    {
        if (number.Length != 9 || !number.All(char.IsDigit))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < 8; i++)
        {
            sum += (9 - i) * (number[i] - '0');
        }

        sum -= number[8] - '0';
        return sum % 11 == 0;
    }

    // The bank 11-test: 9*d1+8*d2+...+1*d9 must be divisible by 11.
    private static bool PassesBankElfproef(string number)
    {
        if (number.Length != 9 || !number.All(char.IsDigit))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < 9; i++)
        {
            sum += (9 - i) * (number[i] - '0');
        }

        return sum % 11 == 0;
    }

    // ISO 13616 IBAN validation: move the first four chars to the end, expand letters to numbers, mod 97 == 1.
    private static bool IsValidIban(string iban)
    {
        var rearranged = iban[4..] + iban[..4];
        var remainder = 0;
        foreach (var c in rearranged)
        {
            var chunk = char.IsDigit(c) ? (c - '0').ToString() : (c - 'A' + 10).ToString();
            foreach (var digit in chunk)
            {
                remainder = (remainder * 10 + (digit - '0')) % 97;
            }
        }

        return remainder == 1;
    }

    [Fact]
    public void Bsn_PassesElfproef_And_HasNineDigits()
    {
        // Arrange
        var nl = NL(42);

        // Act
        var values = Enumerable.Range(0, 200).Select(_ => nl.Bsn()).ToList();

        // Assert
        Assert.All(values, v => Assert.True(PassesBsnElfproef(v), $"Invalid BSN: {v}"));
    }

    [Fact]
    public void Rsin_PassesElfproef()
    {
        // Arrange
        var nl = NL(7);

        // Act
        var values = Enumerable.Range(0, 200).Select(_ => nl.Rsin()).ToList();

        // Assert
        Assert.All(values, v => Assert.True(PassesBsnElfproef(v), $"Invalid RSIN: {v}"));
    }

    [Fact]
    public void BtwNummer_HasLegalEntityShape_WithValidElfproefCore()
    {
        // Arrange
        var nl = NL();

        // Act
        var value = nl.BtwNummer();

        // Assert
        Assert.Matches(new Regex(@"^NL\d{9}B\d{2}$"), value);
        Assert.True(PassesBsnElfproef(value.Substring(2, 9)), $"BTW core is not 11-proof: {value}");
    }

    [Fact]
    public void KvkNummer_IsEightDigits()
    {
        // Arrange
        var nl = NL();

        // Act
        var value = nl.KvkNummer();

        // Assert
        Assert.Matches(new Regex(@"^\d{8}$"), value);
    }

    [Fact]
    public void Vestigingsnummer_IsTwelveDigits()
    {
        // Arrange
        var nl = NL();

        // Act
        var value = nl.Vestigingsnummer();

        // Assert
        Assert.Matches(new Regex(@"^\d{12}$"), value);
    }

    [Fact]
    public void AgbCode_IsEightDigits()
    {
        // Arrange
        var nl = NL();

        // Act
        var value = nl.AgbCode();

        // Assert
        Assert.Matches(new Regex(@"^\d{8}$"), value);
    }

    [Fact]
    public void Iban_IsValid_DutchIban()
    {
        // Arrange
        var nl = NL(123);

        // Act
        var values = Enumerable.Range(0, 200).Select(_ => nl.Iban()).ToList();

        // Assert
        Assert.All(values, v =>
        {
            Assert.Matches(new Regex(@"^NL\d{2}[A-Z]{4}\d{10}$"), v);
            Assert.True(IsValidIban(v), $"Invalid IBAN: {v}");
        });
    }

    [Fact]
    public void Bankrekeningnummer_PassesBankElfproef()
    {
        // Arrange
        var nl = NL(99);

        // Act
        var values = Enumerable.Range(0, 200).Select(_ => nl.Bankrekeningnummer()).ToList();

        // Assert
        Assert.All(values, v => Assert.True(PassesBankElfproef(v), $"Invalid bank account: {v}"));
    }

    [Fact]
    public void Postcode_HasFourDigitsSpaceTwoLetters_AndAvoidsForbiddenPairs()
    {
        // Arrange
        var nl = NL(5);

        // Act
        var values = Enumerable.Range(0, 300).Select(_ => nl.Postcode()).ToList();

        // Assert
        Assert.All(values, v =>
        {
            Assert.Matches(new Regex(@"^[1-9]\d{3} [A-Z]{2}$"), v);
            Assert.DoesNotContain(v[^2..], new[] { "SS", "SD", "SA" });
        });
    }

    [Fact]
    public void Kenteken_UsesOnlyAllowedLetters_AndKnownSidecodes()
    {
        // Arrange
        var nl = NL(11);
        var allowed = new Regex("^(" + string.Join("|", new[]
        {
            @"[BDFGHJKLMNPRSTVXZ]{3}-\d{2}-[BDFGHJKLMNPRSTVXZ]",
            @"[BDFGHJKLMNPRSTVXZ]{2}-\d{3}-[BDFGHJKLMNPRSTVXZ]",
            @"[BDFGHJKLMNPRSTVXZ]-\d{3}-[BDFGHJKLMNPRSTVXZ]{2}",
            @"\d{2}-[BDFGHJKLMNPRSTVXZ]{3}-\d",
            @"\d-[BDFGHJKLMNPRSTVXZ]{3}-\d{2}",
            @"\d{2}-[BDFGHJKLMNPRSTVXZ]{2}-\d{2}",
        }) + ")$");

        // Act
        var values = Enumerable.Range(0, 300).Select(_ => nl.Kenteken()).ToList();

        // Assert
        Assert.All(values, v => Assert.Matches(allowed, v));
    }

    [Fact]
    public void Mobiel_StartsWith06_And_HasTenDigits()
    {
        // Arrange
        var nl = NL();

        // Act
        var value = nl.Mobiel();

        // Assert
        Assert.Matches(new Regex(@"^06\d{8}$"), value);
    }

    [Fact]
    public void VastTelefoonnummer_IsTenDigits_StartingWithZeroAreaCode()
    {
        // Arrange
        var nl = NL(3);

        // Act
        var values = Enumerable.Range(0, 100).Select(_ => nl.VastTelefoonnummer()).ToList();

        // Assert
        Assert.All(values, v => Assert.Matches(new Regex(@"^0\d{9}$"), v));
    }

    [Fact]
    public void ProvincieAndGemeente_ReturnNonEmptyNames()
    {
        // Arrange
        var nl = NL();

        // Act & Assert
        Assert.False(string.IsNullOrWhiteSpace(nl.Provincie()));
        Assert.False(string.IsNullOrWhiteSpace(nl.Gemeente()));
    }

    [Fact]
    public void SameSeed_ProducesIdenticalSequences()
    {
        // Arrange
        var a = NL(2024);
        var b = NL(2024);

        // Act
        var seqA = Enumerable.Range(0, 10).Select(_ => a.Bsn()).ToList();
        var seqB = Enumerable.Range(0, 10).Select(_ => b.Bsn()).ToList();

        // Assert
        Assert.Equal(seqA, seqB);
    }

    [Fact]
    public void Token_Nl_ResolvesThroughMiniLanguage()
    {
        // Arrange & Act
        var p1 = CreateProvider(99).For<Party>()
            .With("Bsn", "nl.Bsn()")
            .With("Postcode", "nl.Postcode()")
            .Build();
        var p2 = CreateProvider(99).For<Party>()
            .With("Bsn", "nl.Bsn()")
            .With("Postcode", "nl.Postcode()")
            .Build();

        // Assert
        Assert.True(PassesBsnElfproef(p1.Bsn), $"Invalid BSN via token: {p1.Bsn}");
        Assert.Matches(new Regex(@"^[1-9]\d{3} [A-Z]{2}$"), p1.Postcode);
        Assert.Equal(p1.Bsn, p2.Bsn);
        Assert.Equal(p1.Postcode, p2.Postcode);
    }

    [Fact]
    public void CoexistsWith_XFaker_WithIndependentSeeds()
    {
        // Arrange
        var provider = new ServiceCollection()
            .AddXModelBuilder()
            .AddXFaker(1)
            .AddDutchFaker(2)
            .BuildServiceProvider()
            .GetRequiredService<IModelBuilderProvider>();

        // Act
        var bsn = provider.NL().Bsn();
        var id = provider.XFake().NextId();

        // Assert
        Assert.True(PassesBsnElfproef(bsn), $"Invalid BSN: {bsn}");
        Assert.Equal(1L, id);
    }
}
