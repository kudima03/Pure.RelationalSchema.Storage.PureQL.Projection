using System.Linq.Expressions;
using System.Reflection;
using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Numeric operators. All are lifted: the result is null as soon as an operand
// is null. integer is a checked 64-bit long and decimal System.Decimal, whose
// operations a run in memory computes exactly (see LocalProvider); overflow,
// division by zero and a value outside a function's domain raise their .NET
// exceptions, which fail the query. An operator over a list goes from left
// to right, each step in the type its two operands give. Rounding is half
// away from zero unless round names half to even.
internal static class Arithmetic
{
    private const int MaxDigits = 28;

    private static readonly MethodInfo FloorMethod = Functions.Method(
        typeof(Math),
        nameof(Math.Floor),
        typeof(decimal)
    );

    private static readonly MethodInfo CeilingMethod = Functions.Method(
        typeof(Math),
        nameof(Math.Ceiling),
        typeof(decimal)
    );

    private static readonly MethodInfo TruncateMethod = Functions.Method(
        typeof(Math),
        nameof(Math.Truncate),
        typeof(decimal)
    );

    private static readonly MethodInfo RoundMethod = Functions.Method(
        typeof(Math),
        nameof(Math.Round),
        typeof(decimal),
        typeof(MidpointRounding)
    );

    private static readonly MethodInfo RoundToDigitsMethod = Functions.Method(
        typeof(Math),
        nameof(Math.Round),
        typeof(decimal),
        typeof(int),
        typeof(MidpointRounding)
    );

    private static readonly MethodInfo MinMethod = Functions.Method(
        typeof(Math),
        nameof(Math.Min),
        typeof(long),
        typeof(long)
    );

    private static readonly MethodInfo PowerMethod = Functions.Method(
        typeof(Math),
        nameof(Math.Pow),
        typeof(double),
        typeof(double)
    );

    public static Expression Add(object node, Scope scope)
    {
        return Fold(node, scope, Expression.AddChecked, Expression.Add);
    }

    public static Expression Subtract(object node, Scope scope)
    {
        return Fold(node, scope, Expression.SubtractChecked, Expression.Subtract);
    }

    public static Expression Multiply(object node, Scope scope)
    {
        return Fold(node, scope, Expression.MultiplyChecked, Expression.Multiply);
    }

    public static Expression Divide(object node, Scope scope)
    {
        return ModelNode
            .Children(node, "Values")
            .Select(operand => Decimal(scope.Compile(operand)))
            .Aggregate(Expression.Divide);
    }

    // The exact quotient truncated toward zero, an integer whatever the
    // operands; of two integers it is C#'s integer division.
    public static Expression IntegerDivide(object node, Scope scope)
    {
        Expression left = scope.Compile(ModelNode.Child(node, "Left"));
        Expression right = scope.Compile(ModelNode.Child(node, "Right"));

        return IsInteger(left) && IsInteger(right)
            ? Expression.Divide(Integer(left), Integer(right))
            : Lifted.Binary(
                Decimal(left),
                Decimal(right),
                typeof(long?),
                (l, r) => Functions.Call(
                    typeof(PureQLMath),
                    nameof(PureQLMath.IntegerDivide),
                    l,
                    r
                )
            );
    }

    // left - right x integerDivide(left, right), with the sign of left; of
    // two integers it is C#'s remainder.
    public static Expression Modulo(object node, Scope scope)
    {
        Expression left = scope.Compile(ModelNode.Child(node, "Left"));
        Expression right = scope.Compile(ModelNode.Child(node, "Right"));

        return IsInteger(left) && IsInteger(right)
            ? Expression.Modulo(Integer(left), Integer(right))
            : Lifted.Binary(
                Decimal(left),
                Decimal(right),
                typeof(decimal?),
                (l, r) => Functions.Call(
                    typeof(PureQLMath),
                    nameof(PureQLMath.Modulo),
                    l,
                    r
                )
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

    // Without digits a decimal is rounded to an integer; with digits the type
    // of the operand is kept.
    public static Expression Round(object node, Scope scope)
    {
        MidpointRounding mode =
            (RoundingMode)ModelNode.Child(node, "Mode") == RoundingMode.HalfEven
                ? MidpointRounding.ToEven
                : MidpointRounding.AwayFromZero;

        return ModelNode.Optional(node, "Digits") is not object digits
                ? ToInteger(node, scope, value => Rounded(value, mode))
            : IsInteger(node)
                ? WithDigits(
                    node,
                    scope,
                    digits,
                    typeof(long?),
                    (value, places) => Functions.Call(
                        typeof(PureQLMath),
                        nameof(PureQLMath.Round),
                        value,
                        places,
                        Expression.Constant(mode)
                    )
                )
            : WithDigits(
                node,
                scope,
                digits,
                typeof(decimal?),
                (value, places) => RoundedToDigits(value, places, mode)
            );
    }

    // Rounds toward zero, typed exactly as round.
    public static Expression Truncate(object node, Scope scope)
    {
        return ModelNode.Optional(node, "Digits") is not object digits
            ? ToInteger(node, scope, value => Expression.Call(TruncateMethod, value))
            : WithDigits(
                node,
                scope,
                digits,
                ValueTypes.Clr(((IOperator)node).Type),
                (value, places) => Functions.Call(
                    typeof(PureQLMath),
                    nameof(PureQLMath.Truncate),
                    value,
                    places
                )
            );
    }

    // The absolute value, in the type of the operand.
    public static Expression Abs(object node, Scope scope)
    {
        Type type = ValueTypes.Clr(((IOperator)node).Type);

        return Lifted.Unary(
            Typed.As(scope.Compile(ModelNode.Child(node, "Operand")), type),
            type,
            value => Functions.Call(typeof(Math), nameof(Math.Abs), value)
        );
    }

    public static Expression Sign(object node, Scope scope)
    {
        return Lifted.Unary(
            Decimal(scope.Compile(ModelNode.Child(node, "Operand"))),
            typeof(long?),
            value => Expression.Convert(
                Functions.Call(typeof(Math), nameof(Math.Sign), value),
                typeof(long)
            )
        );
    }

    public static Expression Power(object node, Scope scope)
    {
        return Function(node, scope, nameof(PureQLMath.Power), "Operand", "Exponent");
    }

    public static Expression Sqrt(object node, Scope scope)
    {
        return Function(node, scope, nameof(PureQLMath.Sqrt), "Operand");
    }

    public static Expression Exp(object node, Scope scope)
    {
        return Function(node, scope, nameof(PureQLMath.Exp), "Operand");
    }

    public static Expression Ln(object node, Scope scope)
    {
        return Function(node, scope, nameof(PureQLMath.Ln), "Operand");
    }

    public static Expression Log10(object node, Scope scope)
    {
        return Function(node, scope, nameof(PureQLMath.Log10), "Operand");
    }

    public static Expression Log(object node, Scope scope)
    {
        return Function(node, scope, nameof(PureQLMath.Log), "Operand", "Base");
    }

    public static Expression Sin(object node, Scope scope)
    {
        return Function(node, scope, nameof(PureQLMath.Sin), "Operand");
    }

    public static Expression Cos(object node, Scope scope)
    {
        return Function(node, scope, nameof(PureQLMath.Cos), "Operand");
    }

    public static Expression Tan(object node, Scope scope)
    {
        return Function(node, scope, nameof(PureQLMath.Tan), "Operand");
    }

    public static Expression Asin(object node, Scope scope)
    {
        return Function(node, scope, nameof(PureQLMath.Asin), "Operand");
    }

    public static Expression Acos(object node, Scope scope)
    {
        return Function(node, scope, nameof(PureQLMath.Acos), "Operand");
    }

    public static Expression Atan(object node, Scope scope)
    {
        return Function(node, scope, nameof(PureQLMath.Atan), "Operand");
    }

    public static Expression Atan2(object node, Scope scope)
    {
        return Function(node, scope, nameof(PureQLMath.Atan2), "Y", "X");
    }

    // pi rounded to the decimal precision.
    public static Expression Pi(object _, Scope _1)
    {
        return Expression.Constant(PureQLMath.Pi, typeof(decimal?));
    }

    public static Expression AwayFromZero(Expression value)
    {
        return Rounded(value, MidpointRounding.AwayFromZero);
    }

    public static Expression Decimal(Expression value)
    {
        return Typed.As(value, typeof(decimal?));
    }

    public static Expression Integer(Expression value)
    {
        return Typed.As(value, typeof(long?));
    }

    public static bool IsInteger(Expression value)
    {
        return Typed.OrNull(value.Type) == typeof(long?);
    }

    private static bool IsInteger(object node)
    {
        return ((IOperator)node).Type.Name == "integer";
    }

    private static Expression Rounded(Expression value, MidpointRounding mode)
    {
        return Expression.Call(RoundMethod, value, Expression.Constant(mode));
    }

    // A decimal rounded to a number of decimal places. A negative digits
    // rounds to tens, hundreds and so on; a decimal holds 28 digits on either
    // side of the point, so further digits change nothing. Digits written
    // as a literal, the usual case, give one of the three branches, with the
    // power of ten a constant; computed digits give all three, and the power
    // of ten as Math.Pow names it, which is computed without a double when
    // the tree runs in memory (ExactArithmetic).
    private static Expression RoundedToDigits(
        Expression value,
        Expression digits,
        MidpointRounding mode
    )
    {
        if (
            digits is MemberExpression { Expression: ConstantExpression { Value: long places } }
        )
        {
            Expression power = Expression.Constant(
                Exact.Decimal(
                    Exact.PowerOfTen((int)Math.Clamp(-places, 0, MaxDigits)),
                    0,
                    false
                )
            );

            return places >= 0
                    ? Expression.Call(
                        RoundToDigitsMethod,
                        value,
                        Expression.Constant((int)Math.Min(places, MaxDigits)),
                        Expression.Constant(mode)
                    )
                : places < -MaxDigits ? Expression.Constant(0m)
                : Expression.Multiply(
                    Rounded(Expression.Divide(value, power), mode),
                    power
                );
        }

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
                    Expression.Call(MinMethod, digits, Expression.Constant((long)MaxDigits)),
                    typeof(int)
                ),
                Expression.Constant(mode)
            ),
            Expression.Condition(
                Expression.LessThan(digits, Expression.Constant((long)-MaxDigits)),
                Expression.Constant(0m),
                Expression.Multiply(Rounded(Expression.Divide(value, scale), mode), scale)
            )
        );
    }

    // A function of PureQLMath over decimal operands, lifted over each.
    private static Expression Function(
        object node,
        Scope scope,
        string name,
        params string[] operands
    )
    {
        return Lifted.All(
            [
                .. operands.Select(operand =>
                    Decimal(scope.Compile(ModelNode.Child(node, operand)))
                ),
            ],
            typeof(decimal?),
            values => Functions.Call(typeof(PureQLMath), name, [.. values])
        );
    }

    private static Expression WithDigits(
        object node,
        Scope scope,
        object digits,
        Type type,
        Func<Expression, Expression, Expression> operation
    )
    {
        return Lifted.Binary(
            Typed.As(scope.Compile(ModelNode.Child(node, "Operand")), type),
            Integer(scope.Compile(digits)),
            type,
            operation
        );
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

    // The operands combined left to right, one step at a time, each step in
    // the type its two operands give: an integer step is checked, and a
    // decimal operand widens only the steps from the one it takes part in.
    private static Expression Fold(
        object node,
        Scope scope,
        Func<Expression, Expression, BinaryExpression> integers,
        Func<Expression, Expression, BinaryExpression> decimals
    )
    {
        return ModelNode
            .Children(node, "Values")
            .Select(scope.Compile)
            .Aggregate(
                (left, right) => IsInteger(left) && IsInteger(right)
                    ? integers(Integer(left), Integer(right))
                    : decimals(Decimal(left), Decimal(right))
            );
    }
}
