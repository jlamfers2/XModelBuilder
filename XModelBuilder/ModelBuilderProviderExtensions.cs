namespace XModelBuilder;

/// <summary>
/// Extension methods on <see cref="IModelBuilderProvider"/> for building batches of models
/// (<c>BuildMany</c>), each through its own fresh builder, optionally by named builder and/or with
/// per-index configuration.
/// </summary>
public static class ModelBuilderProviderExtensions
{
    /// <summary>
    /// Builds <paramref name="count"/> independent <typeparamref name="TModel"/> instances,
    /// each via its own fresh builder (so each gets its own SetDefaults()).
    /// </summary>
    public static IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilderProvider provider, int count)
        where TModel : class
    {
        return provider.BuildMany<TModel>(count, static (builder, _) => builder);
    }

    /// <summary>
    /// Builds <paramref name="count"/> independent <typeparamref name="TModel"/> instances. Each
    /// iteration gets its own fresh builder (from <see cref="IModelBuilderProvider.For{TModel}()"/>),
    /// which <paramref name="configure"/> can further configure based on the (zero-based) index -
    /// e.g. to give each instance a distinct name.
    /// </summary>
    public static IReadOnlyList<TModel> BuildMany<TModel>(
        this IModelBuilderProvider provider,
        int count,
        Func<IModelBuilder<TModel>, int, IModelBuilder<TModel>> configure)
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(provider);

        return BuildMany(count, configure, i => provider.For<TModel>());
    }

    /// <summary>
    /// Like <see cref="BuildMany{TModel}(IModelBuilderProvider, int)"/>, but each fresh builder is
    /// resolved through the model builder explicitly registered under <see cref="ModelBuilderAttribute"/>
    /// name <paramref name="modelBuilderName"/>, instead of whichever builder currently counts as
    /// "default" for <typeparamref name="TModel"/>.
    /// </summary>
    public static IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilderProvider provider, int count, string modelBuilderName)
        where TModel : class
    {
        return provider.BuildMany<TModel>(count, modelBuilderName, static (builder, _) => builder);
    }

    /// <summary>
    /// Combines the named-builder resolution of <see cref="BuildMany{TModel}(IModelBuilderProvider, int, string)"/>
    /// with the per-index configuration of <see cref="BuildMany{TModel}(IModelBuilderProvider, int, Func{IModelBuilder{TModel},int,IModelBuilder{TModel}})"/>.
    /// </summary>
    public static IReadOnlyList<TModel> BuildMany<TModel>(
        this IModelBuilderProvider provider,
        int count,
        string modelBuilderName,
        Func<IModelBuilder<TModel>, int, IModelBuilder<TModel>> configure)
        where TModel : class
    {
        ArgumentNullException.ThrowIfNull(provider);

        return BuildMany(count, configure, i => provider.For<TModel>(modelBuilderName));
    }

    private static IReadOnlyList<TModel> BuildMany<TModel>(
        int count,
        Func<IModelBuilder<TModel>, int, IModelBuilder<TModel>> configure,
        Func<int, IModelBuilder<TModel>> resolveBuilder)
    {
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var result = new List<TModel>(count);

        for (var i = 0; i < count; i++)
        {
            var builder = configure(resolveBuilder(i), i);
            result.Add(builder.Build());
        }

        return result;
    }
}
