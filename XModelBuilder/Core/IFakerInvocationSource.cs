using System.Globalization;

namespace XModelBuilder.Core;

/// <summary>
/// Internal-only counterpart to the dynamic "name(args)" faker-token dispatch (see
/// <see cref="ValueConverter"/>). Deliberately NOT part of the public <see cref="IModelBuilderProvider"/>
/// contract - end users should either call a specific faker directly
/// (<see cref="IModelBuilderProvider.Faker{TFaker}"/>, constructor injection of the faker type
/// itself) or rely on the token syntax, never this plumbing method. Implemented by the built-in
/// providers (<see cref="XModelBuilder.DependencyInjection.ModelBuilderProvider"/> and
/// <see cref="XModelBuilder.Default.DefaultModelBuilderProvider"/>); a custom <see cref="IModelBuilderProvider"/>
/// that does not implement this simply does not support faker tokens.
/// </summary>
internal interface IFakerInvocationSource
{
    /// <summary>
    /// Resolves and invokes a registered faker by its token name, selecting the best matching
    /// overload for the supplied arguments and injecting the target type and culture where required.
    /// </summary>
    /// <param name="name">The faker token name to dispatch to.</param>
    /// <param name="args">The positional arguments parsed from the token, in call order.</param>
    /// <param name="targetType">The type the produced value is intended for; used for overload resolution and conversion.</param>
    /// <param name="culture">The culture to use when converting or generating culture-sensitive values.</param>
    /// <returns>The value produced by the faker, or <see langword="null"/> when the faker yields no value.</returns>
    /// <exception cref="KeyNotFoundException">Thrown when no faker or member matches <paramref name="name"/>.</exception>
    /// <exception cref="MissingMethodException">Thrown when no faker overload matches the supplied <paramref name="args"/>.</exception>
    object? InvokeFaker(string name, object?[] args, Type targetType, CultureInfo culture);
}
