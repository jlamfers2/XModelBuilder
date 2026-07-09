namespace XModelBuilder;

/// <summary>
/// Reusable check-digit / checksum algorithms, so a faker that generates an identifier with an official
/// check can produce a VALID one in a single line. None of these are country-specific: mod-11 (the
/// Dutch "elfproef") also underpins ISBN-10, ISSN and the Norwegian birth number; Luhn (mod-10) is used
/// by credit cards and IMEI; the GS1 check digit covers EAN/GTIN/SSCC barcodes; mod-97 (ISO 7064) is
/// the IBAN check. The Dutch faker is just one caller.
/// </summary>
public static class Checksums
{
    // --- Mod-11 (weighted): the "elfproef" family, ISBN-10, ISSN, ... ---

    /// <summary>
    /// Computes the weighted sum of <paramref name="digits"/> modulo 11, normalised to the range
    /// 0-10. Each digit is multiplied by the weight at the same index; a value of 0 means the number
    /// satisfies the mod-11 check for those weights. Weights may be negative (the BSN uses <c>-1</c> for
    /// the final digit).
    /// </summary>
    /// <param name="digits">The digit characters to weigh.</param>
    /// <param name="weights">The weight per digit; must be the same length as <paramref name="digits"/>.</param>
    /// <returns>The weighted sum modulo 11, as a value in [0, 10].</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="digits"/> or <paramref name="weights"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when the lengths differ or a non-digit character is present.</exception>
    public static int Mod11WeightedSum(string digits, IReadOnlyList<int> weights)
    {
        ArgumentNullException.ThrowIfNull(digits);
        ArgumentNullException.ThrowIfNull(weights);
        if (digits.Length != weights.Count)
        {
            throw new ArgumentException("digits and weights must have the same length.", nameof(weights));
        }

        var sum = 0;
        for (var i = 0; i < digits.Length; i++)
        {
            var c = digits[i];
            if (c is < '0' or > '9')
            {
                throw new ArgumentException($"'{c}' is not a digit.", nameof(digits));
            }

            sum += (c - '0') * weights[i];
        }

        // C# '%' keeps the sign of the dividend; normalise to a non-negative remainder.
        return ((sum % 11) + 11) % 11;
    }

    /// <summary>
    /// Whether <paramref name="digits"/> satisfies the mod-11 check for the given
    /// <paramref name="weights"/> (i.e. <see cref="Mod11WeightedSum"/> is 0).
    /// </summary>
    /// <param name="digits">The digit characters to check.</param>
    /// <param name="weights">The weight per digit.</param>
    /// <returns><see langword="true"/> when the weighted sum is divisible by 11.</returns>
    public static bool Mod11IsValid(string digits, IReadOnlyList<int> weights) =>
        Mod11WeightedSum(digits, weights) == 0;

    // --- Luhn (mod-10): credit cards, IMEI, ... ---

    /// <summary>
    /// The Luhn (mod-10) check digit for a number that does NOT yet include its check digit.
    /// </summary>
    /// <param name="numberWithoutCheck">The digits preceding the check digit.</param>
    /// <returns>The check digit (0-9) that makes the full number Luhn-valid.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="numberWithoutCheck"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a non-digit character is present.</exception>
    public static int LuhnCheckDigit(string numberWithoutCheck)
    {
        var sum = LuhnSum(numberWithoutCheck, oddPositionDoubles: true);
        return (10 - (sum % 10)) % 10;
    }

    /// <summary>
    /// Whether <paramref name="number"/> (including its trailing check digit) is Luhn-valid.
    /// </summary>
    /// <param name="number">The full number including the check digit.</param>
    /// <returns><see langword="true"/> when the number passes the Luhn check.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="number"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a non-digit character is present.</exception>
    public static bool LuhnIsValid(string number) =>
        LuhnSum(number, oddPositionDoubles: false) % 10 == 0;

    private static int LuhnSum(string number, bool oddPositionDoubles)
    {
        ArgumentNullException.ThrowIfNull(number);

        var sum = 0;
        // Walk right-to-left; when computing a check digit the rightmost body digit is doubled, when
        // validating (the check digit already present) the doubling starts one position further left.
        var doubleNext = oddPositionDoubles;
        for (var i = number.Length - 1; i >= 0; i--)
        {
            var c = number[i];
            if (c is < '0' or > '9')
            {
                throw new ArgumentException($"'{c}' is not a digit.", nameof(number));
            }

            var d = c - '0';
            if (doubleNext)
            {
                d *= 2;
                if (d > 9)
                {
                    d -= 9;
                }
            }

            sum += d;
            doubleNext = !doubleNext;
        }

        return sum;
    }

    // --- GS1 (mod-10, 3/1 weighting): EAN, GTIN, SSCC ---

    /// <summary>
    /// The GS1 check digit for a barcode body without its check digit (works for GTIN-8/12/13/14 and
    /// SSCC, as the weighting is right-aligned: the rightmost body digit gets weight 3, alternating).
    /// </summary>
    /// <param name="numberWithoutCheck">The barcode digits preceding the check digit.</param>
    /// <returns>The check digit (0-9).</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="numberWithoutCheck"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a non-digit character is present.</exception>
    public static int Gs1CheckDigit(string numberWithoutCheck)
    {
        ArgumentNullException.ThrowIfNull(numberWithoutCheck);

        var sum = 0;
        for (var i = 0; i < numberWithoutCheck.Length; i++)
        {
            var c = numberWithoutCheck[numberWithoutCheck.Length - 1 - i];
            if (c is < '0' or > '9')
            {
                throw new ArgumentException($"'{c}' is not a digit.", nameof(numberWithoutCheck));
            }

            sum += (c - '0') * (i % 2 == 0 ? 3 : 1);
        }

        return (10 - (sum % 10)) % 10;
    }

    /// <summary>
    /// Whether <paramref name="number"/> (including its trailing GS1 check digit) is valid.
    /// </summary>
    /// <param name="number">The full barcode including the check digit.</param>
    /// <returns><see langword="true"/> when the check digit matches.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="number"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="number"/> is empty or non-numeric.</exception>
    public static bool Gs1IsValid(string number)
    {
        ArgumentNullException.ThrowIfNull(number);
        if (number.Length == 0)
        {
            throw new ArgumentException("number must not be empty.", nameof(number));
        }

        return Gs1CheckDigit(number[..^1]) == number[^1] - '0';
    }

    // --- Mod-97 (ISO 7064 / ISO 13616): IBAN ---

    /// <summary>
    /// Computes an ISO 7064 mod-97 remainder over <paramref name="value"/>, expanding letters to numbers
    /// (<c>A</c>=10 ... <c>Z</c>=35) exactly as IBAN validation requires. It is computed iteratively, so
    /// no big-integer type is needed. To build a valid IBAN, append <c>&lt;country&gt;00</c> to the BBAN,
    /// call this, and use <c>98 - result</c> as the two check digits.
    /// </summary>
    /// <param name="value">A string of digits and/or uppercase letters.</param>
    /// <returns>The remainder modulo 97, a value in [0, 96].</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="value"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when a character is neither a digit nor an uppercase A-Z letter.</exception>
    public static int Mod97(string value)
    {
        ArgumentNullException.ThrowIfNull(value);

        var remainder = 0;
        foreach (var c in value)
        {
            if (c is >= '0' and <= '9')
            {
                remainder = (remainder * 10 + (c - '0')) % 97;
            }
            else if (c is >= 'A' and <= 'Z')
            {
                var n = c - 'A' + 10; // two decimal digits
                remainder = (remainder * 10 + n / 10) % 97;
                remainder = (remainder * 10 + n % 10) % 97;
            }
            else
            {
                throw new ArgumentException($"'{c}' is not a digit or an uppercase A-Z letter.", nameof(value));
            }
        }

        return remainder;
    }
}
