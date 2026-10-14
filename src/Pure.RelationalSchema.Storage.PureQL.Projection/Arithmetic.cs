using System.Linq.Expressions;
using System.Reflection;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Numeric and string operators. All are lifted: the result is null as soon as
// an operand is null. integer is a checked 64-bit long, decimal is
// System.Decimal; overflow and division by zero raise their .NET exceptions,
// which fail the query. Rounding is half away from zero, as SQL's round is.
internal static class Arithmetic
{
    private const int MaxDigits = 28;

    private static readonly MethodInfo FloorMethod = typeof(Math).GetMethod(
        nameof(Math.Floor),
        [typeof(decimal)]
    )!;

    private static readonly MethodInfo CeilingMethod = typeof(Math).GetMethod(
        nameof(Math.Ceiling),
        [typeof(decimal)]
    )!;

    private static readonly MethodInfo RoundMethod = typeof(Math).GetMethod(
        nameof(Math.Round),
        [typeof(decimal), typeof(MidpointRounding)]
    )!;

    private static readonly MethodInfo RoundToDigitsMethod = typeof(Math).GetMethod(
        nameof(Math.Round),
        [typeof(decimal), typeof(int), typeof(MidpointRounding)]
    )!;

    private static readonly MethodInfo MinMethod = typeof(Math).GetMethod(
        nameof(Math.Min),
        [typeof(long), typeof(long)]
    )!;

    private static readonly MethodInfo PowerMethod = typeof(Math).GetMethod(
        nameof(Math.Pow),
        [typeof(double), typeof(double)]
    )!;

    private static readonly MethodInfo ConcatMethod = typeof(string).GetMethod(
        nameof(string.Concat),
        [typeof(string), typeof(string)]
    )!;

    public static Expression AddIntegers(object node, Scope scope)
    {
        return Fold(node, scope, typeof(long?), Expression.AddChecked);
    }

    public static Expression AddDecimals(object node, Scope scope)
    {
        return Fold(node, scope, typeof(decimal?), Expression.Add);
    }

    public static Expression SubtractIntegers(object node, Scope scope)
    {
        return Fold(node, scope, typeof(long?), Expression.SubtractChecked);
    }

    public static Expression SubtractDecimals(object node, Scope scope)
    {
        return Fold(node, scope, typeof(decimal?), Expression.Subtract);
    }

    public static Expression MultiplyIntegers(object node, Scope scope)
    {
        return Fold(node, scope, typeof(long?), Expression.MultiplyChecked);
    }

    public static Expression MultiplyDecimals(object node, Scope scope)
    {
        return Fold(node, scope, typeof(decimal?), Expression.Multiply);
    }

    public static Expression Divide(object node, Scope scope)
    {
        return Fold(node, scope, typeof(decimal?), Expression.Divide);
    }

    // Truncates toward zero, and modulo takes the sign of its left operand,
    // exactly as C# (and SQL) integer division and remainder do.
    public static Expression IntegerDivide(object node, Scope scope)
    {
        return Expression.Divide(
            Integer(scope.Compile(ModelNode.Child(node, "Left"))),
            Integer(scope.Compile(ModelNode.Child(node, "Right")))
        );
    }

    public static Expression Modulo(object node, Scope scope)
    {
        return Expression.Modulo(
            Integer(scope.Compile(ModelNode.Child(node, "Left"))),
            Integer(scope.Compile(ModelNode.Child(node, "Right")))
        );
    }

    public static Expression Floor(object node, Scope scope)
    {
        return ToInteger(node, scope, value => Expression.Call(FloorMethod, value));
    }

    public static Expression Ceiling(object node, Scope scope)
    {
        return ToInteger(node, scope, value => Expression.Call(CeilingMethod, value));
    }

    public static Expression Round(object node, Scope scope)
    {
        return ToInteger(node, scope, AwayFromZero);
    }

    // A negative digits rounds to tens, hundreds and so on; decimal holds 28
    // digits on either side of the point, so further digits change nothing.
    // Constant digits, the usual case, fold to one of the three branches.
    public static Expression RoundToDigits(object node, Scope scope)
    {
        Expression digits = Typed.As(
            scope.Compile(ModelNode.Child(node, "Digits")),
            typeof(long)
        );

        return Lifted.Unary(
            Decimal(scope.Compile(ModelNode.Child(node, "Operand"))),
            typeof(decimal?),
            value =>
            {
                Expression scale = Expression.Convert(
                    Expression.Call(
                        PowerMethod,
                        Expression.Constant(10d),
                        Expression.Convert(Expression.Negate(digits), typeof(double))
                    ),
                    typeof(decimal)
                );

                return Expression.Condition(
                    Expression.GreaterThanOrEqual(digits, Expression.Constant(0L)),
                    Expression.Call(
                        RoundToDigitsMethod,
                        value,
                        Expression.Convert(
                            Expression.Call(
                                MinMethod,
                                digits,
                                Expression.Constant((long)MaxDigits)
                            ),
                            typeof(int)
                        ),
                        Expression.Constant(MidpointRounding.AwayFromZero)
                    ),
                    Expression.Condition(
                        Expression.LessThan(
                            digits,
                            Expression.Constant((long)-MaxDigits)
                        ),
                        Expression.Constant(0m),
                        Expression.Multiply(
                            AwayFromZero(Expression.Divide(value, scale)),
                            scale
                        )
                    )
                );
            }
        );
    }

    public static Expression Concat(object node, Scope scope)
    {
        return Lifted.All(
            [
                .. ModelNode
                    .Children(node, "Values")
                    .Select(operand => Typed.As(scope.Compile(operand), typeof(string))),
            ],
            typeof(string),
            values => values.Aggregate(
                (left, right) => Expression.Call(ConcatMethod, left, right)
            )
        );
    }

    public static Expression AwayFromZero(Expression value)
    {
        return Expression.Call(
            RoundMethod,
            value,
            Expression.Constant(MidpointRounding.AwayFromZero)
        );
    }

    public static Expression Decimal(Expression value)
    {
        return Typed.As(value, typeof(decimal?));
    }

    private static Expression Integer(Expression value)
    {
        return Typed.As(value, typeof(long?));
    }

    private static Expression ToInteger(
        object node,
        Scope scope,
        Func<Expression, Expression> round
    )
    {
        return Lifted.Unary(
            Decimal(scope.Compile(ModelNode.Child(node, "Operand"))),
            typeof(long?),
            value => Expression.Convert(round(value), typeof(long))
        );
    }

    // The operands combined left to right, each converted to the operator's
    // type; lifted operators make the result null on a null operand.
    private static Expression Fold(
        object node,
        Scope scope,
        Type type,
        Func<Expression, Expression, BinaryExpression> operation
    )
    {
        return ModelNode
            .Children(node, "Values")
            .Select(operand => Typed.As(scope.Compile(operand), type))
            .Aggregate((left, right) => operation(left, right));
    }
}
