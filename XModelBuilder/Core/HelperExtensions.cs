using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace XModelBuilder.Core
{
    /// <summary>
    /// Internal reflection helper extensions used across the Core layer: type inspection
    /// (nullable/generic/collection element types), model-builder metadata lookups, cached
    /// writable-member resolution for deep-path setting, and member get/set utilities.
    /// </summary>
    internal static class HelperExtensions
    {
        // Member resolution for a (type, name) pair is immutable for the process lifetime, so the
        // up-to-four reflection lookups per deep-path segment are memoized.
        private static readonly ConcurrentDictionary<(Type Type, string Name), MemberInfo?> WritableMemberCache = new();

        /// <summary>
        /// Returns the underlying type of a <see cref="Nullable{T}"/>, or the type itself when it is not nullable.
        /// </summary>
        /// <param name="type">The type to unwrap.</param>
        /// <returns>The non-nullable underlying type, or <paramref name="type"/> when it is not a <see cref="Nullable{T}"/>.</returns>
        public static Type EnsureNotNullable(this Type type) => Nullable.GetUnderlyingType(type) ?? type;

        /// <summary>
        /// Returns the generic type definition of a generic type, or <see langword="null"/> when the type is not generic.
        /// </summary>
        /// <param name="type">The type to inspect.</param>
        /// <returns>The generic type definition, or <see langword="null"/> when <paramref name="type"/> is not generic.</returns>
        public static Type? GetGenericTypeDefinitionOrNull(this Type type) => type.IsGenericType ? type.GetGenericTypeDefinition() : null;

        /// <summary>
        /// Returns the registered builder name from the <see cref="ModelBuilderAttribute"/> on the given builder type.
        /// </summary>
        /// <param name="modelBuilderType">The builder type to read the attribute from.</param>
        /// <returns>The configured builder name, or <see langword="null"/> when no <see cref="ModelBuilderAttribute"/> is present.</returns>
        public static string? GetModelBuilderName(this Type modelBuilderType) => modelBuilderType.GetCustomAttribute<ModelBuilderAttribute>()?.Name;

        /// <summary>
        /// Determines whether the given builder type carries the specified builder name (case-insensitive).
        /// </summary>
        /// <param name="modelBuilderType">The builder type to check.</param>
        /// <param name="name">The builder name to compare against.</param>
        /// <returns><see langword="true"/> when the builder's name equals <paramref name="name"/>, ignoring case; otherwise <see langword="false"/>.</returns>
        public static bool HasModelBuilderName(this Type modelBuilderType, string name) =>
            string.Equals(modelBuilderType.GetModelBuilderName(), name, StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Returns the model type a concrete builder builds, i.e. the <c>T</c> of its
        /// <see cref="IModelBuilder{T}"/>, or <see langword="null"/> when the type does not implement it.
        /// </summary>
        /// <param name="modelBuilderType">The builder type to inspect.</param>
        /// <returns>The model type <c>T</c>, or <see langword="null"/> when <paramref name="modelBuilderType"/> does not implement <see cref="IModelBuilder{T}"/>.</returns>
        public static Type? GetModelType(this Type modelBuilderType)
        {
            var interfaceType = modelBuilderType
                .GetInterfaces()
                .FirstOrDefault(i => i.GetGenericTypeDefinitionOrNull() == typeof(IModelBuilder<>));

            return interfaceType?.GetGenericArguments()[0];
        }

        /// <summary>
        /// Returns the key and value type arguments when the type is a <see cref="Dictionary{TKey,TValue}"/>
        /// or <see cref="IDictionary{TKey,TValue}"/>; otherwise <see langword="null"/>.
        /// </summary>
        /// <param name="type">The type to inspect.</param>
        /// <returns>A tuple of the key and value types, or <see langword="null"/> when <paramref name="type"/> is not a supported dictionary type.</returns>
        public static (Type KeyType, Type ValueType)? GetDictionaryTypeArgumentsOrNull(this Type type)
        {
            if (type.GetGenericTypeDefinitionOrNull() is not { } genericDef
                || (genericDef != typeof(Dictionary<,>) && genericDef != typeof(IDictionary<,>)))
            {
                return null;
            }

            var args = type.GetGenericArguments();
            return (args[0], args[1]);
        }

        /// <summary>
        /// Returns the element type when the type is a <see cref="HashSet{T}"/> or <see cref="ISet{T}"/>;
        /// otherwise <see langword="null"/>.
        /// </summary>
        /// <param name="type">The type to inspect.</param>
        /// <returns>The set element type, or <see langword="null"/> when <paramref name="type"/> is not a supported set type.</returns>
        public static Type? GetSetElementTypeOrNull(this Type type)
        {
            if (type.GetGenericTypeDefinitionOrNull() is not { } genericDef
                || (genericDef != typeof(HashSet<>) && genericDef != typeof(ISet<>)))
            {
                return null;
            }

            return type.GetGenericArguments()[0];
        }

        /// <summary>
        /// Returns the element type of a list-like type: arrays, the common generic list/collection
        /// interfaces and <see cref="List{T}"/>, or a type implementing <see cref="IList{T}"/> /
        /// <see cref="IReadOnlyList{T}"/>.
        /// </summary>
        /// <param name="listType">The list-like type to inspect.</param>
        /// <returns>The element type, or <see langword="null"/> when no list element type can be determined.</returns>
        public static Type? GetListElementType(this Type listType)
        {
            if (listType.IsArray)
            {
                return listType.GetElementType();
            }

            if (listType.IsGenericType)
            {
                Type genericDef = listType.GetGenericTypeDefinition();
                if (genericDef == typeof(IList<>)
                    || genericDef == typeof(List<>)
                    || genericDef == typeof(ICollection<>)
                    || genericDef == typeof(IReadOnlyList<>)
                    || genericDef == typeof(IReadOnlyCollection<>)
                    || genericDef == typeof(IEnumerable<>))
                {
                    return listType.GetGenericArguments()[0];
                }
            }

            foreach (Type intf in listType.GetInterfaces())
            {
                if (intf.IsGenericType
                    && (intf.GetGenericTypeDefinition() == typeof(IList<>)
                        || intf.GetGenericTypeDefinition() == typeof(IReadOnlyList<>)))
                {
                    return intf.GetGenericArguments()[0];
                }
            }

            return null;
        }

        /// <summary>
        /// Attempts to resolve a writable member (a settable property or a field, including the
        /// conventional <c>_name</c> and compiler-generated backing field) matching the given name,
        /// case-insensitively. Results are cached per (type, name) pair.
        /// </summary>
        /// <param name="type">The declaring type to search.</param>
        /// <param name="name">The member name to resolve (matched case-insensitively).</param>
        /// <param name="member">When this method returns <see langword="true"/>, the resolved writable member; otherwise undefined.</param>
        /// <returns><see langword="true"/> when a writable member was found; otherwise <see langword="false"/>.</returns>
        public static bool TryGetWritableMember(this Type type, string name, out MemberInfo member)
        {
            var resolved = WritableMemberCache.GetOrAdd((type, name), static key => ResolveWritableMember(key.Type, key.Name));
            member = resolved!;
            return resolved is not null;
        }

        private static MemberInfo? ResolveWritableMember(Type type, string name)
        {
            var flags = BindingFlags.Public | BindingFlags.NonPublic |
                        BindingFlags.Instance | BindingFlags.IgnoreCase;

            var prop = type.GetProperty(name, flags);

            if (prop != null && prop.CanWrite)
            {
                return prop;
            }

            return type.GetField(name, flags) ?? type.GetField("_" + name, flags) ?? type.GetField($"<{name}>k__BackingField", flags);
        }

        /// <summary>
        /// Grows the list until <paramref name="targetIndex"/> is a valid index, padding with newly
        /// created elements. For the final path segment (<paramref name="isLast"/>) value-type elements
        /// get their default and reference-type elements get <see langword="null"/>; for intermediate
        /// segments each padding element is built through the provider so it can be navigated further.
        /// </summary>
        /// <param name="list">The list to grow in place.</param>
        /// <param name="targetIndex">The index that must become valid.</param>
        /// <param name="isLast">Whether this is the last segment of the deep path being resolved.</param>
        /// <param name="elementType">The element type used to create padding values.</param>
        /// <param name="provider">The provider used to build intermediate elements.</param>
        public static void EnsureListSize(this IList list, int targetIndex, bool isLast, Type elementType, IModelBuilderProvider provider)
        {
            while (list.Count <= targetIndex)
            {
                object? toAdd;

                if (isLast)
                {
                    if (elementType.IsValueType)
                    {
                        toAdd = Activator.CreateInstance(elementType);
                    }
                    else
                    {
                        toAdd = null;
                    }
                }
                else
                {
                    toAdd = provider
                        .For(elementType)
                        .Build();
                }

                list.Add(toAdd);
            }
        }

        /// <summary>
        /// Returns the declared type of a property or field member.
        /// </summary>
        /// <param name="member">The property or field member.</param>
        /// <returns>The <see cref="PropertyInfo.PropertyType"/> or <see cref="FieldInfo.FieldType"/>.</returns>
        /// <exception cref="NotSupportedException">Thrown when <paramref name="member"/> is neither a property nor a field.</exception>
        public static Type GetMemberType(this MemberInfo member)
        {
            return member switch
            {
                PropertyInfo p => p.PropertyType,
                FieldInfo f => f.FieldType,
                _ => throw new NotSupportedException($"Member type '{member.MemberType}' is not supported.")
            };
        }

        /// <summary>
        /// Reads the value of a property or field member from the given target instance.
        /// </summary>
        /// <param name="member">The property or field member to read.</param>
        /// <param name="target">The instance to read the value from.</param>
        /// <returns>The member's current value.</returns>
        /// <exception cref="NotSupportedException">Thrown when <paramref name="member"/> is neither a property nor a field.</exception>
        public static object? GetMemberValue(this MemberInfo member, object target)
        {
            return member switch
            {
                PropertyInfo p => p.GetValue(target),
                FieldInfo f => f.GetValue(target),
                _ => throw new NotSupportedException($"Member type '{member.MemberType}' not supported."),
            };
        }

        /// <summary>
        /// Writes a value to a property or field member on the given target instance. When a property
        /// has no accessible setter, a matching writable member (backing field or <c>_name</c> field)
        /// is resolved and used instead.
        /// </summary>
        /// <param name="member">The property or field member to write.</param>
        /// <param name="target">The instance to write the value to.</param>
        /// <param name="value">The value to assign.</param>
        /// <exception cref="InvalidOperationException">Thrown when a read-only property has no writable backing member.</exception>
        /// <exception cref="NotSupportedException">Thrown when <paramref name="member"/> is neither a property nor a field.</exception>
        public static void SetMemberValue(this MemberInfo member, object target, object? value)
        {
            switch (member)
            {
                case PropertyInfo p:
                    {
                        if (p.CanWrite)
                        {
                            p.SetValue(target, value);
                        }
                        else if ((p.ReflectedType ?? p.DeclaringType!).TryGetWritableMember(p.Name, out var wm))
                        {
                            wm.SetMemberValue(target, value);
                        }
                        else
                        {
                            throw new InvalidOperationException($"Unable to set member {member}");
                        }
                        break;
                    }
                case FieldInfo f:
                    {
                        f.SetValue(target, value);
                        break;
                    }
                default:
                    {
                        throw new NotSupportedException($"Member type {member.MemberType} not supported.");
                    }
            }
        }

        /// <summary>
        /// Returns the property name if the expression is a direct (non-deep) property access
        /// (<c>m =&gt; m.Name</c> yields <c>"Name"</c>). Anything else — deep access, method call,
        /// field, indexer, etc. — yields <see langword="null"/>. Boxing conversions to
        /// <see cref="object"/> are unwrapped first.
        /// </summary>
        /// <param name="expr">The lambda expression to inspect.</param>
        /// <returns>The shallow property name, or <see langword="null"/> when the expression is not a direct property access.</returns>
        public static string? GetShallowPropertyName(this LambdaExpression expr)
        {
            if (expr == null)
            {
                return null;
            }

            // Handle boxing/conversion to object: m => (object)m.Name
            Expression body = expr.Body;
            if (body is UnaryExpression { NodeType: ExpressionType.Convert or ExpressionType.ConvertChecked } u)
                body = u.Operand;

            // Must be a MemberExpression: m => m.Name
            if (body is not MemberExpression member)
                return null;

            // Must be directly on the parameter (not deep): member.Expression == parameter
            // e.g. m => m.Adres.Straat -> member.Expression is another MemberExpression, so returns null.
            if (member.Expression is not ParameterExpression)
                return null;

            return member.Member.Name;
        }

        /// <summary>
        /// Returns the default value for a type: <see langword="null"/> for reference and nullable
        /// value types, or a fresh default instance (via <see cref="Activator.CreateInstance(Type)"/>)
        /// for non-nullable value types.
        /// </summary>
        /// <param name="type">The type to produce a default value for.</param>
        /// <returns>The type's default value.</returns>
        public static object? GetDefaultValue(this Type type)
        {
            return Nullable.GetUnderlyingType(type) != null || !type.IsValueType ? null : Activator.CreateInstance(type);
        }

        /// <summary>
        /// Returns the parameter's declared default value, or <see langword="null"/> when it has none.
        /// </summary>
        /// <param name="par">The parameter to inspect.</param>
        /// <returns>The declared default value, or <see langword="null"/> when the parameter has no default.</returns>
        public static object? GetParameterDefaultValueOrNull(this ParameterInfo par)
        {
            return par.HasDefaultValue ? par.DefaultValue : null;
        }

    }
}
