namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Boolean connectives and the conditional operators. All evaluate lazily:
// and stops at the first false, or at the first true, if evaluates only the
// branch it takes and coalesce stops at its first non-null operand.
internal static class Logic
{
    public static object? And(object node, Scope scope)
    {
        return ModelNode.Children(node, "Conditions").All(scope.Test);
    }

    public static object? Or(object node, Scope scope)
    {
        return ModelNode.Children(node, "Conditions").Any(scope.Test);
    }

    public static object? Not(object node, Scope scope)
    {
        return !scope.Test(ModelNode.Child(node, "Condition"));
    }

    public static object? If(object node, Scope scope)
    {
        return scope.Evaluate(
            ModelNode.Child(
                node,
                scope.Test(ModelNode.Child(node, "Condition")) ? "Then" : "Else"
            )
        );
    }

    public static object? Coalesce(object node, Scope scope)
    {
        return ModelNode
            .Children(node, "Values")
            .Select(scope.Evaluate)
            .FirstOrDefault(value => value is not null);
    }
}
