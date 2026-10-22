using System.Linq.Expressions;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.Types;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The list operand of in, as a sequence of the type the operand compares in:
// a list literal is a constant array, a list parameter a captured sequence
// and a subquery column the subquery's rows projected to that column.
internal static class Lists
{
    public static Expression Values(object list, Scope scope, Type item)
    {
        object leaf = ModelNode.Unwrap(list);

        return leaf switch
        {
            IParameter parameter => Converted(scope.Parameter(parameter), item),
            ILiteral literal => Expression.Constant(Literals.Items(literal, item)),
            _ => scope.SubqueryColumn(
                (string)ModelNode.Child(leaf, "Subquery"),
                (string)ModelNode.Child(leaf, "Field"),
                item
            ),
        };
    }

    // The type of the items of a list, by its declared type.
    public static Type Item(object list)
    {
        return ValueTypes.Clr((IType)ModelNode.Child(ModelNode.Unwrap(list), "Type"));
    }

    private static Expression Converted(Expression sequence, Type item)
    {
        Type current = Linq.ElementType(sequence.Type);

        if (current == item)
        {
            return sequence;
        }

        ParameterExpression value = Expression.Parameter(current, "value");
        return Linq.Select(sequence, Expression.Lambda(Typed.As(value, item), value));
    }
}
