namespace XModelBuilder.Core;

/// <summary>
/// Thread-local guard that detects a cyclic model build and turns it into a catchable
/// <see cref="InvalidOperationException"/> instead of an unrecoverable <c>StackOverflowException</c>.
/// It tracks the chain of model types currently being built on the current thread; every
/// <see cref="ModelBuilder{TBuilder,TModel}.Build"/> enters the guard, so ANY mechanism that
/// recursively builds a nested model - <c>WithDefault</c>, <c>WithBuilder</c>, a <c>"default()"</c>
/// or named-builder string value, an auto-vivified nested path, or a built list element - is covered,
/// because they all funnel through a builder's <see cref="ModelBuilder{TBuilder,TModel}.Build"/>.
/// A re-entered type means genuinely infinite recursion, since builders configure their defaults
/// declaratively (the same nested build would repeat forever).
/// </summary>
internal static class BuildReentrancyGuard
{
    [ThreadStatic]
    private static List<Type>? _chain;

    /// <summary>
    /// Registers <paramref name="modelType"/> as being built on the current thread. Must be paired
    /// with an <see cref="Exit"/> in a <c>finally</c> block for every successful (non-throwing) call.
    /// </summary>
    /// <param name="modelType">The model type about to be built.</param>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="modelType"/> is already on the current build chain (a cycle).</exception>
    public static void Enter(Type modelType)
    {
        var chain = _chain ??= new List<Type>(8);

        if (chain.Contains(modelType))
        {
            var path = string.Join(" -> ", chain.Append(modelType).Select(t => t.GetFriendlyName(true)));
            throw new InvalidOperationException(
                $"Cyclic model build detected: {path}. A model type is (transitively) building itself " +
                "through its default or named builder - e.g. a WithDefault, WithBuilder, a \"default()\" or " +
                "named-builder string value, or an auto-vivified nested path that resolves back to the same " +
                "type. Break the cycle so a builder does not fill a member that leads back to its own type " +
                "(set that member explicitly instead of letting it build via a default/named builder).");
        }

        chain.Add(modelType);
    }

    /// <summary>
    /// Removes the most recently entered model type from the current thread's build chain. Because
    /// builds nest strictly (each <see cref="Enter"/> is paired with an <see cref="Exit"/> via
    /// <c>try/finally</c>), removing the last entry unwinds the chain correctly.
    /// </summary>
    public static void Exit()
    {
        var chain = _chain;
        if (chain is { Count: > 0 })
        {
            chain.RemoveAt(chain.Count - 1);
        }
    }
}
