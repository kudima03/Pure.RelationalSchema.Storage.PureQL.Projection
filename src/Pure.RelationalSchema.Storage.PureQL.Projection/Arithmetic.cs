namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Numeric and string operators. All are lifted: the result is null as soon as
// an operand is null, and later operands are then not evaluated. integer is a
// checked 64-bit long, decimal is System.Decimal; overflow and division by
// zero raise their .NET exceptions, which fail the query.
internal static class Arithmetic
{
    public static object? AddIntegers(object node, Scope scope)
    {
        return Integers(node, scope, (left, right) => checked(left + right));
    }

    public static object? AddDecimals(object node, Scope scope)
    {
        return Decimals(node, scope, (left, right) => left + right);
    }

    public static object? SubtractIntegers(object node, Scope scope)
    {
        return Integers(node, scope, (left, right) => checked(left - right));
    }

    public static object? SubtractDecimals(object node, Scope scope)
    {
        return Decimals(node, scope, (left, right) => left - right);
    }

    public static object? MultiplyIntegers(object node, Scope scope)
    {
        return Integers(node, scope, (left, right) => checked(left * right));
    }

    public static object? MultiplyDecimals(object node, Scope scope)
    {
        return Decimals(node, scope, (left, right) => left * right);
    }

    public static object? Divide(object node, Scope scope)
    {
        return Decimals(node, scope, (left, right) => left / right);
    }

    // Truncates toward zero, and modulo takes the sign of its left operand,
    // exactly as C# integer division and remainder do.
    public static object? IntegerDivide(object node, Scope scope)
    {
        return Lifted.Binary(node, scope, (left, right) => (long)left / (long)right);
    }

    public static object? Modulo(object node, Scope scope)
    {
        return Lifted.Binary(node, scope, (left, right) => (long)left % (long)right);
    }

    public static object? Floor(object node, Scope scope)
    {
        return Lifted.Unary(
            node,
            scope,
            value => (long)decimal.Floor(Values.Decimal(value))
        );
    }

    public static object? Ceiling(object node, Scope scope)
    {
        return Lifted.Unary(
            node,
            scope,
            value => (long)decimal.Ceiling(Values.Decimal(value))
        );
    }

    public static object? Round(object node, Scope scope)
    {
        return Lifted.Unary(
            node,
            scope,
            value => (long)decimal.Round(
                Values.Decimal(value),
                MidpointRounding.AwayFromZero
            )
        );
    }

    // A negative digits rounds to tens, hundreds and so on; decimal holds 28
    // digits on either side of the point, so further digits change nothing.
    public static object? RoundToDigits(object node, Scope scope)
    {
        object? value = scope.Evaluate(ModelNode.Child(node, "Value"));

        if (value is null)
        {
            return null;
        }

        decimal number = Values.Decimal(value);
        long digits = (long)scope.Evaluate(ModelNode.Child(node, "Digits"))!;

        if (digits >= 0)
        {
            return decimal.Round(
                number,
                (int)Math.Min(digits, 28),
                MidpointRounding.AwayFromZero
            );
        }

        if (digits < -28)
        {
            return 0m;
        }

        decimal scale = Power(-digits);
        return decimal.Round(number / scale, MidpointRounding.AwayFromZero) * scale;
    }

    public static object? Concat(object node, Scope scope)
    {
        return Fold(node, scope, value => (string)value, string.Concat);
    }

    private static object? Integers(
        object node,
        Scope scope,
        Func<long, long, long> operation
    )
    {
        return Fold(node, scope, value => (long)value, operation);
    }

    private static object? Decimals(
        object node,
        Scope scope,
        Func<decimal, decimal, decimal> operation
    )
    {
        return Fold(node, scope, Values.Decimal, operation);
    }

    private static object? Fold<T>(
        object node,
        Scope scope,
        Func<object, T> convert,
        Func<T, T, T> operation
    )
        where T : notnull
    {
        T? result = default;
        bool first = true;

        foreach (object operand in ModelNode.Children(node, "Values"))
        {
            object? value = scope.Evaluate(operand);

            if (value is null)
            {
                return null;
            }

            result = first ? convert(value) : operation(result!, convert(value));
            first = false;
        }

        return result;
    }

    private static decimal Power(long exponent)
    {
        decimal result = 1m;

        for (long i = 0; i < exponent; i++)
        {
            result *= 10m;
        }

        return result;
    }
}
