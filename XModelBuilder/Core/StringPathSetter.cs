using System.Collections;
using System.Globalization;
using System.Reflection;


namespace XModelBuilder.Core;

/// <summary>
/// Sets a value at the end of a string deep-path (e.g. <c>"Address.Street"</c> or
/// <c>"Lines[2].Amount"</c>) on a target object. Splits the path into segments, resolves each
/// writable member, materializes missing intermediate objects, arrays and lists (growing or
/// replacing fixed-size/read-only collections where needed) and converts the final text value
/// culture-aware via <see cref="ValueConverter"/>. This is the string counterpart to
/// <see cref="LambdaPathSetter"/>'s strongly-typed lambda deep-path setting.
/// </summary>
internal static class StringPathSetter
{
    /// <summary>
    /// Sets <paramref name="value"/> at the member described by the string deep-path
    /// <paramref name="path"/> on <paramref name="target"/>, creating any missing intermediate
    /// objects and collection elements via the provider and converting the text value using the
    /// supplied cultures.
    /// </summary>
    /// <param name="target">The root instance to mutate.</param>
    /// <param name="path">The deep-path, e.g. <c>"Address.Street"</c> or <c>"Lines[2].Amount"</c>.</param>
    /// <param name="value">The raw text value to convert and assign at the end of the path.</param>
    /// <param name="dateTimeCultureInfo">The culture used when converting date/time values.</param>
    /// <param name="defaultCultureInfo">The culture used when converting other culture-sensitive values.</param>
    /// <param name="provider">The provider used to build missing intermediate objects and elements.</param>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> contains no segments.</exception>
    /// <exception cref="InvalidOperationException">Thrown when a path segment names a member that does not exist on the current type.</exception>
    /// <exception cref="FormatException">Thrown when an indexed path segment is malformed.</exception>
    public static void SetMemberValueByString(
        this object target,
        string path,
        string? value,
        CultureInfo dateTimeCultureInfo,
        CultureInfo defaultCultureInfo,
        IModelBuilderProvider provider
        )
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(path);

        var segments = path.Split('.', StringSplitOptions.RemoveEmptyEntries);
        if (segments.Length == 0)
        {
            throw new ArgumentException("Path must contain at least one segment.", nameof(path));
        }

        object current = target;
        Type currentType = target.GetType();

        for (int i = 0; i < segments.Length; i++)
        {
            string segment = segments[i];
            bool isLast = i == segments.Length - 1;

            ParsePathSegment(segment, out string memberName, out int? index);

            if (!currentType.TryGetWritableMember(memberName, out MemberInfo member))
            {
                throw new InvalidOperationException(
                    $"Member '{memberName}' not found on type '{currentType.FullName}'.");
            }

            if (index.HasValue)
            {
                HandleIndexedSegment(
                    ref current,
                    ref currentType,
                    member,
                    index.Value,
                    isLast,
                    value,
                    dateTimeCultureInfo,
                    defaultCultureInfo,
                    provider);
            }
            else
            {
                HandleNonIndexedSegment(
                    ref current,
                    ref currentType,
                    member,
                    isLast,
                    value,
                    dateTimeCultureInfo,
                    defaultCultureInfo,
                    provider);
            }
        }
    }

    #region Core helpers (performance-optimized, no closures)

    private static void HandleNonIndexedSegment(
        ref object current,
        ref Type currentType,
        MemberInfo member,
        bool isLast,
        string? value,
        CultureInfo dateTimeCultureInfo,
        CultureInfo defaultCultureInfo,
        IModelBuilderProvider provider)
    {
        Type memberType = member.GetMemberType();
        object? memberValue = member.GetMemberValue(current);

        if (isLast)
        {
            object? converted = ValueConverter.Convert(value, memberType, dateTimeCultureInfo, defaultCultureInfo, provider);
            member.SetMemberValue(current, converted);
        }
        else
        {
            if (memberValue == null)
            {
                memberValue = provider
                    .For(memberType)
                    .Build();
                member.SetMemberValue(current, memberValue);
            }

            current = memberValue!;
            currentType = memberType;
        }
    }

    private static void HandleIndexedSegment(
        ref object current,
        ref Type currentType,
        MemberInfo member,
        int index,
        bool isLast,
        string? value,
        CultureInfo dateTimeCultureInfo,
        CultureInfo defaultCultureInfo,
        IModelBuilderProvider provider)
    {
        Type memberType = member.GetMemberType();
        object? memberValue = member.GetMemberValue(current);

        if (memberType.IsArray)
        {
            HandleArraySegment(
                ref current,
                ref currentType,
                member,
                memberType,
                memberValue as Array,
                index,
                isLast,
                value,
                dateTimeCultureInfo,
                defaultCultureInfo,
                provider);
        }
        else
        {
            HandleListSegment(
                ref current,
                ref currentType,
                member,
                memberType,
                memberValue,
                index,
                isLast,
                value,
                dateTimeCultureInfo,
                defaultCultureInfo,
                provider);
        }
    }

    private static void HandleArraySegment(
        ref object current,
        ref Type currentType,
        MemberInfo member,
        Type arrayType,
        Array? array,
        int idx,
        bool isLast,
        string? value,
        CultureInfo dateTimeCultureInfo,
        CultureInfo defaultCultureInfo,
        IModelBuilderProvider provider)
    {
        Type elementType = arrayType.GetElementType() ?? typeof(object);

        if (array == null || array.Length <= idx)
        {
            int newLength = idx + 1;
            Array newArray = Array.CreateInstance(elementType, newLength);

            if (array != null && array.Length > 0)
            {
                Array.Copy(array, newArray, array.Length);
            }

            array = newArray;
            member.SetMemberValue(current, array);
        }

        if (isLast)
        {
            object? converted = ValueConverter.Convert(value, elementType, dateTimeCultureInfo, defaultCultureInfo, provider);
            array!.SetValue(converted, idx);
        }
        else
        {
            object? element = array!.GetValue(idx);
            if (element == null)
            {
                element = provider
                    .For(elementType)
                    .Build();
                array.SetValue(element, idx);
            }

            current = element!;
            currentType = element!.GetType();
        }
    }

    private static void HandleListSegment(
        ref object current,
        ref Type currentType,
        MemberInfo member,
        Type listType,
        object? memberValue,
        int idx,
        bool isLast,
        string? value,
        CultureInfo dateTimeCultureInfo,
        CultureInfo defaultCultureInfo,
        IModelBuilderProvider provider)
    {
        Type elementType = listType.GetListElementType()
            ?? memberValue?.GetType().GetListElementType()
            ?? typeof(object);

        IList list;

        // We need a GROWABLE list to be able to write at an index. The current value may be missing
        // (null), or a fixed-size/read-only collection (e.g. the default `[]`, which is an empty array,
        // or an IReadOnlyList<>). In those cases we materialize a List<elementType>, copy over any
        // existing elements, and set it back on the member.
        if (memberValue is IList { IsFixedSize: false, IsReadOnly: false } growable)
        {
            list = growable;
        }
        else
        {
            list = (IList)Activator.CreateInstance(typeof(List<>).MakeGenericType(elementType))!;

            if (memberValue is IEnumerable existing and not string)
            {
                foreach (object? item in existing)
                {
                    list.Add(item);
                }
            }

            member.SetMemberValue(current, list);
        }

        list.EnsureListSize(idx, isLast, elementType, provider);

        if (isLast)
        {
            object? converted = ValueConverter.Convert(value, elementType, dateTimeCultureInfo, defaultCultureInfo, provider);
            list[idx] = converted;
        }
        else
        {
            object? element = list[idx];
            if (element == null)
            {
                element = provider
                    .For(elementType)
                    .Build();
                list[idx] = element;
            }

            current = element!;
            currentType = element!.GetType();
        }
    }

    #endregion

    private static void ParsePathSegment(string segment, out string name, out int? index)
    {
        int bracketIndex = segment.IndexOf('[');
        if (bracketIndex < 0)
        {
            name = segment;
            index = null;
            return;
        }

        name = segment[..bracketIndex];
        int endBracketIndex = segment.IndexOf(']', bracketIndex + 1);
        if (endBracketIndex < 0)
        {
            throw new FormatException($"Invalid path segment '{segment}'.");
        }

        string indexString = segment.Substring(bracketIndex + 1, endBracketIndex - bracketIndex - 1);
        index = int.Parse(indexString.Trim(), CultureInfo.InvariantCulture);
    }
}