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
        ArgumentOutOfRangeException.ThrowIfNegative(count);

        var result = new List<TModel>(count);

        for (var i = 0; i < count; i++)
        {
            result.Add(builder.Build());
        }

        return result;
    }
}
