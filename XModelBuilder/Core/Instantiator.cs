using System.Reflection;
using System.Runtime.CompilerServices;

namespace XModelBuilder.Core;

/// <summary>
/// Creates instances of arbitrary model types for use as a builder's starting object.
/// Prefers a parameterless constructor; failing that, invokes the constructor with the fewest
/// parameters using synthesized default arguments; and as a last resort returns an uninitialized
/// object so instantiation always succeeds.
/// </summary>
internal static class Instantiator
{
    /// <summary>
    /// Creates an instance of <typeparamref name="TModel"/>.
    /// </summary>
    /// <typeparam name="TModel">The type to instantiate.</typeparam>
    /// <returns>A new instance of <typeparamref name="TModel"/>.</returns>
    public static TModel CreateInstance<TModel>() => (TModel)CreateInstance(typeof(TModel));

    /// <summary>
    /// Creates an instance of the given type. A parameterless constructor is used when available;
    /// otherwise the constructor with the fewest parameters is invoked with synthesized default
    /// arguments (empty string for <see cref="string"/>, <c>default</c> for value types,
    /// <see langword="null"/> for other reference types). If construction throws, an uninitialized
    /// object is returned as a fallback so this method never fails for a constructible type.
    /// </summary>
    /// <param name="modelType">The type to instantiate.</param>
    /// <returns>A new (possibly uninitialized) instance of <paramref name="modelType"/>.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="modelType"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">Thrown when <paramref name="modelType"/> exposes no usable constructor.</exception>
    public static object CreateInstance(Type modelType)
    {
        ArgumentNullException.ThrowIfNull(modelType);

        var flags = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;

        ConstructorInfo? ctor = null;

        // 1. Try default parameterless constructor
        var defaultCtor = modelType.GetConstructor(
                flags,
                binder: null,
                types: Type.EmptyTypes,
                modifiers: null);
        if (defaultCtor != null)
        {
            return defaultCtor.Invoke(null);
        }

        // 2. No default ctor → pick constructor with the fewest parameters
        ctor = modelType
            .GetConstructors(flags)
            .OrderBy(c => c.GetParameters().Length)
            .FirstOrDefault() ?? throw new InvalidOperationException(
                $"No constructors found for type {modelType.FullName}"
            );

        // 3. Build argument list with required defaults
        var parameters = ctor.GetParameters();
        var args = new object?[parameters.Length];

        for (int i = 0; i < parameters.Length; i++)
        {
            Type paramType = parameters[i].ParameterType;

            if (paramType == typeof(string))
            {
                args[i] = string.Empty;
            }
            else if (paramType.IsValueType)
            {
                // default(T) but using Activator for structs
                args[i] = Activator.CreateInstance(paramType);
            }
            else
            {
                // reference types → null
                args[i] = null;
            }
        }
        try
        {
            return ctor.Invoke(args);
        }
        catch
        {
            // 6. fallback -> always return an instance
            return RuntimeHelpers.GetUninitializedObject(modelType);
        }
    }
}