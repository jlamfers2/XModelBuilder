using Microsoft.Extensions.Options;

namespace XModelBuilder.Default;

/// <summary>
/// The fixed, sealed no-op BASE layer that every build starts from: <see cref="IModelBuilderProvider.For{TModel}"/>
/// and <see cref="IModelBuilderProvider.ForEmpty{TModel}"/> construct it, and it is what a model type
/// without a dedicated builder resolves to. It applies no defaults of its own, producing a plain
/// instance that callers configure through the fluent API. It is deliberately NOT user-replaceable -
/// cross-cutting defaults live in a SEPARATE layer registered via <c>AddCrossCuttingModelBuilder</c>
/// (see README chapter 5), which is seeded on top of this base for <c>For</c>/<c>Use</c> but skipped by
/// <c>ForEmpty</c>.
/// </summary>
/// <typeparam name="TModel">The model type this builder builds.</typeparam>
/// <param name="options">The options controlling conversion and culture behavior.</param>
/// <param name="xprovider">The provider used to resolve nested builders and fakers.</param>
public sealed class DefaultModelBuilder<TModel>(IOptions<ModelBuilderOptions> options, IModelBuilderProvider xprovider) : ModelBuilder<DefaultModelBuilder<TModel>, TModel>(options, xprovider)
    where TModel : class
{
    /// <summary>
    /// Applies no defaults; the generic fallback builder deliberately leaves the model untouched.
    /// </summary>
    protected override void SetDefaults()
    {

    }
}

