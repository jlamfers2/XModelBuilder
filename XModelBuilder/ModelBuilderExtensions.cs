namespace XModelBuilder;

/// <summary>
/// Extension methods on <see cref="IModelBuilder{TModel}"/> for building multiple instances from a
/// single, already-configured builder.
/// </summary>
public static class ModelBuilderExtensions
{
    /// <summary>
    /// Builds <paramref name="count"/> instances by calling <see cref="IModelBuilder{TModel}.Build"/>
    /// repeatedly on the SAME builder instance. Any configuration already applied to
    /// <paramref name="builder"/> (e.g. via With(...)) is shared across every instance; any value
    /// set via a value-factory or a string-path token (including faker calls) is re-evaluated for
    /// every Build() call, so those parts can still vary per instance.
    /// </summary>
    /// <typeparam name="TModel">The model type being built.</typeparam>
    /// <param name="builder">The configured builder to build from repeatedly.</param>
    /// <param name="count">The number of instances to build.</param>
    /// <returns>A read-only list of the built instances.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
    public static IReadOnlyList<TModel> BuildMany<TModel>(this IModelBuilder<TModel> builder, int count)
    {
        ArgumentNullException.ThrowIfNull(builder);

        return builder.BuildMany(count, static (b, _) => b);
    }

    /// <summary>
    /// Builds <paramref name="count"/> instances from the SAME <paramref name="builder"/>, applying
    /// <paramref name="configure"/> before each <see cref="IModelBuilder{TModel}.Build"/> call so the
    /// configuration can vary per (zero-based) index - e.g. to give each instance a distinct name.
    /// <para>
    /// This is the builder-level counterpart of the provider-level per-index overload on
    /// <see cref="ModelBuilderProviderExtensions"/>: that one resolves a FRESH builder for every
    /// index, whereas this one reuses this single, already-configured builder (so any base
    /// configuration is shared across all instances). As with the single-argument overload,
    /// value-factories and string-path tokens (including faker calls) are re-evaluated on every
    /// Build() call.
    /// </para>
    /// </summary>
    /// <typeparam name="TModel">The model type being built.</typeparam>
    /// <param name="builder">The configured builder to build from repeatedly.</param>
    /// <param name="count">The number of instances to build.</param>
    /// <param name="configure">
    /// Applied to <paramref name="builder"/> before each Build(), receiving the (zero-based) index;
    /// returns the builder to build from (typically the same instance).
    /// </param>
    /// <returns>A read-only list of the built instances.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="builder"/> or <paramref name="configure"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">Thrown when <paramref name="count"/> is negative.</exception>
    public static IReadOnlyList<TModel> BuildMany<TModel>(
        this IModelBuilder<TModel> builder,
        int count,
        Func<IModelBuilder<TModel>, int, IModelBuilder<TModel>> configure)
    {
        ArgumentNullException.ThrowIfNull(builder);
        ArgumentNullException.ThrowIfNull(configure);
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var result = new List<TModel>(count);

        for (var i = 0; i < count; i++)
        {
            result.Add(configure(builder, i).Build());
        }

        return result;
    }
}
