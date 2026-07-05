using Microsoft.Extensions.DependencyInjection;
using XModelBuilder.DependencyInjection;
using BogusLib = Bogus;

namespace XModelBuilder.Fakers.Bogus;

/// <summary>
/// <see cref="IServiceCollection"/> extension methods for registering the <see cref="BogusFaker"/>
/// together with a per-instance seeded Bogus <see cref="BogusLib.Faker"/>, so its <c>bogus.*</c>
/// tokens produce deterministic, reproducible data (<c>AddBogusFaker</c>).
/// </summary>
public static class BogusFakerServiceCollectionExtensions
{
    /// <summary>
    /// Registers <see cref="BogusFaker"/> together with a per-instance seeded Bogus
    /// <see cref="BogusLib.Faker"/>, so its Bogus*-tokens produce deterministic, reproducible data.
    ///
    /// <para>
    /// Note that Bogus has its OWN randomizer (separate from <see cref="System.Random"/>): we seed
    /// it per instance via <c>new Faker { Random = new Randomizer(seed) }</c> rather than the global
    /// static <c>Randomizer.Seed</c>, because that global is shared process-wide and would make
    /// parallel runs interfere.
    /// </para>
    ///
    /// <para>
    /// The faker and its seeded Bogus <see cref="BogusLib.Faker"/> follow the
    /// <see cref="XModelBuilderIsolation"/> configured on <c>AddXModelBuilder</c> (Shared → one set
    /// per container; PerScope → a fresh, re-seeded set per DI scope), order-independently.
    /// </para>
    /// </summary>
    public static IServiceCollection AddBogusFaker(
        this IServiceCollection services,
        int seed = 8675309,
        string locale = "en")
    {
        return services.AddBogusFaker(_ => seed, locale);
    }

    /// <summary>
    /// As <see cref="AddBogusFaker(IServiceCollection,int,string)"/>, but the seed is produced from the
    /// <see cref="IServiceProvider"/> at resolution time (e.g. a per-scenario seed under
    /// <see cref="XModelBuilderIsolation.PerScope"/>). <paramref name="locale"/> selects the Bogus
    /// data set (e.g. "nl" for Dutch names/addresses/email); see Bogus' supported locales.
    /// </summary>
    public static IServiceCollection AddBogusFaker(
        this IServiceCollection services,
        Func<IServiceProvider, int> seedFactory,
        string locale = "en")
    {
        ArgumentNullException.ThrowIfNull(seedFactory);
        ArgumentException.ThrowIfNullOrWhiteSpace(locale);

        return services.AddIsolatedXModelBuilderServices((s, lifetime) =>
        {
            s.Add(new ServiceDescriptor(
                typeof(BogusLib.Faker),
                sp => new BogusLib.Faker(locale) { Random = new BogusLib.Randomizer(seedFactory(sp)) },
                lifetime));
            s.AddFaker<BogusFaker>(lifetime);
        });
    }
}

