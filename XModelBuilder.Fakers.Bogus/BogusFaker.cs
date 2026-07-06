// Alias the Bogus library so referencing it is unambiguous inside the XModelBuilder.Bogus
// namespace (whose last segment "Bogus" would otherwise clash with the global Bogus namespace).
using BogusLib = Bogus;

namespace XModelBuilder.Fakers.Bogus;

/// <summary>
/// Exposes a seeded Bogus <see cref="BogusLib.Faker"/> to XModelBuilder. The whole Bogus surface is
/// reachable WITHOUT any hand-written adapter methods, thanks to deep-path faker resolution
/// (see <c>FakerInvoker</c>):
///
/// <list type="bullet">
/// <item><description>
/// From tokens, as a member path that starts at the <see cref="Bogus"/> property:
/// <c>"bogus.name.firstname()"</c>, <c>"bogus.address.city()"</c>, <c>"bogus.internet.email()"</c>.
/// The terminal segment is invoked as a method, or - when it is a property, e.g. Bogus' pre-built
/// person - read as one: <c>"bogus.person.firstname()"</c>.
/// </description></item>
/// <item><description>
/// From typed C#, directly: <c>xprovider.Faker&lt;BogusFaker&gt;().Bogus.Address.County()</c>.
/// </description></item>
/// </list>
///
/// The <c>bogus.</c> root keeps every generator namespaced, so its tokens never collide with your
/// own fakers or with <c>XFaker</c>.
/// </summary>
public class BogusFaker(BogusLib.Faker faker) : IFaker
{
    /// <summary>The underlying seeded Bogus faker; the root of the <c>bogus.*</c> token paths.</summary>
    public BogusLib.Faker Bogus => faker;
}
