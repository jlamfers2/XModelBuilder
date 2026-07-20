namespace XModelBuilder.Demo.Shop.IntegrationTests.Support;

/// <summary>
/// A frozen <see cref="TimeProvider"/> that always returns the same instant, so every clock-bound
/// value in the suite is reproducible - the audit <c>CreatedAt</c> stamped by the cross-cutting layer
/// (<see cref="EntityDefaults{TModel}"/>), the server's order timestamps, and XFaker's age tokens.
/// This is a tiny stand-in for <c>Microsoft.Extensions.TimeProvider.Testing.FakeTimeProvider</c>
/// (the guide's recommended choice) kept dependency-free for the demo. To time-travel, register a
/// second instance at a later instant; see <c>docs/scenarios/01-time-traveling.md</c>.
/// </summary>
/// <param name="utcNow">The fixed UTC instant this provider reports.</param>
public sealed class FixedTimeProvider(DateTimeOffset utcNow) : TimeProvider
{
    /// <inheritdoc/>
    public override DateTimeOffset GetUtcNow() => utcNow;
}
