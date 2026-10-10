using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using OneOf;
using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Structural access to PureQL.CSharp.Model nodes. Every union is a OneOf over
// its cases, and every operator record names its operands after the keys of
// the specification's JSON (values, left, right, condition, selector, ...),
// so one walker reads every node of every context.
internal static class ModelNode
{
    private static readonly Assembly ModelAssembly = typeof(PureQLQuery).Assembly;

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ChildProperties =
        new();

    public static object Unwrap(object node)
    {
        object current = node;

        while (current is IOneOf union)
        {
            current = union.Value;
        }

        return current;
    }

    public static bool Has(object node, string property)
    {
        return node.GetType().GetProperty(property) is not null;
    }

    public static object? Property(object node, string property)
    {
        return (
            node.GetType().GetProperty(property)
            ?? throw new NotSupportedException(
                $"{node.GetType().Name} has no '{property}' operand."
            )
        ).GetValue(node);
    }

    public static object Child(object node, string property)
    {
        return Property(node, property)
            ?? throw new ArgumentException(
                $"{node.GetType().Name} has no value for '{property}'."
            );
    }

    public static IEnumerable<object> Children(object node, string property)
    {
        return ((IEnumerable)Child(node, property)).Cast<object>();
    }

    // The root and every node below it, unwrapped from their unions, depth
    // first. A node for which descend is false is yielded but not entered.
    public static IEnumerable<object> Nodes(object root, Func<object, bool> descend)
    {
        Stack<object> pending = new();
        pending.Push(root);

        while (pending.Count > 0)
        {
            object node = Unwrap(pending.Pop());

            yield return node;

            if (descend(node))
            {
                foreach (object child in DirectChildren(node).Reverse())
                {
                    pending.Push(child);
                }
            }
        }
    }

    private static IEnumerable<object> DirectChildren(object node)
    {
        foreach (PropertyInfo property in ChildProperties.GetOrAdd(node.GetType(), Of))
        {
            object? value = property.GetValue(node);

            if (value is IEnumerable many and not string)
            {
                foreach (object child in many)
                {
                    yield return child;
                }
            }
            else if (value is not null)
            {
                yield return value;
            }
        }
    }

    private static PropertyInfo[] Of(Type type)
    {
        return
        [
            .. type.GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(property => property.GetIndexParameters().Length == 0)
                .Where(property => IsModelType(property.PropertyType)),
        ];
    }

    private static bool IsModelType(Type type)
    {
        Type element = type.IsGenericType
            && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
            ? type.GetGenericArguments()[0]
            : type;

        return element.Assembly == ModelAssembly && !element.IsEnum;
    }
}
