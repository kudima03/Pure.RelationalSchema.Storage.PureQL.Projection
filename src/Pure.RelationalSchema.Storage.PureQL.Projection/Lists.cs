using System.Collections;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The list operand of in: a list literal, a list parameter or one column of a
// subquery.
internal static class Lists
{
    public static IEnumerable<object?> Values(object list, Scope scope)
    {
        object leaf = ModelNode.Unwrap(list);

        return leaf switch
        {
            IParameter parameter => throw UnboundParameter.Error(parameter.Name),
            ILiteral literal => ((IEnumerable)ModelNode.Child(literal, "Value"))
                .Cast<object>()
                .Select(Literals.Normalize),
            _ => scope.SubqueryColumn(
                (string)ModelNode.Child(leaf, "Subquery"),
                (string)ModelNode.Child(leaf, "Field")
            ),
        };
    }
}
