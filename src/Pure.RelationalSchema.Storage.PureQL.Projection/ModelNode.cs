using System.Collections;
using System.Collections.Concurrent;
using System.Reflection;
using System.Runtime.CompilerServices;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Types;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Structural access to PureQL.CSharp.Model nodes. Every choice between nodes
// is a C# union, whose Value is the record it holds, and every record names
// its properties after the keys of the specification's JSON (values, left,
// right, condition, selector, over, ...), so one walker reads every node of
// every context.
internal static class ModelNode
{
    private static readonly Assembly ModelAssembly = typeof(PureQLQuery).Assembly;

    private static readonly ConcurrentDictionary<Type, PropertyInfo[]> ChildProperties =
        new();

    private static readonly ConcurrentDictionary<
        (Type Type, string Name),
        PropertyInfo?
    > Properties = new();

    public static object Unwrap(object node)
    {
        object current = node;

        while (current is IUnion { Value: object value })
        {
            current = value;
        }

        return current;
    }

    public static bool Has(object node, string property)
    {
        return Find(node, property) is not null;
    }

    public static object? Property(object node, string property)
    {
        return (
            Find(node, property)
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

    // The value of a property a node may lack or leave unset, out of its
    // union; null when there is none.
    public static object? Optional(object node, string property)
    {
        return Find(node, property)?.GetValue(node) is object value
            ? Unwrap(value)
            : null;
    }

    public static IEnumerable<object> Children(object node, string property)
    {
        return ((IEnumerable)Child(node, property)).Cast<object>();
    }

    public static IEnumerable<object> OptionalChildren(object node, string property)
    {
        return Optional(node, property) is IEnumerable many ? many.Cast<object>() : [];
    }

    // The type an expression declares: every leaf and every operator has one.
    public static IType TypeOf(object expression)
    {
        return (IType)Child(Unwrap(expression), "Type");
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

    private static PropertyInfo? Find(object node, string property)
    {
        return Properties.GetOrAdd(
            (node.GetType(), property),
            key => key.Type.GetProperty(key.Name)
        );
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
        return type.Assembly != ModelAssembly
            ? []
            :
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
        element = Nullable.GetUnderlyingType(element) ?? element;

        return element.Assembly == ModelAssembly && !element.IsEnum;
    }
}
