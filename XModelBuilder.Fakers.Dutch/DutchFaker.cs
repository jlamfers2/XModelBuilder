namespace XModelBuilder.Fakers.Dutch;

/// <summary>
/// The registered <see cref="IFaker"/> for the Dutch faker. Following the faker NAMESPACE convention,
/// it exposes its whole method surface under a single namespace member - <see cref="NL"/> - so tokens
/// address it as <c>"nl.&lt;method&gt;()"</c> (e.g. <c>"nl.bsn()"</c>, <c>"nl.postcode()"</c>) and NOT
/// at the top level. This mirrors how XFaker exposes everything under <c>"xfake."</c> and the Bogus
/// integration under <c>"bogus."</c>. The namespace keeps the Dutch tokens from colliding with other
/// fakers' tokens.
///
/// <para>
/// Register it with <c>AddDutchFaker(seed)</c>. From C#, reach the methods via the <see cref="NL"/>
/// property, e.g. <c>xprovider.NL().Bsn()</c>. The actual methods live on <see cref="DutchFakerApi"/>.
/// </para>
/// </summary>
public class DutchFaker(Random random) : IFaker
{
    /// <summary>
    /// The Dutch namespace root: every Dutch faker method lives here. Tokens use the (case-insensitive)
    /// <c>"nl."</c> prefix; C# uses this property directly, e.g. <c>xprovider.NL().Postcode()</c>.
    /// </summary>
    public DutchFakerApi NL { get; } = new DutchFakerApi(random);
}
