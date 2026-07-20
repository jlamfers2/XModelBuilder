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
        /// Resolves the builder for <paramref name="modelType"/>: ALWAYS the fixed, sealed base plus the
        /// optional cross-cutting layer - never a specific/named builder. Works for any model type,
        /// whether or not a dedicated builder is registered for it. To layer a specific builder on top,
        /// use <see cref="For(Type,string)"/> or <see cref="Use(Type)"/>; to skip the cross-cutting
        /// layer, use <c>ForEmpty</c>. See README chapter 5.
        /// </summary>
        /// <param name="modelType">The model type to obtain a builder for.</param>
        /// <returns>A base + cross-cutting-layer builder for the requested model type.</returns>
        IModelBuilder For(Type modelType);

        /// <summary>
        /// Resolves the builder for <typeparamref name="TModel"/>: ALWAYS the fixed, sealed base plus the
        /// optional cross-cutting layer - never a specific/named builder. Works for any model type,
        /// whether or not a dedicated builder is registered for it. To layer a specific builder on top,
        /// use <see cref="For{TModel}(string)"/> or <see cref="Use{TModelBuilder}()"/>; to skip the
        /// cross-cutting layer, use <see cref="ForEmpty{TModel}()"/>. See README chapter 5.
        /// </summary>
        /// <typeparam name="TModel">The model type to obtain a builder for.</typeparam>
        /// <returns>A strongly-typed base + cross-cutting-layer builder for the requested model type.</returns>
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
        /// Returns a FRESH, EMPTY instance of the fixed, sealed <c>DefaultModelBuilder&lt;TModel&gt;</c>
        /// base with no staged configuration, bypassing any specific builder for <typeparamref name="TModel"/>
        /// AND suppressing the optional cross-cutting layer for it. Unlike <see cref="For{TModel}()"/>
        /// (which is base + cross-cutting layer), it runs no <c>SetDefaults</c> and no <c>Build</c>
        /// override, so it applies ONLY the values you give it - use it when you must set members (e.g.
        /// onto an existing instance via <c>Extend</c>) without any defaults or computed logic running.
        /// See README chapter 5.
        /// </summary>
        /// <typeparam name="TModel">The model type to build.</typeparam>
        /// <returns>A fresh, empty <c>DefaultModelBuilder&lt;TModel&gt;</c> that applies only the values you set on it.</returns>
        IModelBuilder<TModel> ForEmpty<TModel>() where TModel : class;

        /// <summary>
        /// Resolves a specific, compile-time known <see cref="IFaker"/> implementation directly -
        /// the typed counterpart to the dynamic "name(args)" token syntax. Throws
        /// <see cref="KeyNotFoundException"/> if no instance of <typeparamref name="TFaker"/> is
        /// registered.
        /// </summary>
        TFaker Faker<TFaker>() where TFaker : IFaker;
    }
}
