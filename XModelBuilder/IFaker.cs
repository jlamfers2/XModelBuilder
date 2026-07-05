namespace XModelBuilder;

/// <summary>
/// Marker interface for classes whose public methods can be invoked from the mini-DSL via the
/// "name(args)" token syntax, e.g. "AgeBetween(1,20)". Register implementations via
/// XModelBuilderServiceCollectionExtensions.AddFaker (DI) or
/// XModelBuilder.Default.DefaultModelBuilderProvider.AddFaker (standalone).
/// </summary>
public interface IFaker
{
}
