namespace XModelBuilder.Default;

/// <summary>
/// Static convenience facade for building models through the standalone default provider
/// (<see cref="DefaultModelBuilderProvider.Current"/>), without needing DI or an explicit
/// <see cref="IModelBuilderProvider"/> reference. Offers one-liners for a single model
/// (<see cref="Model{TModel}"/>) and for batches (the <c>Models</c> overloads).
/// </summary>
public static class Create
{
    /// <summary>
    /// Builds a single model of the given type using its default builder.
    /// </summary>
    /// <typeparam name="TModel">The model type to build.</typeparam>
    /// <returns>A newly built <typeparamref name="TModel"/> instance.</returns>
    public static TModel Model<TModel>() where TModel : class => DefaultModelBuilderProvider.Current.For<TModel>().Build();

    /// <summary>
    /// Builds <paramref name="count"/> models of the given type using its default builder.
    /// </summary>
    /// <typeparam name="TModel">The model type to build.</typeparam>
    /// <param name="count">The number of models to build.</param>
    /// <returns>A read-only list of the built models.</returns>
    public static IReadOnlyList<TModel> Models<TModel>(int count) where TModel : class =>
        DefaultModelBuilderProvider.Current.BuildMany<TModel>(count);

    /// <summary>
    /// Builds <paramref name="count"/> models of the given type using the named builder.
    /// </summary>
    /// <typeparam name="TModel">The model type to build.</typeparam>
    /// <param name="count">The number of models to build.</param>
    /// <param name="modelBuilderName">The registered name of the builder to use.</param>
    /// <returns>A read-only list of the built models.</returns>
    public static IReadOnlyList<TModel> Models<TModel>(int count, string modelBuilderName) where TModel : class =>
        DefaultModelBuilderProvider.Current.BuildMany<TModel>(count, modelBuilderName);

    /// <summary>
    /// Builds <paramref name="count"/> models of the given type using its default builder, applying
    /// the <paramref name="configure"/> callback to each builder (receiving the zero-based index).
    /// </summary>
    /// <typeparam name="TModel">The model type to build.</typeparam>
    /// <param name="count">The number of models to build.</param>
    /// <param name="configure">A callback that configures the builder for each item, given the builder and its zero-based index.</param>
    /// <returns>A read-only list of the built models.</returns>
    public static IReadOnlyList<TModel> Models<TModel>(int count, Func<IModelBuilder<TModel>, int, IModelBuilder<TModel>> configure) where TModel : class =>
        DefaultModelBuilderProvider.Current.BuildMany(count, configure);

    /// <summary>
    /// Builds <paramref name="count"/> models of the given type using the named builder, applying
    /// the <paramref name="configure"/> callback to each builder (receiving the zero-based index).
    /// </summary>
    /// <typeparam name="TModel">The model type to build.</typeparam>
    /// <param name="count">The number of models to build.</param>
    /// <param name="modelBuilderName">The registered name of the builder to use.</param>
    /// <param name="configure">A callback that configures the builder for each item, given the builder and its zero-based index.</param>
    /// <returns>A read-only list of the built models.</returns>
    public static IReadOnlyList<TModel> Models<TModel>(int count, string modelBuilderName, Func<IModelBuilder<TModel>, int, IModelBuilder<TModel>> configure) where TModel : class =>
        DefaultModelBuilderProvider.Current.BuildMany(count, modelBuilderName, configure);
}

