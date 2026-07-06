namespace XModelBuilder.Fakers.XFaker;

/// <summary>
/// Extension methods on <see cref="IModelBuilderProvider"/> for conveniently resolving the
/// <see cref="Faker"/>.
/// </summary>
public static class ModelBuilderProviderExtensions
{
    /// <summary>
    /// Resolves the registered <see cref="Faker"/> instance from the provider. Its methods live under
    /// the namespace member <see cref="Faker.XFake"/>, so call them as
    /// <c>xmodels.XFaker().XFake.NextId()</c> (mirroring <c>xmodels.Faker&lt;BogusFaker&gt;().Bogus</c>).
    /// </summary>
    /// <param name="provider">The provider to resolve from.</param>
    /// <returns>The registered <see cref="Faker"/>; its method surface is <see cref="Faker.XFake"/>.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no <see cref="Faker"/> is registered.</exception>
    public static Faker XFaker(this IModelBuilderProvider provider)
        => provider.Faker<Faker>();
}