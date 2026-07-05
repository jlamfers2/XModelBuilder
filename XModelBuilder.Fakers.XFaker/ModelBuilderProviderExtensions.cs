namespace XModelBuilder.Fakers.XFaker;

/// <summary>
/// Extension methods on <see cref="IModelBuilderProvider"/> for conveniently resolving the
/// <see cref="Faker"/>.
/// </summary>
public static class ModelBuilderProviderExtensions
{
    /// <summary>
    /// Resolves the registered <see cref="Faker"/> instance from the provider.
    /// </summary>
    /// <param name="provider">The provider to resolve from.</param>
    /// <returns>The registered <see cref="Faker"/>.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no <see cref="Faker"/> is registered.</exception>
    public static Faker XFaker(this IModelBuilderProvider provider)
        => provider.Faker<Faker>();
}