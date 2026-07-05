using System.Collections;
using System.Collections.Concurrent;
using System.Globalization;
using System.Text.RegularExpressions;

namespace XModelBuilder.Core;

/// <summary>
/// Converts textual (and already-parsed) input into strongly typed values. Handles nullable
/// types, enums, arrays, generic lists, sets and dictionaries, culture-aware primitive parsing,
/// nested and "bare" object literals, the special tokens <c>null()</c>, <c>new()</c> and
/// <c>default()</c>, faker-method tokens (<c>Name(args)</c>), and named model-builder references,
/// with a final fallback to <see cref="System.Convert.ChangeType(object, Type, IFormatProvider)"/>.
/// A single leading '@' escapes any token/reference and treats the remainder as literal data.
/// </summary>
internal static class ValueConverter
{

    private static readonly ConcurrentDictionary<Type, Func<string, CultureInfo, object>>
    _knownTypeConverters =
        new()
        {
            [typeof(bool)] = (s, c) => bool.Parse(s),
            [typeof(byte)] = (s, c) => byte.Parse(s, NumberStyles.Integer | NumberStyles.AllowThousands, c),
            [typeof(short)] = (s, c) => short.Parse(s, NumberStyles.Integer | NumberStyles.AllowThousands, c),
            [typeof(int)] = (s, c) => int.Parse(s, NumberStyles.Integer | NumberStyles.AllowThousands, c),
            [typeof(long)] = (s, c) => long.Parse(s, NumberStyles.Integer | NumberStyles.AllowThousands, c),
            [typeof(float)] = (s, c) => float.Parse(s, NumberStyles.Float, c),
            [typeof(double)] = (s, c) => double.Parse(s, NumberStyles.Float, c),
            [typeof(decimal)] = (s, c) => decimal.Parse(s, NumberStyles.Number, c),
            [typeof(DateTime)] = (s, c) => DateTime.Parse(s, c),
            [typeof(DateTimeOffset)] = (s, c) => DateTimeOffset.Parse(s, c),
            [typeof(TimeSpan)] = (s, c) => TimeSpan.Parse(s, c),
            [typeof(Guid)] = (s, c) => Guid.Parse(s),
            [typeof(char)] = (s, c) => char.Parse(s)
        };

    private const string
        TokenNull = "null()",
        TokenNew = "new()",
        TokenDefault = "default()";

    // Matches a faker-method invocation: an identifier immediately followed by parentheses,
    // e.g. "AgeBetween(1,20)" or "Fixture()". The leading identifier-immediately-followed-by-'('
    // shape never occurs in plain data unless deliberately written that way, and - like the
    // null()/new()/default() tokens and named-builder references - can always be escaped with a
    // single leading '@' if the literal text is actually wanted.
    private static readonly Regex FunctionCallPattern = new(
        @"^(?<name>[A-Za-z_][A-Za-z0-9_]*(?:\.[A-Za-z_][A-Za-z0-9_]*)*)\((?<args>.*)\)$",
        RegexOptions.Compiled | RegexOptions.Singleline);

    /// <summary>
    /// Registers (or replaces) the culture-aware converter used to parse a string into the given
    /// primitive/simple target type. Applies globally for the process lifetime.
    /// </summary>
    /// <param name="targetType">The type the converter produces.</param>
    /// <param name="converter">A function that parses a string, using the supplied culture, into a value of <paramref name="targetType"/>.</param>
    public static void AddKnownTypeConverter(Type targetType, Func<string, CultureInfo, object> converter)
    {
        _knownTypeConverters[targetType] = converter;
    }

    /// <summary>
    /// Converts an already-parsed input value into the target type. Dispatches on the input shape:
    /// strings go through <see cref="Convert(string?, Type, CultureInfo, CultureInfo, IModelBuilderProvider)"/>,
    /// object arrays become arrays/lists/sets, and key/value sequences become either a dictionary
    /// (when the target is a dictionary type) or a populated complex object.
    /// </summary>
    /// <param name="input">The parsed input value (string, object array, or key/value sequence).</param>
    /// <param name="targetType">The type to convert into.</param>
    /// <param name="dateTimeCulture">The culture used for date and time parsing.</param>
    /// <param name="culture">The culture used for all other parsing.</param>
    /// <param name="provider">The provider used to build complex objects and resolve builders.</param>
    /// <returns>The converted value, or <see langword="null"/> when applicable.</returns>
    /// <exception cref="NotSupportedException">Thrown when <paramref name="input"/> has a shape that cannot be converted to <paramref name="targetType"/>.</exception>
    public static object? ConvertObject(object input, Type targetType, CultureInfo dateTimeCulture, CultureInfo culture, IModelBuilderProvider provider)
    {
        switch (input)
        {
            case string stringValue:
                return Convert(stringValue, targetType, dateTimeCulture, culture, provider);

            case object[] arrayValues:
                return ConvertArray(arrayValues, targetType, dateTimeCulture, culture, provider);

            case IEnumerable<KeyValuePair<string, object>> values when targetType.GetDictionaryTypeArgumentsOrNull() is { } dictionaryArgs:
                return BuildDictionary(values, dictionaryArgs.KeyType, dictionaryArgs.ValueType, dateTimeCulture, culture, provider);

            case IEnumerable<KeyValuePair<string, object>> values:
                return ConvertObjectValues(values, targetType, dateTimeCulture, culture, provider);

            default:
                throw new NotSupportedException($"Cannot convert value of type {input.GetType().GetFriendlyName()} to target type {targetType.GetFriendlyName()}.");
        }
    }

    private static object ConvertArray(object[] items, Type targetType, CultureInfo dateTimeCulture, CultureInfo culture, IModelBuilderProvider provider)
    {
        if (targetType.IsArray)
        {
            var elementType = targetType.GetElementType()!;
            var array = Array.CreateInstance(elementType, items.Length);

            for (var i = 0; i < items.Length; i++)
            {
                array.SetValue(ConvertObject(items[i], elementType, dateTimeCulture, culture, provider), i);
            }

            return array;
        }

        if (targetType.GetSetElementTypeOrNull() is { } setElementType)
        {
            return BuildSet(items, setElementType, dateTimeCulture, culture, provider);
        }

        var listElementType = targetType.GetListElementType() ?? typeof(object);
        var listType = typeof(List<>).MakeGenericType(listElementType);
        var list = (IList)Activator.CreateInstance(listType)!;

        foreach (var item in items)
        {
            list.Add(ConvertObject(item, listElementType, dateTimeCulture, culture, provider));
        }

        return list;
    }

    private static object ConvertObjectValues(IEnumerable<KeyValuePair<string, object>> values, Type targetType, CultureInfo dateTimeCulture, CultureInfo culture, IModelBuilderProvider provider)
    {
        var instance = provider.For(targetType).Build();

        foreach (var kvp in values)
        {
            if (!targetType.TryGetWritableMember(kvp.Key, out var member))
            {
                throw new InvalidOperationException($"Unable to set member '{kvp.Key}' for type {targetType.GetFriendlyName(true)}.");
            }

            var convertedValue = ConvertObject(kvp.Value, member.GetMemberType(), dateTimeCulture, culture, provider);
            member.SetMemberValue(instance, convertedValue);
        }

        return instance;
    }

    private static object BuildDictionary(IEnumerable<KeyValuePair<string, object>> entries, Type keyType, Type valueType, CultureInfo dateTimeCulture, CultureInfo culture, IModelBuilderProvider provider)
    {
        var dictType = typeof(Dictionary<,>).MakeGenericType(keyType, valueType);
        var dict = (IDictionary)Activator.CreateInstance(dictType)!;

        foreach (var entry in entries)
        {
            var key = Convert(entry.Key, keyType, dateTimeCulture, culture, provider);
            var value = ConvertObject(entry.Value, valueType, dateTimeCulture, culture, provider);
            dict.Add(key!, value);
        }

        return dict;
    }

    private static object BuildSet(object[] items, Type elementType, CultureInfo dateTimeCulture, CultureInfo culture, IModelBuilderProvider provider)
    {
        var setType = typeof(HashSet<>).MakeGenericType(elementType);
        var set = Activator.CreateInstance(setType)!;
        var addMethod = setType.GetMethod("Add")!;

        foreach (var item in items)
        {
            var convertedItem = ConvertObject(item, elementType, dateTimeCulture, culture, provider);
            addMethod.Invoke(set, [convertedItem]);
        }

        return set;
    }

    /// <summary>
    /// Converts a string into the specified target type, choosing the culture automatically:
    /// <see cref="DateTime"/> and <see cref="DateTimeOffset"/> use <paramref name="dateTimeCultureInfo"/>,
    /// all other types use <paramref name="defaultCultureInfo"/>. Delegates to
    /// <see cref="Convert(string?, Type, CultureInfo?, IModelBuilderProvider)"/>.
    /// </summary>
    /// <param name="input">The input string to convert.</param>
    /// <param name="targetType">The type to convert into.</param>
    /// <param name="dateTimeCultureInfo">Culture used for date and time parsing.</param>
    /// <param name="defaultCultureInfo">Culture used for all non-date parsing.</param>
    /// <param name="provider">The provider used to resolve model builders and faker tokens.</param>
    /// <returns>The converted value, or <see langword="null"/> when applicable.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="targetType"/> is <see langword="null"/>.</exception>
    public static object? Convert(
           string? input,
           Type targetType,
           CultureInfo dateTimeCultureInfo,
           CultureInfo defaultCultureInfo,
           IModelBuilderProvider provider)
    {
        var effectiveType = targetType.EnsureNotNullable();
        var culture = effectiveType == typeof(DateTime) || effectiveType == typeof(DateTimeOffset) ? dateTimeCultureInfo : defaultCultureInfo;
        return Convert(input, targetType, culture, provider);
    }

    /// <summary>
    /// Converts a string into the specified target type using the given culture.
    /// Supports the special tokens null(), new() and default(), and - for any reference
    /// type other than string - a bare name referring to a model builder registered for
    /// that type via <see cref="ModelBuilderAttribute"/>. A single leading '@' escapes all
    /// of the above and is stripped, so the remainder is treated as literal data.
    /// Arrays and generic collections are parsed element-by-element.
    /// Enum values can be specified using names or numeric values.
    /// Known primitive types are handled using predefined converters.
    /// Fallback conversion uses System.Convert.ChangeType.
    /// </summary>
    /// <param name="input">The input string to convert.</param>
    /// <param name="targetType">The type to convert into.</param>
    /// <param name="culture">The culture used for parsing. If null, invariant culture is used.</param>
    /// <param name="provider">Provider used to resolve model builders (default() and named-builder references).</param>
    /// <returns>The converted value or null when applicable.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="targetType"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="input"/> is empty and <paramref name="targetType"/> is a non-nullable value type.</exception>
    /// <exception cref="FormatException">Thrown when parsing fails for a supported type, or a dictionary literal does not start with '{'.</exception>
    /// <exception cref="InvalidOperationException">Thrown for unsupported array or collection types.</exception>
    /// <exception cref="NotSupportedException">Thrown when the input is a faker token but <paramref name="provider"/> does not support faker invocation.</exception>
    /// <exception cref="KeyNotFoundException">Thrown when a named model builder reference cannot be resolved.</exception>
    public static object? Convert(string? input, Type targetType, CultureInfo? culture, IModelBuilderProvider provider)
    {
        ArgumentNullException.ThrowIfNull(targetType);

        culture ??= CultureInfo.InvariantCulture;

        var convertToType = targetType.EnsureNotNullable();
        var isNullable = Nullable.GetUnderlyingType(targetType) != null;

        var trimmed = input?.Trim();
        var isEscaped = trimmed is { Length: > 0 } && trimmed[0] == '@';

        if (isEscaped)
        {
            trimmed = trimmed![1..];
        }
        else if (trimmed == TokenNull)
        {
            return null;
        }
        else if (trimmed == TokenNew)
        {
            return Instantiator.CreateInstance(targetType);
        }
        else if (trimmed == TokenDefault)
        {
            if (isNullable || convertToType == typeof(string))
            {
                return null;
            }

            if (convertToType.IsValueType)
            {
                return Activator.CreateInstance(convertToType);
            }

            return provider.For(convertToType).Build();
        }
        else if (trimmed != null && FunctionCallPattern.Match(trimmed) is { Success: true } functionCall)
        {
            if (provider is not IFakerInvocationSource fakerSource)
            {
                throw new NotSupportedException($"The current {nameof(IModelBuilderProvider)} ({provider.GetType().GetFriendlyName()}) does not support faker tokens.");
            }

            var fakerName = functionCall.Groups["name"].Value;
            var argsText = functionCall.Groups["args"].Value;
            var rawArgs = string.IsNullOrWhiteSpace(argsText) ? [] : Parser.ParseArray(argsText);
            return fakerSource.InvokeFaker(fakerName, rawArgs, convertToType, culture);
        }

        if (targetType == typeof(string))
        {
            return trimmed;
        }

        input = trimmed;

        if (string.IsNullOrEmpty(input))
        {
            if (isNullable || !convertToType.IsValueType)
            {
                return null;
            }

            throw new ArgumentException($"Cannot convert '{input}' to type {targetType}", nameof(input));
        }

        if (convertToType.IsArray)
        {
            var elementType = convertToType.GetElementType()!;
            var items = Parser.ParseArray(input);
            var array = Array.CreateInstance(elementType, items.Length);

            for (var i = 0; i < items.Length; i++)
            {
                var convertedItem = ConvertObject(items[i], elementType, culture, culture, provider);
                array.SetValue(convertedItem, i);
            }

            return array;
        }

        if (convertToType.GetDictionaryTypeArgumentsOrNull() is { } dictionaryArgs)
        {
            if (input[0] != '{')
            {
                throw new FormatException($"Cannot convert '{input}' to target type {targetType.GetFriendlyName()}: expected an object literal '{{...}}'.");
            }

            var entries = Parser.ParseObject(input);
            return BuildDictionary(entries, dictionaryArgs.KeyType, dictionaryArgs.ValueType, culture, culture, provider);
        }

        if (convertToType.GetSetElementTypeOrNull() is { } setElementType)
        {
            var items = Parser.ParseArray(input);
            return BuildSet(items, setElementType, culture, culture, provider);
        }

        if (convertToType.IsGenericType)
        {
            var genericDef = convertToType.GetGenericTypeDefinition();

            if (genericDef == typeof(List<>)
                || genericDef == typeof(IList<>)
                || genericDef == typeof(ICollection<>)
                || genericDef == typeof(IReadOnlyList<>)
                || genericDef == typeof(IReadOnlyCollection<>)
                || genericDef == typeof(IEnumerable<>))
            {
                var elementType = convertToType.GetGenericArguments()[0];
                var listType = typeof(List<>).MakeGenericType(elementType);

                var list = (IList)Activator.CreateInstance(listType)!
                    ?? throw new InvalidOperationException($"Could not create instance of {listType.GetFriendlyName()}.");

                var items = Parser.ParseArray(input);

                foreach (var item in items)
                {
                    var convertedItem = ConvertObject(item, elementType, culture, culture, provider);
                    list.Add(convertedItem);
                }

                return list;
            }
        }

        if (input[0] == '{')
        {
            var values = Parser.ParseObject(input);
            return ConvertObject(values, convertToType, culture, culture, provider);
        }

        if (!isEscaped && !convertToType.IsValueType && convertToType != typeof(string) && convertToType != typeof(object))
        {
            // "Bare" object literal without { } for a complex target type (e.g. Bezorgadres:
            // "Straat:...,Plaats:..."). A top-level ':' distinguishes this from a builder name.
            if (Parser.LooksLikeBareObject(input))
            {
                var values = Parser.ParseObject(input);
                return ConvertObject(values, convertToType, culture, culture, provider);
            }

            return provider.For(convertToType, input).Build();
        }

        try
        {
            if (convertToType.IsEnum)
            {
                return Enum.Parse(convertToType, input, ignoreCase: true);
            }

            if (_knownTypeConverters.TryGetValue(convertToType, out var converter))
            {
                return converter.Invoke(input, culture);
            }

            return System.Convert.ChangeType(input, convertToType, culture);
        }
        catch(Exception ex)
        {
            throw new FormatException($"Cannot convert {input ?? "null"} to target type {targetType.GetFriendlyName()}. Missing converter for {targetType.GetFriendlyName()}?", ex);
        }
    }
}
