using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model.Returnings;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Builds the constant output cell of a scalar select expression
// (SELECT 5 AS version): standard SQL repeats the constant on every output
// row. The cell text is the canonical invariant form that CellValueExtractor
// round-trips, so scalar cells behave exactly like stored ones.
internal static class ScalarCell
{
    internal static bool IsScalar(SingleValueReturning returning)
    {
        return returning.Match(
            boolean => boolean.IsT1,
            date => date.IsT1,
            dateTime => dateTime.IsT1,
            number => number.IsT1
                || LiteralArithmeticEvaluator.TryEvaluate(number, out double _),
            text => text.IsT1,
            time => time.IsT1,
            uuid => uuid.IsT1
        );
    }

    internal static ICell From(SingleValueReturning returning)
    {
        return new Cell(new String(Text(returning)));
    }

    private static string Text(SingleValueReturning returning)
    {
        return returning.Match(
            boolean => ValueText.From(boolean.AsT1.Value),
            date => ValueText.From(date.AsT1.Value),
            dateTime => ValueText.From(dateTime.AsT1.Value),
            number => ValueText.From(NumberValue(number)),
            text => text.AsT1.Value,
            time => ValueText.From(time.AsT1.Value),
            uuid => ValueText.From(uuid.AsT1.Value)
        );
    }

    private static double NumberValue(NumberReturning number)
    {
        return LiteralArithmeticEvaluator.TryEvaluate(number, out double value)
            ? value
            : number.AsT1.Value;
    }
}
