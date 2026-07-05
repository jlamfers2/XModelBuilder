namespace XModelBuilder.DependencyInjection;

/// <summary>
/// Order-independent record of which registered builder is the default for a given model type,
/// populated by <c>UseAsDefaultModelBuilder&lt;TBuilder&gt;()</c>. Registered as a singleton
/// instance and consulted by <see cref="ModelBuilderProvider"/> when more than one builder exists
/// for a model type. Keeping the choice explicit here (instead of inferring it from registration
/// order) is what makes default resolution deterministic across assemblies and scan orders.
/// </summary>
internal sealed class ModelBuilderDefaults
{
    private readonly Dictionary<Type, Type> _builderByModelType = [];

    /// <summary>
    /// Records <paramref name="builderType"/> as the default builder for <paramref name="modelType"/>,
    /// overwriting any previous choice for that model type.
    /// </summary>
    /// <param name="modelType">The model type to set the default builder for.</param>
    /// <param name="builderType">The builder type to use as the default.</param>
    public void Set(Type modelType, Type builderType) => _builderByModelType[modelType] = builderType;

    /// <summary>
    /// Returns the default builder type recorded for <paramref name="modelType"/>, or
    /// <see langword="null"/> when none has been set.
    /// </summary>
    /// <param name="modelType">The model type to look up.</param>
    /// <returns>The default builder type, or <see langword="null"/> when none is recorded.</returns>
    public Type? GetBuilderType(Type modelType) =>
        _builderByModelType.TryGetValue(modelType, out var builderType) ? builderType : null;
}
