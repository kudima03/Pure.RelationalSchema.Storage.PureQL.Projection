using PureQL.CSharp.Model.Arithmetics;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Evaluates a single-value NumberReturning to a constant double, but only
// when every operand, recursively, is a pure literal (NumberScalar, or
// Arithmetic composed entirely of such). Parameters, aggregates, and Count
// have no meaning outside a row/group context, so any of those appearing
// anywhere in the tree makes the whole expression non-literal. Shared by
// ScalarCell (SELECT) and WhereExpressionBuilder (WHERE) so both scalar,
// no-row-data paths fold Add/Subtract/Multiply/Divide the same way as the
// per-row EachArithmetic family, left-to-right over the Arguments list.
internal static class LiteralArithmeticEvaluator
{
    internal static bool TryEvaluate(NumberReturning returning, out double value)
    {
        double? evaluated = returning.Match(
            _ => null,
            scalar => scalar.Value,
            EvaluateArithmetic,
            _ => null,
            _ => null
        );

        value = evaluated ?? default;
        return evaluated.HasValue;
    }

    private static double? EvaluateArithmetic(Arithmetic arithmetic)
    {
        IEnumerable<NumberReturning> arguments = arithmetic.Match(
            add => add.Arguments,
            divide => divide.Arguments,
            multiply => multiply.Arguments,
            subtract => subtract.Arguments
        );

        Func<double, double, double> reduce = arithmetic.Match<
            Func<double, double, double>
        >(
            add => Add,
            divide => Divide,
            multiply => Multiply,
            subtract => Subtract
        );

        return Fold(arguments, reduce);
    }

    private static double? Fold(
        IEnumerable<NumberReturning> arguments,
        Func<double, double, double> reduce
    )
    {
        double? accumulator = null;

        foreach (NumberReturning argument in arguments)
        {
            if (!TryEvaluate(argument, out double operand))
            {
                return null;
            }

            accumulator = accumulator.HasValue
                ? reduce(accumulator.Value, operand)
                : operand;
        }

        return accumulator;
    }

    private static double Add(double a, double b)
    {
        return a + b;
    }

    private static double Multiply(double a, double b)
    {
        return a * b;
    }

    private static double Subtract(double a, double b)
    {
        return a - b;
    }

    private static double Divide(double a, double b)
    {
        return b == 0
            ? throw new DivideByZeroException(
                "Literal arithmetic division by zero: division by zero is a "
                    + "defined failure, matching SQL division-by-zero semantics."
            )
            : a / b;
    }
}
