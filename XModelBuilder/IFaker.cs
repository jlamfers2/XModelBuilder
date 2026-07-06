namespace XModelBuilder;

/// <summary>
/// Marker interface for classes whose public methods can be invoked from the mini-DSL via the
/// "name(args)" token syntax, e.g. "AgeBetween(1,20)". Register implementations via
/// XModelBuilderServiceCollectionExtensions.AddFaker (DI) or
/// XModelBuilder.Default.DefaultModelBuilderProvider.AddFaker (standalone).
///
/// <para>
/// NAMESPACE CONVENTION (recommended for every faker): rather than expose its methods at the top
/// level, a faker exposes a single gettable member whose NAME is the faker's namespace and returns
/// the object that actually holds the methods. Tokens then address the methods through that namespace
/// - e.g. XFaker exposes <c>XFake</c> (tokens <c>"xfake.nextid()"</c>) and the Bogus integration exposes
/// <c>Bogus</c> (tokens <c>"bogus.name.firstname()"</c>). Because a token's first segment selects the
/// owning faker, giving each faker its own namespace keeps their tokens from colliding, and keeps the
/// top-level token space clean.
/// </para>
/// </summary>
public interface IFaker
{
}
