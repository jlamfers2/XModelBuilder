namespace XModelBuilder;

/// <summary>
/// Internal contract for resolving the optional, always-applied CROSS-CUTTING layer for a model type
/// (registered via <c>AddCrossCuttingModelBuilder</c>). Implemented by the real provider so that a
/// <see cref="ModelBuilder{TBuilder,TModel}"/> can seed the cross-cutting layer's settings into itself
/// on <c>Reset()</c>, at the lowest precedence, before its own <c>SetDefaults()</c> runs. Returns
/// <see langword="null"/> when the cross-cutting layer for that very type is already being constructed
/// (recursion guard), when <c>ForEmpty</c> has suppressed it, or when none is registered.
/// </summary>
internal interface ICrossCuttingLayerProvider
{
    /// <summary>
    /// Resolves the registered cross-cutting layer for <paramref name="modelType"/>, or
    /// <see langword="null"/> when it must not be applied (recursion guard / suppressed / none registered).
    /// </summary>
    /// <param name="modelType">The model type to resolve the cross-cutting layer for.</param>
    /// <returns>The cross-cutting-layer builder, or <see langword="null"/>.</returns>
    IModelBuilder? GetCrossCuttingLayer(Type modelType);
}
