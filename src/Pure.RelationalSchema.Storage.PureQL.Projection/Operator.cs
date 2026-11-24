using System.Collections.Concurrent;
using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The specification's operators. PureQL.CSharp.Model names every operator
// record after its operator, the family of its operand where signatures
// differ by it, its declared type and its context (AddIntegerRow,
// YearDatetimeIntegerNullableRow, SumDecimalGroup, EqualStringProjection,
// ...), and the record of an operator names its operands the same way in
// every signature and context. So the longest operator name a record's type
// name starts with selects the operator, which reads the node's declared type
// and its operands' types where signatures differ. Each operator compiles
// its record into the expression that computes it.
internal sealed class Operator
{
    private static readonly IReadOnlyDictionary<
        string,
        Func<object, Scope, Expression>
    > All =
        new Dictionary<string, Func<object, Scope, Expression>>(StringComparer.Ordinal)
        {
            ["Add"] = Arithmetic.Add,
            ["Subtract"] = Arithmetic.Subtract,
            ["Multiply"] = Arithmetic.Multiply,
            ["Divide"] = Arithmetic.Divide,
            ["IntegerDivide"] = Arithmetic.IntegerDivide,
            ["Modulo"] = Arithmetic.Modulo,
            ["Floor"] = Arithmetic.Floor,
            ["Ceiling"] = Arithmetic.Ceiling,
            ["Round"] = Arithmetic.Round,
            ["Truncate"] = Arithmetic.Truncate,
            ["Abs"] = Arithmetic.Abs,
            ["Sign"] = Arithmetic.Sign,
            ["Power"] = Arithmetic.Power,
            ["Sqrt"] = Arithmetic.Sqrt,
            ["Exp"] = Arithmetic.Exp,
            ["Ln"] = Arithmetic.Ln,
            ["Log10"] = Arithmetic.Log10,
            ["Log"] = Arithmetic.Log,
            ["Sin"] = Arithmetic.Sin,
            ["Cos"] = Arithmetic.Cos,
            ["Tan"] = Arithmetic.Tan,
            ["Asin"] = Arithmetic.Asin,
            ["Acos"] = Arithmetic.Acos,
            ["Atan"] = Arithmetic.Atan,
            ["Atan2"] = Arithmetic.Atan2,
            ["Pi"] = Arithmetic.Pi,
            ["Concat"] = Text.Concat,
            ["ConcatWith"] = Text.ConcatWith,
            ["Length"] = Text.Length,
            ["Upper"] = Text.Upper,
            ["Lower"] = Text.Lower,
            ["FoldCase"] = Text.FoldCase,
            ["Normalize"] = Text.Normalize,
            ["RemoveDiacritics"] = Text.RemoveDiacritics,
            ["Trim"] = Text.Trim,
            ["TrimStart"] = Text.TrimStart,
            ["TrimEnd"] = Text.TrimEnd,
            ["Substring"] = Text.Substring,
            ["Replace"] = Text.Replace,
            ["Position"] = Text.Position,
            ["SplitPart"] = Text.SplitPart,
            ["PadStart"] = Text.PadStart,
            ["PadEnd"] = Text.PadEnd,
            ["Repeat"] = Text.Repeat,
            ["Reverse"] = Text.Reverse,
            ["Contains"] = Text.Contains,
            ["StartsWith"] = Text.StartsWith,
            ["EndsWith"] = Text.EndsWith,
            ["MatchesRegex"] = Text.MatchesRegex,
            ["ExtractRegex"] = Text.ExtractRegex,
            ["ReplaceRegex"] = Text.ReplaceRegex,
            ["DateAddDays"] = Temporal.DateAddDays,
            ["DateAddMonths"] = Temporal.DateAddMonths,
            ["DateDiffDays"] = Temporal.DateDiffDays,
            ["DateDiffMonths"] = Temporal.DateDiffMonths,
            ["TimeAddSeconds"] = Temporal.TimeAddSeconds,
            ["TimeDiffSeconds"] = Temporal.TimeDiffSeconds,
            ["DatetimeAddSeconds"] = Temporal.DatetimeAddSeconds,
            ["DatetimeDiffSeconds"] = Temporal.DatetimeDiffSeconds,
            ["DatetimeAddMonths"] = Temporal.DatetimeAddMonths,
            ["DatetimeDiffMonths"] = Temporal.DatetimeDiffMonths,
            ["DateFromParts"] = Temporal.DateFromParts,
            ["TimeFromParts"] = Temporal.TimeFromParts,
            ["DatetimeFromParts"] = Temporal.DatetimeFromParts,
            ["Year"] = Temporal.Year,
            ["Quarter"] = Temporal.Quarter,
            ["Month"] = Temporal.Month,
            ["Day"] = Temporal.Day,
            ["DayOfWeek"] = Temporal.DayOfWeek,
            ["DayOfYear"] = Temporal.DayOfYear,
            ["Week"] = Temporal.Week,
            ["WeekYear"] = Temporal.WeekYear,
            ["Hour"] = Temporal.Hour,
            ["Minute"] = Temporal.Minute,
            ["Second"] = Temporal.Second,
            ["Microsecond"] = Temporal.Microsecond,
            ["StartOfYear"] = Temporal.StartOfYear,
            ["StartOfQuarter"] = Temporal.StartOfQuarter,
            ["StartOfMonth"] = Temporal.StartOfMonth,
            ["StartOfWeek"] = Temporal.StartOfWeek,
            ["StartOfDay"] = Temporal.StartOfDay,
            ["StartOfHour"] = Temporal.StartOfHour,
            ["StartOfMinute"] = Temporal.StartOfMinute,
            ["StartOfSecond"] = Temporal.StartOfSecond,
            ["AsString"] = Conversions.AsString,
            ["AsDate"] = Conversions.AsDate,
            ["AsTime"] = Conversions.AsTime,
            ["AsDatetime"] = Conversions.AsDatetime,
            ["AsInteger"] = Conversions.AsInteger,
            ["AsDecimal"] = Conversions.AsDecimal,
            ["AsBoolean"] = Conversions.AsBoolean,
            ["AsUuid"] = Conversions.AsUuid,
            ["AsNullable"] = Conversions.AsNullable,
            ["TryAsInteger"] = Conversions.TryAsInteger,
            ["TryAsDecimal"] = Conversions.TryAsDecimal,
            ["TryAsBoolean"] = Conversions.TryAsBoolean,
            ["TryAsDate"] = Conversions.TryAsDate,
            ["TryAsTime"] = Conversions.TryAsTime,
            ["TryAsDatetime"] = Conversions.TryAsDatetime,
            ["TryAsUuid"] = Conversions.TryAsUuid,
            ["And"] = Logic.And,
            ["Or"] = Logic.Or,
            ["Not"] = Logic.Not,
            ["If"] = Logic.If,
            ["Coalesce"] = Logic.Coalesce,
            ["Greatest"] = Logic.Greatest,
            ["Least"] = Logic.Least,
            ["NullIfEqual"] = Logic.NullIfEqual,
            ["Equal"] = Comparison.Equal,
            ["NotEqual"] = Comparison.NotEqual,
            ["GreaterThan"] = Comparison.GreaterThan,
            ["GreaterThanOrEqual"] = Comparison.GreaterThanOrEqual,
            ["LessThan"] = Comparison.LessThan,
            ["LessThanOrEqual"] = Comparison.LessThanOrEqual,
            ["In"] = Comparison.In,
            ["Count"] = Aggregates.Count,
            ["CountDistinct"] = Aggregates.CountDistinct,
            ["Sum"] = Aggregates.Sum,
            ["Average"] = Aggregates.Average,
            ["Min"] = Aggregates.Min,
            ["Max"] = Aggregates.Max,
            ["Any"] = Aggregates.Any,
            ["All"] = Aggregates.All,
            ["First"] = Aggregates.First,
            ["Last"] = Aggregates.Last,
            ["ConcatAll"] = Aggregates.ConcatAll,
            ["Median"] = Aggregates.Median,
            ["PercentileContinuous"] = Aggregates.PercentileContinuous,
            ["PercentileDiscrete"] = Aggregates.PercentileDiscrete,
            ["Mode"] = Aggregates.Mode,
            ["VariancePopulation"] = Aggregates.VariancePopulation,
            ["VarianceSample"] = Aggregates.VarianceSample,
            ["StandardDeviationPopulation"] = Aggregates.StandardDeviationPopulation,
            ["StandardDeviationSample"] = Aggregates.StandardDeviationSample,
            ["CovariancePopulation"] = Aggregates.CovariancePopulation,
            ["CovarianceSample"] = Aggregates.CovarianceSample,
            ["Correlation"] = Aggregates.Correlation,
            ["RegressionSlope"] = Aggregates.RegressionSlope,
            ["RegressionIntercept"] = Aggregates.RegressionIntercept,
            ["RegressionRSquared"] = Aggregates.RegressionRSquared,
            ["RowNumber"] = Windows.RowNumber,
            ["Rank"] = Windows.Rank,
            ["DenseRank"] = Windows.DenseRank,
            ["PercentRank"] = Windows.PercentRank,
            ["CumulativeDistribution"] = Windows.CumulativeDistribution,
            ["Ntile"] = Windows.Ntile,
            ["Previous"] = Windows.Previous,
            ["Next"] = Windows.Next,
        };

    private static readonly ConcurrentDictionary<Type, string?> Names = new();

    private readonly Func<object, Scope, Expression> _compile;

    private Operator(Func<object, Scope, Expression> compile)
    {
        _compile = compile;
    }

    public static Operator Of(Type type)
    {
        return Name(type) is string name
            ? new Operator(All[name])
            : throw new NotSupportedException(
                $"{type.Name} is not a PureQL operator this interpreter knows."
            );
    }

    // The name of the operator a record is of, as the model spells it: Add,
    // NotEqual, DateAddDays; null for a type that is not an operator's.
    public static string? Name(Type type)
    {
        return Names.GetOrAdd(type, Resolve);
    }

    public Expression Compile(object node, Scope scope)
    {
        return _compile(node, scope);
    }

    private static string? Resolve(Type type)
    {
        return typeof(global::PureQL.CSharp.Model.IOperator).IsAssignableFrom(type)
            ? All
                .Keys.Where(name => type.Name.StartsWith(name, StringComparison.Ordinal))
                .MaxBy(name => name.Length)
            : null;
    }
}
