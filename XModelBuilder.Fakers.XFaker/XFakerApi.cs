using System.Collections.Concurrent;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace XModelBuilder.Fakers.XFaker;

/// <summary>
/// The XFaker method surface: small, dependency-free deterministic primitives that Bogus deliberately
/// does NOT do well - monotonic identity counters, stable name-based GUIDs and clock-bound ages. Meant
/// to live ALONGSIDE a rich data faker (e.g. XModelBuilder.Bogus), not to replace it.
///
/// <para>
/// This is the object exposed by the <see cref="Faker"/> namespace member <see cref="Faker.XFake"/>, so
/// its methods are addressed as <c>xfake.&lt;method&gt;()</c> from tokens (e.g. <c>"xfake.nextid()"</c>,
/// <c>"xfake.newguid(order-1)"</c>) and via <c>xprovider.XFake()</c> from C#.
/// </para>
///
/// <para>
/// All randomness flows through the injected, seeded <see cref="Random"/>; all "now"-relative
/// values flow through the injected <see cref="TimeProvider"/>. The determinism/isolation boundary is
/// the ServiceProvider: build a fresh one per test (each with the same seed) and every run reproduces -
/// separate providers get separate seeded RNGs and counters that start over, so parallel tests stay safe.
/// </para>
///
/// <para>
/// Two kinds of "deterministic" live here on purpose:
/// <list type="bullet">
/// <item><description><see cref="NewGuid()"/>, <see cref="IntBetween"/>, <see cref="DateBetween"/>,
/// <see cref="AgeBetween(int,int)"/> are RNG-based: reproducible for a given seed, but their value
/// depends on how many times the RNG has been drawn before (call order).</description></item>
/// <item><description><see cref="NewGuid(string)"/> is name-based (hash): the SAME key always maps to
/// the SAME GUID, independent of call order or parallelism. Prefer it when you want a stable id for
/// a known entity rather than "just a random id".</description></item>
/// </list>
/// </para>
/// </summary>
public class XFakerApi(Random random, TimeProvider clock)
{
    private long _counter;
    private readonly ConcurrentDictionary<string, long> _namedCounters = new(StringComparer.OrdinalIgnoreCase);

    // --- Identity / counters (RNG-independent, so order doesn't matter and values never collide) ---

    /// <summary>Next value of the default monotonic counter, starting at 1 (1, 2, 3, ...).</summary>
    public long NextId() => Interlocked.Increment(ref _counter);

    /// <summary>Next value of an independent, named monotonic counter (e.g. one per entity type).</summary>
    public long NextId(string sequence) =>
        _namedCounters.AddOrUpdate(sequence, 1L, static (_, current) => current + 1L);

    /// <summary>
    /// A human-readable sequence value: <paramref name="format"/> is a composite format string whose
    /// {0} placeholder receives the next value of a counter keyed on the format itself, e.g.
    /// <c>Sequence("INV-{0:0000}")</c> yields "INV-0001", "INV-0002", ...
    /// </summary>
    public string Sequence(string format) =>
        string.Format(CultureInfo.InvariantCulture, format, NextId(format));

    // --- GUIDs ---

    /// <summary>A deterministic (seeded-random) GUID. Reproducible for a given seed and call order.</summary>
    public Guid NewGuid()
    {
        Span<byte> bytes = stackalloc byte[16];
        random.NextBytes(bytes);
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x40); // version 4
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80); // RFC 4122 variant
        return new Guid(bytes);
    }

    /// <summary>
    /// A stable, name-based GUID: the same <paramref name="name"/> always produces the same GUID,
    /// regardless of seed, call order or parallelism. Ideal for giving a known entity a fixed id
    /// (e.g. <c>NewGuid("customer:acme")</c>). Uses MD5 as a fast, non-cryptographic hash.
    /// </summary>
    public Guid NewGuid(string name)
    {
        Span<byte> hash = stackalloc byte[16];
        MD5.HashData(Encoding.UTF8.GetBytes(name), hash);
        return new Guid(hash);
    }

    // --- Primitives ---

    /// <summary>A seeded random int in the inclusive range [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public int IntBetween(int min, int max) => random.Next(min, max == int.MaxValue ? max : max + 1);

    /// <summary>A seeded boolean that is true roughly <paramref name="truePercent"/>% of the time.</summary>
    public bool Bool(int truePercent = 50) => random.Next(1, 101) <= truePercent;

    /// <summary>A seeded random date in the inclusive range [<paramref name="min"/>, <paramref name="max"/>].</summary>
    public DateTime DateBetween(DateTime min, DateTime max)
    {
        if (max < min)
        {
            (min, max) = (max, min);
        }

        var days = (max - min).Days;
        return days <= 0 ? min : min.AddDays(random.Next(0, days + 1));
    }

    /// <summary>
    /// A birthdate for someone whose age (in whole years) at <paramref name="atDate"/> falls in
    /// [<paramref name="minAge"/>, <paramref name="maxAge"/>].
    /// </summary>
    public DateTime AgeBetween(int minAge, int maxAge, DateTime atDate) =>
        atDate.AddYears(-IntBetween(minAge, maxAge));

    /// <summary>
    /// As <see cref="AgeBetween(int,int,DateTime)"/>, but relative to "today" from the injected
    /// <see cref="TimeProvider"/> (NOT <c>DateTime.Today</c>, so it stays deterministic under a fake clock).
    /// </summary>
    public DateTime AgeBetween(int minAge, int maxAge) =>
        AgeBetween(minAge, maxAge, clock.GetLocalNow().Date);
}
