using System.Linq.Expressions;
using PureQL.CSharp.Model.Parameters;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// PureQLProjection has no way to bind parameter values yet. A parameter is a
// captured variable in the tree, the Value of a QueryParameter constant, as
// C# closures are; reading it fails the query instead of guessing a value.
internal static class UnboundParameter
{
    public static NotSupportedException Error(string name)
    {
        return new NotSupportedException(
            $"Parameter '{name}' cannot be bound: parameter binding is not supported."
        );
    }

    // A scalar parameter has its type's value type, a list parameter is a
    // sequence of its item type.
    public static Expression Value(IParameter parameter)
    {
        Type item = ValueTypes.Clr(parameter.Type);
        Type type = parameter.Type.Name.EndsWith("List", StringComparison.Ordinal)
            ? typeof(IEnumerable<>).MakeGenericType(item)
            : item;

        return Expression.Property(
            Expression.Constant(
                Activator.CreateInstance(
                    typeof(QueryParameter<>).MakeGenericType(type),
                    parameter.Name
                )
            ),
            nameof(QueryParameter<>.Value)
        );
    }
}
