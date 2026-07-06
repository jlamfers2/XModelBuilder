namespace XModelBuilder.Fakers.XFaker;

/// <summary>
/// The registered <see cref="IFaker"/> for XFaker. Following the faker NAMESPACE convention, it
/// exposes its whole method surface under a single namespace member - <see cref="XFake"/> - so tokens
/// address it as <c>"xfake.&lt;method&gt;()"</c> (e.g. <c>"xfake.nextid()"</c>, <c>"xfake.newguid(order-1)"</c>)
/// and NOT at the top level. This mirrors how XModelBuilder.Bogus exposes everything under
/// <c>"bogus."</c>. The namespace keeps XFaker's tokens from colliding with other fakers' tokens.
///
/// <para>
/// Register it with <c>AddXFaker(seed)</c>. From C#, reach the methods via the <see cref="XFake"/>
/// property, e.g. <c>xprovider.XFake().NextId()</c>. The actual methods live on
/// <see cref="XFakerApi"/>.
/// </para>
/// </summary>
public class Faker(Random random, TimeProvider clock) : IFaker
{
    /// <summary>
    /// The XFaker namespace root: every XFaker method lives here. Tokens use the (case-insensitive)
    /// <c>"xfake."</c> prefix; C# uses this property directly, e.g. <c>xprovider.XFake().NextId()</c>.
    /// </summary>
    public XFakerApi XFake { get; } = new XFakerApi(random, clock);
}
