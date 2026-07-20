using System.Text;

namespace XModelBuilder;

/// <summary>
/// Small, general-purpose building blocks for writing deterministic fakers on top of a seeded
/// <see cref="Random"/>. These are the primitives the built-in fakers (e.g. the Dutch faker) are built
/// from; they are public so your own <see cref="IFaker"/> implementations can reuse them and keep each
/// generator a one-liner. Everything flows through the supplied <see cref="Random"/>, so results stay
/// reproducible for a given seed.
/// </summary>
public static class RandomExtensions
{
    /// <summary>
    /// Produces a string of exactly <paramref name="count"/> random decimal digits (leading zeros
    /// allowed).
    /// </summary>
    /// <param name="random">The seeded random source.</param>
    /// <param name="count">The number of digits to produce.</param>
    /// <returns>A string of <paramref name="count"/> digit characters.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="random"/> is null.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
    public static string Digits(this Random random, int count)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var sb = new StringBuilder(count);
        for (var i = 0; i < count; i++)
        {
            sb.Append((char)('0' + random.Next(10)));
        }

        return sb.ToString();
    }

    /// <summary>
    /// Returns a uniformly random element from <paramref name="items"/>.
    /// </summary>
    /// <typeparam name="T">The element type.</typeparam>
    /// <param name="random">The seeded random source.</param>
    /// <param name="items">The non-empty list to pick from.</param>
    /// <returns>One randomly chosen element.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="random"/> or <paramref name="items"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="items"/> is empty.</exception>
    public static T PickFrom<T>(this Random random, IReadOnlyList<T> items)
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count == 0)
        {
            throw new ArgumentException("The collection to pick from must not be empty.", nameof(items));
        }

        return items[random.Next(items.Count)];
    }

    /// <summary>
    /// Fills a template string: each <c>'#'</c> becomes a random digit, each <c>'?'</c> a random letter
    /// from <paramref name="letters"/>, and every other character is copied verbatim. For example
    /// <c>FromPattern("??-###-?", "BDFGHJKLMNPRSTVXZ")</c> yields a Dutch number plate such as
    /// <c>"GK-123-D"</c>. (The <c>#</c>/<c>?</c> convention mirrors Bogus' <c>Randomizer.Replace</c>.)
    /// </summary>
    /// <param name="random">The seeded random source.</param>
    /// <param name="pattern">The template; <c>#</c> = digit, <c>?</c> = letter, anything else literal.</param>
    /// <param name="letters">The alphabet the <c>?</c> placeholders draw from. Defaults to A-Z.</param>
    /// <returns>The filled string.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="random"/> or <paramref name="pattern"/> is null.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="letters"/> is null or empty.</exception>
    public static string FromPattern(
        this Random random,
        string pattern,
        string letters = "ABCDEFGHIJKLMNOPQRSTUVWXYZ")
    {
        ArgumentNullException.ThrowIfNull(random);
        ArgumentNullException.ThrowIfNull(pattern);
        ArgumentException.ThrowIfNullOrEmpty(letters);

        var sb = new StringBuilder(pattern.Length);
        foreach (var c in pattern)
        {
            sb.Append(c switch
            {
                '#' => (char)('0' + random.Next(10)),
                '?' => letters[random.Next(letters.Length)],
                _ => c,
            });
        }

        return sb.ToString();
    }
}
