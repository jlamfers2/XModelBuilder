using System.Globalization;
using Microsoft.Extensions.Options;
using XModelBuilder.Demo.Shop.Domain;

namespace XModelBuilder.Demo.Shop.IntegrationTests.Support;

/// <summary>
/// The demo's CROSS-CUTTING layer (README chapter 5): a single open-generic builder, registered once
/// via <c>AddCrossCuttingModelBuilder(typeof(EntityDefaults&lt;&gt;))</c>, that runs on EVERY build on
/// top of the fixed base and under any specific builder. It stamps a deterministic audit
/// <see cref="IAuditable.CreatedAt"/> on every entity that opts in by implementing
/// <see cref="IAuditable"/> - so <see cref="Customer"/>, <see cref="Product"/> and <see cref="Category"/>
/// all get a consistent timestamp without any of their builders knowing about it, and a plain
/// request DTO (which is not <see cref="IAuditable"/>) is left untouched.
///
/// <para>
/// The timestamp comes from the injected <see cref="TimeProvider"/> - frozen in tests (see
/// <see cref="FixedTimeProvider"/>) - so it is reproducible run to run. It carries the LOWEST
/// precedence: a builder or a <c>With</c> that sets <c>CreatedAt</c> explicitly still wins, and
/// <c>ForEmpty&lt;T&gt;()</c> opts out of it entirely.
/// </para>
/// </summary>
/// <typeparam name="TModel">The model type being built.</typeparam>
/// <param name="options">The culture options, forwarded to the base builder.</param>
/// <param name="xprovider">The provider used for nested builds, forwarded to the base builder.</param>
/// <param name="clock">The (frozen, in tests) clock the audit timestamp is read from.</param>
public sealed class EntityDefaults<TModel>(
        IOptions<ModelBuilderOptions> options,
        IModelBuilderProvider xprovider,
        TimeProvider clock)
    : ModelBuilder<EntityDefaults<TModel>, TModel>(options, xprovider)
    where TModel : class
{
    /// <inheritdoc/>
    protected override void SetDefaults()
    {
        // Guard: only stamp types that actually carry the cross-cutting audit concern.
        if (typeof(IAuditable).IsAssignableFrom(typeof(TModel)))
        {
            With(nameof(IAuditable.CreatedAt), clock.GetUtcNow().UtcDateTime.ToString("s", CultureInfo.InvariantCulture));
        }
    }
}
