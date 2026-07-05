// Alias the Bogus library so referencing it is unambiguous inside the XModelBuilder.Bogus
// namespace (whose last segment "Bogus" would otherwise clash with the global Bogus namespace).
using BogusLib = Bogus;

namespace XModelBuilder.Fakers.Bogus;

/// <summary>
/// Extension methods on <see cref="IModelBuilderProvider"/> for conveniently resolving the underlying
/// seeded Bogus <see cref="BogusLib.Faker"/>.
/// </summary>
public static class ModelBuilderProviderExtensions
{
    /// <summary>
    /// Resolves the registered <see cref="BogusFaker"/> and returns its underlying seeded Bogus
    /// <see cref="BogusLib.Faker"/>.
    /// </summary>
    /// <param name="provider">The provider to resolve from.</param>
    /// <returns>The underlying seeded Bogus <see cref="BogusLib.Faker"/>.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no <see cref="BogusFaker"/> is registered.</exception>
    public static BogusLib.Faker Bogus(this IModelBuilderProvider provider)
        => provider.Faker<BogusFaker>().Bogus;
}