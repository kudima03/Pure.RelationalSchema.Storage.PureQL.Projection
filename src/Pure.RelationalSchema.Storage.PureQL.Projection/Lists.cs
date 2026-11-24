using System.Linq.Expressions;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;

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
            ISubqueryColumn column => scope.SubqueryColumn(
                column.Subquery,
                column.Field,
                item
            ),
            _ => throw new NotSupportedException(
                $"{leaf.GetType().Name} is not a list this interpreter knows."
            ),
        };
    }

    // The type of the items of a list, by its declared type.
    public static Type Item(object list)
    {
        return ValueTypes.Clr(ModelNode.TypeOf(list));
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
