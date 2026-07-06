namespace XModelBuilder.Fakers.XFaker;

/// <summary>
/// Extension methods on <see cref="IModelBuilderProvider"/> for conveniently resolving the
/// <see cref="Faker"/>.
/// </summary>
public static class ModelBuilderProviderExtensions
{
    /// <summary>
    /// Resolves the registered <see cref="XFakerApi"/> instance from the provider. Its methods live under
    /// the namespace member <see cref="Faker.XFake"/>, so call them as
    /// <c>xprovider.XFake().NextId()</c> 
    /// </summary>
    /// <param name="provider">The provider to resolve from.</param>
    /// <returns>The registered <see cref="XFakerApi"/>; its method surface is <see cref="Faker.XFake"/>.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no <see cref="Faker"/> is registered.</exception>
    public static XFakerApi XFake(this IModelBuilderProvider provider)
        => provider.Faker<Faker>().XFake;
}