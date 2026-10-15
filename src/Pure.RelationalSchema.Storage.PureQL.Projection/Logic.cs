using System.Linq.Expressions;
using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Boolean connectives and the conditional operators. and, or and not test
// their conditions, so a null condition counts as false; if takes then when
// its condition is true and else otherwise; coalesce is the first non-null
// operand. All of them evaluate lazily, as their C# counterparts do.
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
}
