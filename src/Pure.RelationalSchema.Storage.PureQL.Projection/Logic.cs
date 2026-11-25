using System.Linq.Expressions;
using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Boolean connectives and the conditional operators. and, or and not test
// their conditions, so a null condition counts as false; if takes then when
// its condition is true and else otherwise; coalesce is the first non-null
// operand. All of them evaluate lazily, as their C# counterparts do.
// greatest and least are the largest and the smallest operand by the order
// of their type, null when any operand is null; nullIfEqual is null when its
// operands are equal, as equal compares them, and its left operand otherwise.
internal static class Logic
{
    public static Expression And(object node, Scope scope)
    {
        return ModelNode
            .Children(node, "Conditions")
            .Select(scope.Test)
            .Aggregate(Expression.AndAlso);
    }

    public static Expression Or(object node, Scope scope)
    {
        return ModelNode
            .Children(node, "Conditions")
            .Select(scope.Test)
            .Aggregate(Expression.OrElse);
    }

    public static Expression Not(object node, Scope scope)
    {
        return Expression.Not(scope.Test(ModelNode.Child(node, "Condition")));
    }

    public static Expression If(object node, Scope scope)
    {
        Type type = ValueTypes.Clr(((IOperator)node).Type);

        return Expression.Condition(
            scope.Test(ModelNode.Child(node, "Condition")),
            Typed.As(scope.Compile(ModelNode.Child(node, "Then")), type),
            Typed.As(scope.Compile(ModelNode.Child(node, "Else")), type)
        );
    }

    public static Expression Coalesce(object node, Scope scope)
    {
        Type type = ValueTypes.Clr(((IOperator)node).Type);

        return ModelNode
            .Children(node, "Values")
            .Select(operand => Typed.As(scope.Compile(operand), type))
            .Reverse()
            .Aggregate((rest, value) => Expression.Coalesce(value, rest));
    }

    public static Expression Greatest(object node, Scope scope)
    {
        return Extreme(node, scope, nameof(Math.Max));
    }

    public static Expression Least(object node, Scope scope)
    {
        return Extreme(node, scope, nameof(Math.Min));
    }

    public static Expression NullIfEqual(object node, Scope scope)
    {
        Type type = ValueTypes.Clr(((IOperator)node).Type);
        Expression left = scope.Compile(ModelNode.Child(node, "Left"));

        return Expression.Condition(
            Comparison.Equal(left, scope.Compile(ModelNode.Child(node, "Right"))),
            Typed.Null(type),
            Typed.As(left, type)
        );
    }

    // Numbers fold through Math.Max and Math.Min; every other type is the
    // Max or Min of the list of its operands, strings ordered ordinally like
    // every string comparison of the tree.
    private static Expression Extreme(object node, Scope scope, string name)
    {
        Type type = ValueTypes.Clr(((IOperator)node).Type);
        Type item = Nullable.GetUnderlyingType(type) ?? type;

        return Lifted.All(
            [
                .. ModelNode
                    .Children(node, "Values")
                    .Select(operand => Typed.As(scope.Compile(operand), type)),
            ],
            type,
            values =>
            {
                if (item == typeof(long) || item == typeof(decimal))
                {
                    return values.Aggregate(
                        (left, right) => Functions.Call(typeof(Math), name, left, right)
                    );
                }

                Expression list = Expression.NewArrayInit(item, values);

                return item == typeof(string)
                    ? name == nameof(Math.Max)
                        ? Linq.Max(list, Comparison.Ordinal)
                        : Linq.Min(list, Comparison.Ordinal)
                    : name == nameof(Math.Max)
                        ? Linq.Max(list)
                    : Linq.Min(list);
            }
        );
    }
}
