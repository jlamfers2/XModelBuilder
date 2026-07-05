namespace XModelBuilder
{
    /// <summary>
    /// Central factory for obtaining configured <see cref="IModelBuilder"/>/<see cref="IModelBuilder{TModel}"/>
    /// instances and resolving fakers. Implemented by the DI-based provider and by the standalone default
    /// provider; nested builders and faker tokens are resolved through this abstraction.
    /// </summary>
    public interface IModelBuilderProvider
    {
        /// <summary>
        /// Resolves the model builder for <paramref name="modelType"/> (its dedicated builder, or the
        /// default/fallback builder when none is registered).
        /// </summary>
        /// <param name="modelType">The model type to obtain a builder for.</param>
        /// <returns>A builder for the requested model type.</returns>
        IModelBuilder For(Type modelType);

        /// <summary>
        /// Resolves the model builder for <typeparamref name="TModel"/> (its dedicated builder, or the
        /// default/fallback builder when none is registered).
        /// </summary>
        /// <typeparam name="TModel">The model type to obtain a builder for.</typeparam>
        /// <returns>A strongly-typed builder for the requested model type.</returns>
        IModelBuilder<TModel> For<TModel>() where TModel : class;

        /// <summary>
        /// Resolves the model builder explicitly registered for <paramref name="modelType"/> under
        /// the given <see cref="ModelBuilderAttribute"/> name. Throws <see cref="KeyNotFoundException"/>
        /// if no builder with that name is registered for the type.
        /// </summary>
        IModelBuilder For(Type modelType, string name);

        /// <summary>
        /// Resolves the model builder explicitly registered for <typeparamref name="TModel"/> under
        /// the given <see cref="ModelBuilderAttribute"/> name. Throws <see cref="KeyNotFoundException"/>
        /// if no builder with that name is registered for the type.
        /// </summary>
        IModelBuilder<TModel> For<TModel>(string name) where TModel : class;

        /// <summary>
        /// Resolves a specific, compile-time known builder implementation directly, by its builder type
        /// rather than by the model type it builds.
        /// </summary>
        /// <typeparam name="TModelBuilder">The builder type to resolve.</typeparam>
        /// <returns>The resolved builder instance.</returns>
        TModelBuilder Use<TModelBuilder>() where TModelBuilder : IModelBuilder;

        /// <summary>
        /// Resolves a specific builder implementation directly, by its builder type rather than by the
        /// model type it builds.
        /// </summary>
        /// <param name="modelBuilderType">The builder type to resolve.</param>
        /// <returns>The resolved builder instance.</returns>
        IModelBuilder Use(Type modelBuilderType);

        /// <summary>
        /// Returns a FRESH, built-in <c>DefaultModelBuilder&lt;TModel&gt;</c> - always the plain built-in
        /// builder, bypassing any registered custom builder for <typeparamref name="TModel"/> as well as a
        /// configured open-generic fallback (the keyed "default" registration). Because that builder has no
        /// <c>SetDefaults</c> and no <c>Build</c> override, it applies ONLY the values you give it - use it
        /// when you must set members (e.g. onto an existing instance via <c>Extend</c>) without any
        /// builder's own defaults or computed logic running.
        /// </summary>
        IModelBuilder<TModel> NewDefaultModelBuilder<TModel>() where TModel : class;

        /// <summary>
        /// Resolves a specific, compile-time known <see cref="IFaker"/> implementation directly -
        /// the typed counterpart to the dynamic "name(args)" token syntax. Throws
        /// <see cref="KeyNotFoundException"/> if no instance of <typeparamref name="TFaker"/> is
        /// registered.
        /// </summary>
        TFaker Faker<TFaker>() where TFaker : IFaker;
    }
}
