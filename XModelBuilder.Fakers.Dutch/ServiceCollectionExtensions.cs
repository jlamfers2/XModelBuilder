using Microsoft.Extensions.DependencyInjection;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.Fakers.Dutch;

/// <summary>
/// <see cref="IServiceCollection"/> extension methods for registering the dependency-free
/// <see cref="DutchFaker"/> together with a seeded <see cref="Random"/>, so its Netherlands-specific
/// tokens are reproducible (<c>AddDutchFaker</c>).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="DutchFaker"/> together with a seeded <see cref="Random"/> so its tokens
    /// (addressed under the "nl." namespace: nl.Bsn(), nl.Postcode(), nl.Kenteken(), ...) produce
    /// deterministic, reproducible data.
    ///
    /// <para>
    /// The faker and its <see cref="Random"/> follow the <see cref="XModelBuilderIsolation"/> configured
    /// on <c>AddXModelBuilder</c> (Shared → one set per container; PerScope → a fresh, re-seeded set per
    /// DI scope). The wiring is order-independent: it does not matter whether AddDutchFaker is called
    /// before or after AddXModelBuilder.
    /// </para>
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="seed">The seed for the deterministic <see cref="Random"/>.</param>
    /// <returns>The same service collection, to allow call chaining.</returns>
    public static IServiceCollection AddDutchFaker(
        this IServiceCollection services,
        int seed = 8675309)
    {
        return services.AddDutchFaker(_ => seed);
    }

    /// <summary>
    /// As <see cref="AddDutchFaker(IServiceCollection,int)"/>, but the seed is produced from the
    /// <see cref="IServiceProvider"/> at resolution time. Under
    /// <see cref="XModelBuilderIsolation.PerScope"/> the factory runs once per DI scope with that
    /// scope's provider, so you can derive a stable-yet-distinct seed per scope - e.g. per BDD scenario
    /// from <c>ScenarioContext.ScenarioInfo.Title</c>.
    /// </summary>
    /// <param name="services">The service collection to add to.</param>
    /// <param name="seedFactory">A factory that produces the seed from the resolving provider.</param>
    /// <returns>The same service collection, to allow call chaining.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="seedFactory"/> is null.</exception>
    public static IServiceCollection AddDutchFaker(
        this IServiceCollection services,
        Func<IServiceProvider, int> seedFactory)
    {
        ArgumentNullException.ThrowIfNull(seedFactory);

        // Like the Bogus integration, register the faker with its OWN dedicated Random rather than a
        // shared typeof(Random) service. That keeps its seed independent from XFaker's (which does
        // register typeof(Random)), so the two can coexist with different seeds without interfering.
        return services.AddIsolatedXModelBuilderServices((s, lifetime) =>
        {
            s.Add(new ServiceDescriptor(
                typeof(DutchFaker),
                sp => new DutchFaker(new Random(seedFactory(sp))),
                lifetime));
            s.Add(new ServiceDescriptor(
                typeof(IFaker),
                sp => sp.GetRequiredService<DutchFaker>(),
                lifetime));
        });
    }
}
