using System.Collections.Concurrent;
using System.Reflection;

// Based on: https://stackoverflow.com/questions/129389/how-do-you-do-a-deep-copy-of-an-object-in-net

namespace XModelBuilder.Core
{
    /// <summary>
    /// Reflection-based deep cloning via <see cref="object.MemberwiseClone"/>.
    /// Handles cyclic object graphs, private fields (including base-type private fields),
    /// and multi-dimensional arrays.
    /// </summary>
    /// <remarks>
    /// Known semantics/limitations:
    /// <list type="bullet">
    /// <item>Delegates (events, callbacks, <c>Func&lt;&gt;</c>/<c>Action&lt;&gt;</c> fields) are set to <c>null</c> in the clone.</item>
    /// <item>Strings and immutable value types (enums, <see cref="decimal"/>, <see cref="DateTime"/>,
    /// <see cref="DateTimeOffset"/>, <see cref="TimeSpan"/>, <see cref="Guid"/>) are shared, not copied — safe because they are immutable.</item>
    /// <item>Types holding unmanaged resources (streams, DB connections, handles) are NOT safe to clone:
    /// the handle value is copied, leaving two objects that both think they own the resource.</item>
    /// <item>Non-zero-lower-bound arrays are not supported and throw <see cref="NotSupportedException"/>.</item>
    /// </list>
    /// </remarks>
    public static class DeepCloneExtension
    {
        private static readonly MethodInfo _cloneMethod =
#pragma warning disable S3011
            typeof(object).GetMethod("MemberwiseClone", BindingFlags.NonPublic | BindingFlags.Instance)!;
#pragma warning restore S3011

        // Cache reflected fields per (type, flags) — reflection field lookup is the hot path.
        private static readonly ConcurrentDictionary<(Type Type, BindingFlags Flags), FieldInfo[]> _fieldCache = new();

        /// <summary>
        /// Determines whether values of <paramref name="type"/> are safe to SHARE between the original
        /// and its clone rather than deep-copied — i.e. the type is immutable, so a reference copy is
        /// indistinguishable from a deep copy. Covers <see cref="string"/>, enums, the built-in
        /// primitives and the common immutable value types (<see cref="decimal"/>, <see cref="DateTime"/>,
        /// <see cref="DateTimeOffset"/>, <see cref="TimeSpan"/>, <see cref="Guid"/>).
        /// </summary>
        /// <param name="type">The type to classify.</param>
        /// <returns>
        /// <see langword="true"/> if <paramref name="type"/> is treated as an immutable primitive and may
        /// be shared; otherwise <see langword="false"/>.
        /// </returns>
        public static bool IsImmutablePrimitive(this Type type)
        {
            if (type == typeof(string))
            {
                return true;
            }

            if (type.IsEnum)
            {
                return true;
            }

            if (type == typeof(decimal) || type == typeof(DateTime) || type == typeof(DateTimeOffset)
                || type == typeof(TimeSpan) || type == typeof(Guid))
            {
                return true;
            }

            return type.IsPrimitive; // IsPrimitive already implies IsValueType
        }

        /// <summary>
        /// Creates a deep clone of <paramref name="originalObject"/> via reflection and
        /// <see cref="object.MemberwiseClone"/>. Cyclic references are preserved (shared identity within
        /// the graph is kept), immutable values are shared, delegate fields become <see langword="null"/>,
        /// and multi-dimensional arrays are copied. See the remarks on <see cref="DeepCloneExtension"/> for
        /// the full semantics and limitations.
        /// </summary>
        /// <param name="originalObject">The object to clone; may be <see langword="null"/>.</param>
        /// <returns>
        /// A deep clone of <paramref name="originalObject"/>, or <see langword="null"/> when it is
        /// <see langword="null"/>.
        /// </returns>
        /// <exception cref="NotSupportedException">An array in the graph has a non-zero lower bound.</exception>
        public static object? DeepClone(this object? originalObject)
        {
            return InternalCopy(originalObject, new Dictionary<object, object>(ReferenceEqualityComparer.Instance));
        }

        /// <summary>
        /// Strongly-typed overload of <see cref="DeepClone(object?)"/>: creates a deep clone of
        /// <paramref name="original"/> and returns it as <typeparamref name="T"/>. See the remarks on
        /// <see cref="DeepCloneExtension"/> for the full semantics and limitations.
        /// </summary>
        /// <typeparam name="T">The static type of the value being cloned.</typeparam>
        /// <param name="original">The value to clone; may be <see langword="null"/>.</param>
        /// <returns>
        /// A deep clone of <paramref name="original"/>, or <see langword="null"/> when it is
        /// <see langword="null"/>.
        /// </returns>
        /// <exception cref="NotSupportedException">An array in the graph has a non-zero lower bound.</exception>
        public static T? DeepClone<T>(this T? original)
        {
            return (T?)DeepClone((object?)original);
        }

        private static object? InternalCopy(object? originalObject, IDictionary<object, object> visited)
        {
            if (originalObject == null)
            {
                return null;
            }

            var typeToReflect = originalObject.GetType();
            if (IsImmutablePrimitive(typeToReflect))
            {
                return originalObject;
            }

            if (visited.TryGetValue(originalObject, out var existingClone))
            {
                return existingClone;
            }

            // Delegates capture closures and invocation lists that are not meaningfully cloneable.
            if (typeof(Delegate).IsAssignableFrom(typeToReflect))
            {
                return null;
            }

            var cloneObject = _cloneMethod.Invoke(originalObject, null);
            if (cloneObject == null)
            {
                return null;
            }

            // Register the clone BEFORE descending into elements/fields so that
            // self-referencing structures (e.g. object[] a; a[0] = a;) resolve to
            // the in-progress clone instead of recursing forever.
            visited.Add(originalObject, cloneObject);

            if (typeToReflect.IsArray)
            {
                var elementType = typeToReflect.GetElementType()!;
                if (!IsImmutablePrimitive(elementType))
                {
                    var clonedArray = (Array)cloneObject;
                    clonedArray.ForEach((array, indices) =>
                        array.SetValue(InternalCopy(array.GetValue(indices), visited), indices));
                }
            }

            CopyFields(originalObject, visited, cloneObject, typeToReflect);
            RecursiveCopyBaseTypePrivateFields(originalObject, visited, cloneObject, typeToReflect);
            return cloneObject;
        }

        private static void RecursiveCopyBaseTypePrivateFields(object originalObject, IDictionary<object, object> visited, object cloneObject, Type typeToReflect)
        {
            if (typeToReflect.BaseType != null)
            {
                RecursiveCopyBaseTypePrivateFields(originalObject, visited, cloneObject, typeToReflect.BaseType);
#pragma warning disable S3011
                CopyFields(originalObject, visited, cloneObject, typeToReflect.BaseType,
                    BindingFlags.Instance | BindingFlags.NonPublic, static info => info.IsPrivate);
#pragma warning restore S3011
            }
        }

        private static void CopyFields(
            object originalObject,
            IDictionary<object, object> visited,
            object cloneObject,
            Type typeToReflect,
#pragma warning disable S3011
            BindingFlags bindingFlags = BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.FlattenHierarchy,
#pragma warning restore S3011
            Func<FieldInfo, bool>? filter = null)
        {
            var fields = _fieldCache.GetOrAdd((typeToReflect, bindingFlags),
                static key => key.Type.GetFields(key.Flags));

            foreach (FieldInfo fieldInfo in fields)
            {
                if (filter != null && !filter(fieldInfo))
                {
                    continue;
                }

                if (IsImmutablePrimitive(fieldInfo.FieldType))
                {
                    continue;
                }

                var originalFieldValue = fieldInfo.GetValue(originalObject);
                var clonedFieldValue = InternalCopy(originalFieldValue, visited);
                fieldInfo.SetValue(cloneObject, clonedFieldValue);
            }
        }
    }

    internal static class ArrayExtensions
    {
        /// <summary>
        /// Invokes <paramref name="action"/> once for every element index tuple of
        /// <paramref name="array"/>, supporting arrays of any rank (multi-dimensional). Returns
        /// immediately for an empty array.
        /// </summary>
        /// <param name="array">The array to walk; every dimension must have a zero-based lower bound.</param>
        /// <param name="action">
        /// The callback invoked per element, receiving the array itself and the current (zero-based) index tuple.
        /// </param>
        /// <exception cref="NotSupportedException">The array has a non-zero lower bound on any dimension.</exception>
        public static void ForEach(this Array array, Action<Array, int[]> action)
        {
            if (array.LongLength == 0)
            {
                return;
            }

            for (int i = 0; i < array.Rank; i++)
            {
                if (array.GetLowerBound(i) != 0)
                {
                    throw new NotSupportedException("Arrays with non-zero lower bounds are not supported.");
                }
            }

            var walker = new ArrayTraverse(array);
            do
            {
                action(array, walker.Position);
            }
            while (walker.Step());
        }
    }

    internal sealed class ArrayTraverse
    {
        /// <summary>The current zero-based index tuple, one element per array dimension.</summary>
        public int[] Position { get; }
        private readonly int[] _maxLengths;

        /// <summary>
        /// Initializes a traversal cursor positioned at the first element of <paramref name="array"/>.
        /// </summary>
        /// <param name="array">The array to traverse; its per-dimension lengths bound the walk.</param>
        public ArrayTraverse(Array array)
        {
            _maxLengths = new int[array.Rank];
            for (int i = 0; i < array.Rank; ++i)
            {
                _maxLengths[i] = array.GetLength(i) - 1;
            }
            Position = new int[array.Rank];
        }

        /// <summary>Advances <see cref="Position"/> to the next element in row-major order.</summary>
        /// <returns>
        /// <see langword="true"/> if the cursor advanced to a valid element; <see langword="false"/> if
        /// the traversal is complete.
        /// </returns>
        public bool Step()
        {
            for (int i = 0; i < Position.Length; ++i)
            {
                if (Position[i] < _maxLengths[i])
                {
                    Position[i]++;
                    for (int j = 0; j < i; j++)
                    {
                        Position[j] = 0;
                    }
                    return true;
                }
            }
            return false;
        }
    }
}
