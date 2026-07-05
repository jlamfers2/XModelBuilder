using System.Reflection;
using System.Text;

namespace XModelBuilder.Core;

/// <summary>
/// Extension methods that render reflection metadata (<see cref="Type"/>, <see cref="FieldInfo"/>,
/// <see cref="PropertyInfo"/>, <see cref="MethodInfo"/>) into readable, C#-like display names.
/// Uses C# keyword aliases (<c>int</c>, <c>string</c>, …), formats generics as
/// <c>Dictionary&lt;string,Person&gt;</c>, nullable value types as <c>T?</c>, and arrays as
/// <c>T[]</c>. Primarily used to produce friendly diagnostic and error messages.
/// </summary>
public static class FriendlyNameExtensions
{
    // Maps CLR types to their C# keyword aliases.
    private static readonly Dictionary<Type, string> _knownTypeAliases = new()
{
    { typeof(bool),    "bool" },
    { typeof(byte),    "byte" },
    { typeof(sbyte),   "sbyte" },
    { typeof(char),    "char" },
    { typeof(decimal), "decimal" },
    { typeof(double),  "double" },
    { typeof(float),   "float" },
    { typeof(int),     "int" },
    { typeof(uint),    "uint" },
    { typeof(long),    "long" },
    { typeof(ulong),   "ulong" },
    { typeof(short),   "short" },
    { typeof(ushort),  "ushort" },
    { typeof(object),  "object" },
    { typeof(string),  "string" },
    { typeof(void),    "void" }
};

    /// <summary>
    /// Returns a friendly name for the given <see cref="Type"/>.
    /// Example outputs: IList&lt;string&gt;, Dictionary&lt;string,Person&gt;, Dictionary&lt;TKey,TValue&gt;.
    /// Generic argument type names never include namespaces and use C# keyword aliases where applicable.
    /// </summary>
    /// <param name="type">The type to format.</param>
    /// <param name="includeNamespace">
    /// If true, the outer type will include its namespace (if any).
    /// Generic argument type names will never include namespaces.
    /// </param>
    /// <returns>The friendly, C#-like name of the type.</returns>
    /// <exception cref="ArgumentNullException">Thrown when <paramref name="type"/> is <see langword="null"/>.</exception>
    public static string GetFriendlyName(this Type type, bool includeNamespace = false)
    {
        ArgumentNullException.ThrowIfNull(type);

        // Nullable<T> => T?
        if (IsNullableType(type))
        {
            var innerType = Nullable.GetUnderlyingType(type)!;
            var innerName = innerType.GetFriendlyName(includeNamespace: false);
            return innerName + "?";
        }

        // Arrays
        if (type.IsArray)
        {
            var elementType = type.GetElementType()!;
            var elementName = elementType.GetFriendlyName(includeNamespace);
            var rank = type.GetArrayRank();

            if (rank == 1)
            {
                return elementName + "[]";
            }

            // Multi-dimensional arrays: e.g. int[,,]
            return elementName + "[" + new string(',', rank - 1) + "]";
        }

        // Generic types (open or closed)
        if (type.IsGenericType)
        {
            return GetGenericTypeFriendlyName(type, includeNamespace);
        }

        // Non-generic simple type
        return GetSimpleTypeName(type, includeNamespace);
    }

    /// <summary>
    /// Returns a friendly name for the given <see cref="FieldInfo"/> field, in the form
    /// <c>FieldType DeclaringType.FieldName</c>.
    /// </summary>
    /// <param name="field">The field whose type must be formatted.</param>
    /// <returns>The friendly name of the field.</returns>
    public static string GetFriendlyName(this FieldInfo field)
    {
        return $"{field.FieldType.GetFriendlyName()} {field.DeclaringType?.GetFriendlyName()}.{field.Name}";
    }

    /// <summary>
    /// Returns a friendly name for the given <see cref="PropertyInfo"/> property, in the form
    /// <c>PropertyType DeclaringType.PropertyName</c>.
    /// </summary>
    /// <param name="property">The property whose type must be formatted.</param>
    /// <returns>The friendly name of the property.</returns>
    public static string GetFriendlyName(this PropertyInfo property)
    {
        return $"{property.PropertyType.GetFriendlyName()} {property.DeclaringType?.GetFriendlyName()}.{property.Name}";
    }

    /// <summary>
    /// Returns a friendly name for the given <see cref="MethodInfo"/> method, including its
    /// return type, declaring type, generic type arguments (if any) and parameter list, in the
    /// form <c>ReturnType DeclaringType.MethodName&lt;T&gt;(ParamType name, …)</c>.
    /// </summary>
    /// <param name="method">The method to format.</param>
    /// <returns>The friendly name of the method.</returns>
    public static string GetFriendlyName(this MethodInfo method)
    {
        var sb = new StringBuilder();
        sb.Append($"{method.ReturnType.GetFriendlyName()} {method.DeclaringType?.GetFriendlyName()}.{method.Name}");
        var comma = string.Empty;
        if (method.IsGenericMethod)
        {
            sb.Append('<');
            foreach (var t in method.GetGenericArguments())
            {
                sb.Append($"{comma}{t.GetFriendlyName()}");
                comma = ",";
            }
            sb.Append('>');
        }
        comma = string.Empty;
        sb.Append('(');
        foreach (var p in method.GetParameters())
        {
            sb.Append($"{comma}{p.ParameterType.GetFriendlyName()} {p.Name}");
            comma = ", ";
        }
        sb.Append(')');
        return sb.ToString();
    }

    /// <summary>
    /// Returns a friendly name for generic types, including generic type definitions.
    /// </summary>
    private static string GetGenericTypeFriendlyName(Type type, bool includeNamespace)
    {
        // For closed generic types we still want to base the name on the generic type definition
        // (to avoid the arity suffix like `1, `2, etc.).
        var genericDefinition = type.IsGenericTypeDefinition ? type : type.GetGenericTypeDefinition();

        var genericArguments = type.GetGenericArguments();
        var genericArgumentNames = genericArguments.Select(arg =>
        {
            // Generic type parameters (e.g. TKey, TValue) keep their own name
            if (arg.IsGenericParameter)
            {
                return arg.Name;
            }

            // Concrete generic arguments must never include namespace,
            // and should use aliases where applicable (string, int, etc.).
            return GetSimpleTypeName(arg, includeNamespace: false);
        });

        var baseName = genericDefinition.Name;
        var tickIndex = baseName.IndexOf('`');
        if (tickIndex >= 0)
        {
            baseName = baseName[..tickIndex];
        }

        if (includeNamespace && !string.IsNullOrEmpty(genericDefinition.Namespace))
        {
            baseName = genericDefinition.Namespace + "." + baseName;
        }

        return $"{baseName}<{string.Join(",", genericArgumentNames)}>";
    }

    /// <summary>
    /// Returns a friendly name for a non-generic simple type, optionally including its namespace.
    /// Aliases are used where possible (string, int, etc.).
    /// </summary>
    private static string GetSimpleTypeName(Type type, bool includeNamespace)
    {
        if (_knownTypeAliases.TryGetValue(type, out var alias))
        {
            return alias;
        }

        var name = type.Name;

        if (includeNamespace && !string.IsNullOrEmpty(type.Namespace))
        {
            return type.Namespace + "." + name;
        }

        return name;
    }

    /// <summary>
    /// Determines whether the provided type is a Nullable&lt;T&gt; type.
    /// </summary>
    private static bool IsNullableType(Type type)
    {
        return type.IsGenericType &&
               type.GetGenericTypeDefinition() == typeof(Nullable<>);
    }
}