using Microsoft.Extensions.Options;

namespace XModelBuilder.Default;

/// <summary>
/// The generic fallback <see cref="ModelBuilder{TBuilder,TModel}"/> used for any model type that
/// has no dedicated, custom-registered builder. It applies no defaults of its own, producing a
/// plain instance that callers configure through the fluent API.
/// </summary>
/// <typeparam name="TModel">The model type this builder builds.</typeparam>
/// <param name="options">The options controlling conversion and culture behavior.</param>
/// <param name="xmodels">The provider used to resolve nested builders and fakers.</param>
public sealed class DefaultModelBuilder<TModel>(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xmodels) : ModelBuilder<DefaultModelBuilder<TModel>, TModel>(options, xmodels)
    where TModel : class
{
    /// <summary>
    /// Applies no defaults; the generic fallback builder deliberately leaves the model untouched.
    /// </summary>
    protected override void SetDefaults()
    {

    }
}

