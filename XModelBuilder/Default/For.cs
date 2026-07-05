namespace XModelBuilder.Default;

/// <summary>
/// Static convenience facade that returns a fluent <see cref="IModelBuilder{TModel}"/> for a model
/// type — using the type's registered builder, or the default builder when none is registered —
/// through the standalone default provider (<see cref="DefaultModelBuilderProvider.Current"/>),
/// without requiring DI. Usage: <c>For.Model&lt;Person&gt;().With(...).Build()</c>.
/// </summary>
public static class For
{
    /// <summary>
    /// Returns a fluent builder for <typeparamref name="TModel"/>, ready to configure and build.
    /// </summary>
    /// <typeparam name="TModel">The model type to build.</typeparam>
    /// <returns>An <see cref="IModelBuilder{TModel}"/> for the model type.</returns>
    public static IModelBuilder<TModel> Model<TModel>() where TModel : class => DefaultModelBuilderProvider.Current.For<TModel>();
}

