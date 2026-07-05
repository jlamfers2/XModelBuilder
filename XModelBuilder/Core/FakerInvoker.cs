using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;

namespace XModelBuilder.Core;

/// <summary>
/// Resolves and invokes a faker token on one of the registered <see cref="IFaker"/> instances.
/// A token name may be a single identifier ("FirstName()") or a dotted MEMBER PATH that starts at
/// a registered faker and walks its members ("bogus.name.firstname()"): the first segment selects
/// the owning faker, intermediate segments are read as properties/fields/parameterless methods,
/// and the final segment is invoked as a method - or, when no such method exists and no arguments
/// were supplied, read as a property/field. Methods may declare optional parameters.
///
/// Shared by both the DI-based provider and <see cref="XModelBuilder.Default.DefaultModelBuilderProvider"/>,
/// which only differ in how they obtain the list of registered fakers.
/// </summary>
internal static class FakerInvoker
{
    private const BindingFlags MethodFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static;

    private const BindingFlags MemberFlags =
        BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.IgnoreCase;

    // Reflection metadata for a (type, name) pair is immutable for the process lifetime, so the
    // method lookup - the heaviest per-token cost (GetMethods + LINQ) - is memoized.
    private static readonly ConcurrentDictionary<(Type Type, string Name), MethodInfo[]> NamedMethodCache = new();

    /// <summary>
    /// Resolves the faker token <paramref name="name"/> against the registered fakers and invokes it.
    /// The most recently registered faker that owns the first path segment wins; intermediate segments
    /// are read as members, and the final segment is invoked as the best-matching method overload (or
    /// read as a member when no method matches and no arguments were supplied). The result is coerced
    /// to <paramref name="targetType"/> where needed.
    /// </summary>
    /// <param name="fakers">The registered faker instances, in registration order (last wins).</param>
    /// <param name="name">The faker token name; a single identifier or a dotted member path.</param>
    /// <param name="args">The positional arguments parsed from the token, in call order.</param>
    /// <param name="targetType">The type the produced value is intended for; used for overload resolution, injection and coercion.</param>
    /// <param name="culture">The culture used when converting arguments and coercing the result.</param>
    /// <param name="provider">The provider used to convert argument and result values.</param>
    /// <param name="services">The service provider auto-injected into faker methods that declare a leading <see cref="IServiceProvider"/> parameter.</param>
    /// <returns>The value produced by the faker, coerced to <paramref name="targetType"/> where applicable.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="fakers"/>, <paramref name="name"/>, <paramref name="args"/> or <paramref name="targetType"/> is <see langword="null"/>.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when no faker owns the token, or a path segment cannot be resolved.</exception>
    /// <exception cref="MissingMethodException">Thrown when no method overload matches the supplied arguments.</exception>
    public static object? Invoke(
        IReadOnlyList<IFaker> fakers,
        string name,
        object?[] args,
        Type targetType,
        CultureInfo culture,
        IModelBuilderProvider provider,
        IServiceProvider services)
    {
        ArgumentNullException.ThrowIfNull(fakers);
        ArgumentNullException.ThrowIfNull(name);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(targetType);

        var segments = name.Split('.');

        // The most recently registered faker that OWNS the first segment (has a method or a
        // gettable member with that name) wins entirely - we never mix across faker classes.
        for (var i = fakers.Count - 1; i >= 0; i--)
        {
            object? current = fakers[i];
            if (!OwnsSegment(current!.GetType(), segments[0]))
            {
                continue;
            }

            // Walk all but the last segment as plain member reads (property / field / parameterless method).
            for (var s = 0; s < segments.Length - 1; s++)
            {
                if (current is null || !TryGetMember(current, segments[s], out current))
                {
                    throw new KeyNotFoundException($"Cannot resolve faker path '{name}': no member '{segments[s]}'.");
                }
            }

            if (current is null)
            {
                throw new KeyNotFoundException($"Cannot resolve faker path '{name}': the value before '{segments[^1]}' is null.");
            }

            var result = InvokeTerminal(current, segments[^1], args, targetType, culture, provider, services);
            return CoerceResult(result, targetType, culture, provider);
        }

        throw new KeyNotFoundException($"No faker method or member named '{segments[0]}' is registered.");
    }

    private static bool OwnsSegment(Type type, string segment) =>
        GetNamedMethods(type, segment).Length > 0 || GetGettableMember(type, segment) is not null;

    // Terminal resolution: prefer a method (best overload, optional-aware). When there is NO such
    // method and no arguments were supplied, fall back to a gettable member - this is what lets a
    // token like "bogus.person.firstname()" reach a property named FirstName.
    private static object? InvokeTerminal(
        object target,
        string name,
        object?[] args,
        Type targetType,
        CultureInfo culture,
        IModelBuilderProvider provider,
        IServiceProvider services)
    {
        var methods = GetNamedMethods(target.GetType(), name);
        if (methods.Length > 0)
        {
            return InvokeBestOverload(target, methods, name, args, targetType, culture, provider, services);
        }

        if (args.Length == 0 && GetGettableMember(target.GetType(), name) is { } member)
        {
            return member.GetMemberValue(target);
        }

        throw new KeyNotFoundException(
            $"No faker method or member named '{name}' on '{target.GetType().GetFriendlyName()}' matches {args.Length} argument(s).");
    }

    private static bool TryGetMember(object target, string name, out object? value)
    {
        if (GetGettableMember(target.GetType(), name) is { } member)
        {
            value = member.GetMemberValue(target);
            return true;
        }

        // An intermediate segment may also be a parameterless method, e.g. faker.Make().Deeper().
        var method = GetNamedMethods(target.GetType(), name).FirstOrDefault(m => m.GetParameters().Length == 0);
        if (method is not null)
        {
            value = method.Invoke(target, []);
            return true;
        }

        value = null;
        return false;
    }

    private static MemberInfo? GetGettableMember(Type type, string name)
    {
        var prop = type.GetProperty(name, MemberFlags);
        if (prop is not null && prop.CanRead)
        {
            return prop;
        }

        return type.GetField(name, MemberFlags);
    }

    // Public, protected, internal and protected-internal methods are all eligible (so a faker can
    // hide "framework-oriented" overloads - e.g. ones taking a Type/IServiceProvider - from its
    // public, typed-callable surface while still allowing token dispatch to find them), and both
    // instance AND static methods count. Private methods, open generic method definitions and
    // compiler-special methods (property accessors, operators) are never eligible.
    private static MethodInfo[] GetNamedMethods(Type type, string name) =>
        NamedMethodCache.GetOrAdd((type, name), static key =>
            key.Type.GetMethods(MethodFlags)
                .Where(m => !m.IsPrivate && !m.IsGenericMethodDefinition && !m.IsSpecialName)
                .Where(m => string.Equals(m.Name, key.Name, StringComparison.OrdinalIgnoreCase))
                .ToArray());

#pragma warning disable S107
    private static object? InvokeBestOverload(
        object target,
        IReadOnlyList<MethodInfo> candidates,
        string name,
        object?[] args,
        Type targetType,
        CultureInfo culture,
        IModelBuilderProvider provider,
        IServiceProvider services)
#pragma warning restore S107
    {
        // Each candidate's leading Type/IServiceProvider parameters are auto-injected and not
        // counted as token arguments (matched purely by type, in any order relative to each other).
        // A candidate is viable when the number of supplied arguments lies between its required
        // (non-optional) data-parameter count and its total data-parameter count. Prefer an exact
        // arity match, then the overload that needs to fill the fewest optional defaults.
        var viable = candidates
            .Select(method =>
            {
                var parameters = method.GetParameters();
                var lead = CountLeadingSpecialParameters(parameters);
                var data = parameters.AsSpan(lead).ToArray();
                var required = data.Count(p => !p.HasDefaultValue);
                return (method, parameters, lead, data, required);
            })
            .Where(x => args.Length >= x.required && args.Length <= x.data.Length)
            .OrderBy(x => x.data.Length == args.Length ? 0 : 1)
            .ThenBy(x => x.data.Length - args.Length)
            .ToList();

        foreach (var (method, parameters, lead, data, _) in viable)
        {
            var invokeArgs = new object?[parameters.Length];

            for (var i = 0; i < lead; i++)
            {
                invokeArgs[i] = parameters[i].ParameterType == typeof(Type) ? targetType : services;
            }

            if (!TryConvertArguments(args, data, culture, provider, invokeArgs, lead))
            {
                continue;
            }

            // Fill any trailing optional parameters the caller did not supply with their defaults.
            for (var j = args.Length; j < data.Length; j++)
            {
                invokeArgs[lead + j] = data[j].DefaultValue;
            }

            return method.Invoke(target, invokeArgs);
        }

        throw new MissingMethodException($"No overload of faker method '{name}' matches the given {args.Length} argument(s).");
    }

    private static int CountLeadingSpecialParameters(ParameterInfo[] parameters)
    {
        var count = 0;
        while (count < parameters.Length && IsSpecialParameterType(parameters[count].ParameterType))
        {
            count++;
        }
        return count;
    }

    private static bool IsSpecialParameterType(Type type) => type == typeof(Type) || type == typeof(IServiceProvider);

    private static bool TryConvertArguments(
        object?[] args,
        ParameterInfo[] dataParameters,
        CultureInfo culture,
        IModelBuilderProvider provider,
        object?[] invokeArgs,
        int offset)
    {
        for (var i = 0; i < args.Length; i++)
        {
            try
            {
                invokeArgs[i + offset] = args[i] is null
                    ? null
                    : ValueConverter.ConvertObject(args[i]!, dataParameters[i].ParameterType, culture, culture, provider);
            }
            catch
            {
                return false;
            }
        }

        return true;
    }

    private static object? CoerceResult(object? result, Type targetType, CultureInfo culture, IModelBuilderProvider provider)
    {
        if (result is null || targetType.IsInstanceOfType(result))
        {
            return result;
        }

        if (result is string text)
        {
            return ValueConverter.Convert(text, targetType, culture, provider);
        }

        return result;
    }
}
