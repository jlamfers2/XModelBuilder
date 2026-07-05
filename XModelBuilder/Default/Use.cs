namespace XModelBuilder.Default;

/// <summary>
/// Static convenience facade for resolving a specific, compile-time known builder or faker
/// implementation directly through the standalone default provider
/// (<see cref="DefaultModelBuilderProvider.Current"/>), without requiring DI.
/// </summary>
public static class Use
{
    /// <summary>
    /// Resolves a specific, compile-time known builder implementation
    /// <typeparamref name="TModelBuilder"/> directly, rather than by model type.
    /// </summary>
    /// <typeparam name="TModelBuilder">The builder type to resolve.</typeparam>
    /// <returns>The resolved builder instance.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no instance of <typeparamref name="TModelBuilder"/> is registered.</exception>
    public static TModelBuilder Builder<TModelBuilder>() where TModelBuilder : IModelBuilder => DefaultModelBuilderProvider.Current.Use<TModelBuilder>();

    /// <summary>
    /// Resolves a specific, compile-time known <see cref="IFaker"/> implementation
    /// <typeparamref name="TFaker"/> directly — the typed counterpart to the dynamic
    /// "name(args)" token syntax.
    /// </summary>
    /// <typeparam name="TFaker">The faker type to resolve.</typeparam>
    /// <returns>The resolved faker instance.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no instance of <typeparamref name="TFaker"/> is registered.</exception>
    public static TFaker Faker<TFaker>() where TFaker : IFaker => DefaultModelBuilderProvider.Current.Faker<TFaker>();
}

