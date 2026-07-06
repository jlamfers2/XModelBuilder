using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using XModelBuilder.DependencyInjection;

namespace XModelBuilder.Fakers.XFaker;

/// <summary>
/// <see cref="IServiceCollection"/> extension methods for registering the dependency-free
/// <see cref="Faker"/> together with a seeded <see cref="Random"/> and a <see cref="TimeProvider"/>,
/// so its deterministic tokens are reproducible (<c>AddXFaker</c>).
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="Faker"/> together with a seeded <see cref="Random"/> so its tokens
    /// (addressed under the "xfake." namespace: xfake.NextId(), xfake.NewGuid(), xfake.AgeBetween(...), ...)
    /// produce deterministic, reproducible data.
    ///
    /// <para>
    /// Determinism is the GUARANTEE here, not a lucky side effect: the seeded RNG is always
    /// registered. The faker and its <see cref="Random"/> follow the <see cref="XModelBuilderIsolation"/>
    /// configured on <c>AddXModelBuilder</c> (Shared → one set per container; PerScope → a fresh,
    /// re-seeded set per DI scope). The wiring is order-independent: it does not matter whether
    /// AddXFaker is called before or after AddXModelBuilder.
    /// </para>
    ///
    /// <para>
    /// A <see cref="TimeProvider"/> is registered with TryAdd only (always Singleton; it is
    /// stateless), so a fake clock you registered earlier wins and ages stay deterministic.
    /// </para>
    /// </summary>
    public static IServiceCollection AddXFaker(
        this IServiceCollection services,
        int seed = 8675309)
    {
        return services.AddXFaker(_ => seed);
    }

    /// <summary>
    /// As <see cref="AddXFaker(IServiceCollection,int)"/>, but the seed is produced from the
    /// <see cref="IServiceProvider"/> at resolution time. Under
    /// <see cref="XModelBuilderIsolation.PerScope"/> the factory runs once per DI scope with that
    /// scope's provider, so you can derive a stable-yet-distinct seed per scope - e.g. per BDD
    /// scenario from <c>ScenarioContext.ScenarioInfo.Title</c>.
    /// </summary>
    public static IServiceCollection AddXFaker(
        this IServiceCollection services,
        Func<IServiceProvider, int> seedFactory)
    {
        ArgumentNullException.ThrowIfNull(seedFactory);

        services.TryAddSingleton(TimeProvider.System);
        return services.AddIsolatedXModelBuilderServices((s, lifetime) =>
        {
            s.Add(new ServiceDescriptor(typeof(Random), sp => new Random(seedFactory(sp)), lifetime));
            s.AddFaker<Faker>(lifetime);
        });
    }
}
