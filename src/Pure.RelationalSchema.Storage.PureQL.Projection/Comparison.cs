namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Comparisons accept nullable operands and always return a boolean: null
// equals null, an ordering comparison against null is false, and a null value
// is in a list exactly when the list holds a null.
internal static class Comparison
{
    public static object? Equal(object node, Scope scope)
    {
        return Values.Equal(Left(node, scope), Right(node, scope));
    }

    public static object? NotEqual(object node, Scope scope)
    {
        return !Values.Equal(Left(node, scope), Right(node, scope));
    }

    public static object? GreaterThan(object node, Scope scope)
    {
        return Ordered(node, scope, order => order > 0);
    }

    public static object? GreaterThanOrEqual(object node, Scope scope)
    {
        return Ordered(node, scope, order => order >= 0);
    }

    public static object? LessThan(object node, Scope scope)
    {
        return Ordered(node, scope, order => order < 0);
    }

    public static object? LessThanOrEqual(object node, Scope scope)
    {
        return Ordered(node, scope, order => order <= 0);
    }

    public static object? In(object node, Scope scope)
    {
        object? value = scope.Evaluate(ModelNode.Child(node, "Value"));

        return Lists
            .Values(ModelNode.Child(node, "List"), scope)
            .Any(item => Values.Equal(item, value));
    }

    private static bool Ordered(object node, Scope scope, Func<int, bool> accept)
    {
        object? left = Left(node, scope);
        object? right = Right(node, scope);

        return left is not null
            && right is not null
            && accept(Values.Compare(left, right));
    }

    private static object? Left(object node, Scope scope)
    {
        return scope.Evaluate(ModelNode.Child(node, "Left"));
    }

    private static object? Right(object node, Scope scope)
    {
        return scope.Evaluate(ModelNode.Child(node, "Right"));
    }
}
