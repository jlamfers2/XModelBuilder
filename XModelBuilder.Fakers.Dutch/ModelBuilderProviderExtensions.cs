namespace XModelBuilder.Fakers.Dutch;

/// <summary>
/// Extension methods on <see cref="IModelBuilderProvider"/> for conveniently resolving the
/// <see cref="DutchFaker"/>.
/// </summary>
public static class ModelBuilderProviderExtensions
{
    /// <summary>
    /// Resolves the registered <see cref="DutchFakerApi"/> instance from the provider. Its methods live
    /// under the namespace member <see cref="DutchFaker.Nl"/>, so call them as
    /// <c>xprovider.Nl().Bsn()</c>.
    /// </summary>
    /// <param name="provider">The provider to resolve from.</param>
    /// <returns>The registered <see cref="DutchFakerApi"/>; its method surface is <see cref="DutchFaker.Nl"/>.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no <see cref="DutchFaker"/> is registered.</exception>
    public static DutchFakerApi Nl(this IModelBuilderProvider provider)
        => provider.Faker<DutchFaker>().Nl;
}
