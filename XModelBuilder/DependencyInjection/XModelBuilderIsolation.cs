using Microsoft.Extensions.DependencyInjection;

namespace XModelBuilder.DependencyInjection;

/// <summary>
/// Controls the isolation boundary of the SHARED, STATEFUL parts of XModelBuilder - the
/// <see cref="IModelBuilderProvider"/>, the fakers and their seeded RNGs - in one place. (Builder
/// registrations, <c>ModelBuilderOptions</c> and the default registry stay container-wide
/// regardless.) Because provider and fakers must move together to be coherent, a single setting
/// here makes the broken "scoped faker + singleton provider" combination unrepresentable.
/// </summary>
public enum XModelBuilderIsolation
{
    /// <summary>
    /// One shared set of provider + fakers + seeded RNGs for the whole container (registered as
    /// Singleton). The DI scope is NOT the isolation boundary; for deterministic tests build a FRESH
    /// ServiceProvider per test. Safe to inject anywhere. This is the default.
    /// </summary>
    Shared = 0,

    /// <summary>
    /// A fresh set of provider + fakers + seeded RNGs PER DI scope (registered as Scoped). The DI
    /// scope IS the isolation boundary: each scope re-seeds, so a BDD scenario per scope is
    /// reproducible and parallel-safe. Resolve within a scope; do NOT inject the provider into a
    /// singleton (captive dependency).
    /// </summary>
    PerScope = 1,
}

/// <summary>
/// Shared, registration-time record of the chosen <see cref="XModelBuilderIsolation"/> plus any
/// faker/seeder registrations that arrived BEFORE the isolation was known. Stashed in the
/// <see cref="IServiceCollection"/> (like the default registry) so the choice is order-independent:
/// whoever sets the isolation flushes the deferred registrations with the matching lifetime.
/// </summary>
internal sealed class XModelBuilderIsolationState
{
    /// <summary>
    /// The chosen isolation mode, or <see langword="null"/> until it has been set explicitly.
    /// </summary>
    public XModelBuilderIsolation? Isolation { get; set; }

    /// <summary>
    /// Registrations (faker/seeder) that arrived before the isolation was known; flushed with the
    /// matching lifetime once the isolation is set.
    /// </summary>
    public List<Action<IServiceCollection, ServiceLifetime>> PendingRegistrations { get; } = [];

    /// <summary>
    /// Maps an isolation mode to the service lifetime it uses: <see cref="ServiceLifetime.Scoped"/>
    /// for <see cref="XModelBuilderIsolation.PerScope"/>, otherwise <see cref="ServiceLifetime.Singleton"/>.
    /// </summary>
    /// <param name="isolation">The isolation mode to map.</param>
    /// <returns>The corresponding <see cref="ServiceLifetime"/>.</returns>
    public static ServiceLifetime ToLifetime(XModelBuilderIsolation isolation) =>
        isolation == XModelBuilderIsolation.PerScope ? ServiceLifetime.Scoped : ServiceLifetime.Singleton;
}
