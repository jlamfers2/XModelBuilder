using System.Collections;
using System.Linq.Expressions;
using System.Reflection;

namespace XModelBuilder.Core
{
    /// <summary>
    /// Assigns a value at the end of a member-access lambda path (e.g.
    /// <c>m =&gt; m.Address.Street</c> or <c>m =&gt; m.Lines[2].Amount</c>), materializing any missing
    /// intermediate objects, arrays and lists along the way through the supplied provider. This is the
    /// strongly-typed lambda counterpart to <see cref="StringPathSetter"/>'s string deep-path setting.
    /// </summary>
    internal static class LambdaPathSetter
    {

        // small helper class for parsing results
        private sealed class ExpressionPathSegment(MemberInfo member, int? index)
        {
            public MemberInfo Member { get; } = member ?? throw new ArgumentNullException(nameof(member));
            public int? Index { get; } = index;
        }


        /// <summary>
        /// Sets <paramref name="value"/> at the member path described by <paramref name="path"/> on
        /// <paramref name="target"/>, creating any missing intermediate objects/collections via the
        /// provider. Strongly-typed wrapper around <see cref="SetMemberValueByLambdaUntyped"/>.
        /// </summary>
        /// <typeparam name="TModel">The root model type the path starts from.</typeparam>
        /// <typeparam name="TValue">The type of the value being assigned.</typeparam>
        /// <param name="target">The root instance to mutate.</param>
        /// <param name="path">A member-access expression describing the path to the target member.</param>
        /// <param name="value">The value to assign at the end of the path.</param>
        /// <param name="provider">The provider used to build missing intermediate objects and elements.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> contains no member access.</exception>
        /// <exception cref="NotSupportedException">Thrown when the path uses an unsupported expression or indexer shape.</exception>
        public static void SetMemberValueByLambda<TModel, TValue>(
            TModel target,
            Expression<Func<TModel, TValue>> path,
            TValue value,
            IModelBuilderProvider provider)
        {
            ArgumentNullException.ThrowIfNull(target);
            SetMemberValueByLambdaUntyped(target, path, value, provider);
        }

        /// <summary>
        /// Sets <paramref name="value"/> at the member path described by <paramref name="path"/> on
        /// <paramref name="target"/>. Walks each path segment, creating missing intermediate objects,
        /// growing arrays and lists (padding with default/null or built elements) as needed, and
        /// assigns the value at the final segment.
        /// </summary>
        /// <param name="target">The root instance to mutate.</param>
        /// <param name="path">The member-access lambda describing the path to the target member.</param>
        /// <param name="value">The value to assign at the end of the path.</param>
        /// <param name="provider">The provider used to build missing intermediate objects and elements.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="target"/> or <paramref name="path"/> is <see langword="null"/>.</exception>
        /// <exception cref="ArgumentException">Thrown when <paramref name="path"/> contains no member access.</exception>
        /// <exception cref="NotSupportedException">Thrown when a member is not an indexable collection, or the path uses an unsupported expression or indexer shape.</exception>
        public static void SetMemberValueByLambdaUntyped(
            object target,
            LambdaExpression path,
            object? value,
            IModelBuilderProvider provider
            )
        {
            ArgumentNullException.ThrowIfNull(target);
            ArgumentNullException.ThrowIfNull(path);

            var segments = ParseExpressionPath(path.Body);
            if (segments.Count == 0)
            {
                throw new ArgumentException(
                    "Path expression must contain at least one member access.",
                    nameof(path));
            }

            object current = target!;
            Type currentType = target.GetType();

            for (int i = 0; i < segments.Count; i++)
            {
                var segment = segments[i];
                bool isLast = i == segments.Count - 1;

                MemberInfo member = segment.Member;
                Type memberType = member.GetMemberType();
                object? memberValue = member.GetMemberValue(current);

                if (segment.Index.HasValue)
                {
                    int index = segment.Index.Value;

                    // Array
                    if (memberType.IsArray)
                    {
                        Type elementType = memberType.GetElementType() ?? typeof(object);
                        var array = memberValue as Array;

                        if (array == null || array.Length <= index)
                        {
                            int newLength = index + 1;
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
                            array!.SetValue(value, index);
                        }
                        else
                        {
                            object? element = array!.GetValue(index);
                            if (element == null)
                            {
                                element = provider
                                    .For(elementType)
                                    .Build();
                                array.SetValue(element, index);
                            }

                            current = element!;
                            currentType = element.GetType();
                        }
                    }
                    // IList / List<T> etc.
                    else
                    {
                        if (memberValue == null)
                        {
                            if (memberType.IsInterface && memberType.IsGenericType)
                            {
                                memberValue = Activator.CreateInstance(typeof(List<>).MakeGenericType(memberType.GetGenericArguments()[0]));
                            }
                            else if (memberType == typeof(IList))
                            {
                                memberValue = new List<object>();
                            }
                            else
                            {
                                memberValue = Activator.CreateInstance(memberType);
                            }
                            member.SetMemberValue(current, memberValue);
                        }

                        if (memberValue is not IList list)
                        {
                            throw new NotSupportedException(
                                $"Member '{member.Name}' on type '{currentType.FullName}' is not an indexable collection.");
                        }

                        Type elementType = memberType.GetListElementType() ?? typeof(object);

                        // Make sure the list is large enough; for the final step we fill it with default/null.
                        list.EnsureListSize(index, isLast, elementType, provider);

                        if (isLast)
                        {
                            list[index] = value!;
                        }
                        else
                        {
                            object? element = list[index];
                            if (element == null)
                            {
                                element = provider
                                    .For(elementType)
                                    .Build();
                                list[index] = element;
                            }

                            current = element!;
                            currentType = element.GetType();
                        }
                    }
                }
                else
                {
                    // No indexer: it is a property/field
                    if (isLast)
                    {
                        member.SetMemberValue(current, value);
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
            }
        }

        private static List<ExpressionPathSegment> ParseExpressionPath(Expression expression)
        {
            var segments = new List<ExpressionPathSegment>();
            Expression? current = expression;

            // Strip casts: x => (object)x.Address.Street
            while (current is UnaryExpression u &&
                   (u.NodeType == ExpressionType.Convert || u.NodeType == ExpressionType.ConvertChecked))
            {
                current = u.Operand;
            }

            while (current != null && current.NodeType != ExpressionType.Parameter)
            {
                switch (current.NodeType)
                {
                    case ExpressionType.MemberAccess:
                        {
                            var me = (MemberExpression)current;
                            segments.Add(new ExpressionPathSegment(me.Member, null));
                            current = me.Expression!;
                            break;
                        }

                    case ExpressionType.Index:
                        {
                            var ie = (IndexExpression)current;

                            if (ie.Arguments.Count != 1)
                            {
                                throw new NotSupportedException("Only single-argument indexers are supported.");
                            }

                            if (ie.Arguments[0] is not ConstantExpression constIndex ||
                                constIndex.Value is not int idx)
                            {
                                throw new NotSupportedException("Only constant integer indexers are supported.");
                            }

                            if (ie.Object is not MemberExpression collectionMember)
                            {
                                throw new NotSupportedException("Unsupported indexer expression shape.");
                            }

                            segments.Add(new ExpressionPathSegment(collectionMember.Member, idx));
                            current = collectionMember.Expression!;
                            break;
                        }

                    case ExpressionType.ArrayIndex:
                        {
                            var be = (BinaryExpression)current;

                            if (be.Right is not ConstantExpression constIdx ||
                                constIdx.Value is not int arrayIdx)
                            {
                                throw new NotSupportedException("Only constant integer array indexes are supported.");
                            }

                            if (be.Left is not MemberExpression arrayMember)
                            {
                                throw new NotSupportedException("Unsupported array index expression shape.");
                            }

                            segments.Add(new ExpressionPathSegment(arrayMember.Member, arrayIdx));
                            current = arrayMember.Expression!;
                            break;
                        }
                    case ExpressionType.Call:
                        {
                            var ce = (MethodCallExpression)current;

                            // Support indexer method calls like get_Item(1)
                            if (ce.Method.Name != "get_Item")
                            {
                                throw new NotSupportedException("Only indexer method calls (get_Item) are supported.");
                            }

                            if (ce.Arguments.Count != 1)
                            {
                                throw new NotSupportedException("Only single-argument indexers are supported.");
                            }

                            if (ce.Arguments[0] is not ConstantExpression constIndex ||
                                constIndex.Value is not int idx)
                            {
                                throw new NotSupportedException("Only constant integer indexers are supported.");
                            }

                            if (ce.Object is not MemberExpression collectionMember)
                            {
                                throw new NotSupportedException("Unsupported indexer expression shape.");
                            }

                            segments.Add(new ExpressionPathSegment(collectionMember.Member, idx));
                            current = collectionMember.Expression!;
                            break;
                        }
                    default:
                        throw new NotSupportedException(
                            $"Expression type {current.NodeType} is not supported in path.");
                }
            }

            // segments are orderd leaf => root; reverse to root => leaf.
            segments.Reverse();
            return segments;
        }
    }
}
