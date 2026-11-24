using System.Linq.Expressions;
using System.Reflection;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Calls of static methods by the types of their arguments: the BCL's, and
// the functions of this library (PureQLMath, PureQLText, PureQLTime,
// PureQLConversions, PureQLAggregates) an operator is when the BCL has no
// member that computes exactly what the specification defines. Such a call
// is what a provider meets in the tree, and translates or refuses by its
// method.
internal static class Functions
{
    public static MethodCallExpression Call(
        Type owner,
        string name,
        params Expression[] arguments
    )
    {
        return Expression.Call(
            Method(owner, name, [.. arguments.Select(argument => argument.Type)]),
            arguments
        );
    }

    public static MethodCallExpression Call(
        Expression instance,
        string name,
        params Expression[] arguments
    )
    {
        return Expression.Call(
            instance,
            Method(instance.Type, name, [.. arguments.Select(argument => argument.Type)]),
            arguments
        );
    }

    public static MethodInfo Method(Type owner, string name, params Type[] parameters)
    {
        return owner.GetMethod(name, parameters)
            ?? throw new InvalidOperationException(
                $"{owner.Name} has no method {name}("
                    + $"{string.Join(", ", parameters.Select(type => type.Name))})."
            );
    }
}
