namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Lifted operators: a null operand makes the result null. Operands are
// evaluated left to right and evaluation stops at the first null.
internal static class Lifted
{
    public static object? Unary(object node, Scope scope, Func<object, object> operation)
    {
        object? value = scope.Evaluate(ModelNode.Child(node, "Value"));
        return value is null ? null : operation(value);
    }

    public static object? Binary(
        object node,
        Scope scope,
        Func<object, object, object> operation
    )
    {
        object? left = scope.Evaluate(ModelNode.Child(node, "Left"));

        if (left is null)
        {
            return null;
        }

        object? right = scope.Evaluate(ModelNode.Child(node, "Right"));
        return right is null ? null : operation(left, right);
    }
}
