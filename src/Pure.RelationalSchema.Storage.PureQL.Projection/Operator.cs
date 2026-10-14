using System.Collections.Concurrent;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The specification's operators. PureQL.CSharp.Model names every operator
// record after its operator, result type and context (AddIntegerRow,
// SumDecimalGroup, GreaterThanStringProjection, ...), and the record of an
// operator has the same operands in every context. So the record type's
// longest matching name prefix selects the operator; where integer and
// decimal results differ (add, subtract, multiply, sum, round) the prefix
// carries the result type too.
internal sealed class Operator
{
    private static readonly IReadOnlyDictionary<
        string,
        Func<object, Scope, object?>
    > All =
        new Dictionary<string, Func<object, Scope, object?>>(StringComparer.Ordinal)
        {
            ["AddInteger"] = Arithmetic.AddIntegers,
            ["AddDecimal"] = Arithmetic.AddDecimals,
            ["SubtractInteger"] = Arithmetic.SubtractIntegers,
            ["SubtractDecimal"] = Arithmetic.SubtractDecimals,
            ["MultiplyInteger"] = Arithmetic.MultiplyIntegers,
            ["MultiplyDecimal"] = Arithmetic.MultiplyDecimals,
            ["Divide"] = Arithmetic.Divide,
            ["IntegerDivide"] = Arithmetic.IntegerDivide,
            ["Modulo"] = Arithmetic.Modulo,
            ["Floor"] = Arithmetic.Floor,
            ["Ceiling"] = Arithmetic.Ceiling,
            ["RoundInteger"] = Arithmetic.Round,
            ["RoundDecimal"] = Arithmetic.RoundToDigits,
            ["Concat"] = Arithmetic.Concat,
            ["DateAddDays"] = Temporal.DateAddDays,
            ["DateDiffDays"] = Temporal.DateDiffDays,
            ["TimeAddSeconds"] = Temporal.TimeAddSeconds,
            ["TimeDiffSeconds"] = Temporal.TimeDiffSeconds,
            ["DatetimeAddSeconds"] = Temporal.DatetimeAddSeconds,
            ["DatetimeDiffSeconds"] = Temporal.DatetimeDiffSeconds,
            ["And"] = Logic.And,
            ["Or"] = Logic.Or,
            ["Not"] = Logic.Not,
            ["Equal"] = Comparison.Equal,
            ["NotEqual"] = Comparison.NotEqual,
            ["GreaterThan"] = Comparison.GreaterThan,
            ["GreaterThanOrEqual"] = Comparison.GreaterThanOrEqual,
            ["LessThan"] = Comparison.LessThan,
            ["LessThanOrEqual"] = Comparison.LessThanOrEqual,
            ["In"] = Comparison.In,
            ["If"] = Logic.If,
            ["Coalesce"] = Logic.Coalesce,
            ["Count"] = Aggregates.Count,
            ["SumInteger"] = Aggregates.SumIntegers,
            ["SumDecimal"] = Aggregates.SumDecimals,
            ["Average"] = Aggregates.Average,
            ["Min"] = Aggregates.Min,
            ["Max"] = Aggregates.Max,
            ["Any"] = Aggregates.Any,
            ["All"] = Aggregates.All,
        };

    private static readonly ConcurrentDictionary<Type, Operator?> Resolved = new();

    private readonly Func<object, Scope, object?> _evaluate;

    private Operator(Func<object, Scope, object?> evaluate)
    {
        _evaluate = evaluate;
    }

    public static Operator Of(Type type)
    {
        return Resolved.GetOrAdd(type, Resolve)
            ?? throw new NotSupportedException(
                $"{type.Name} is not a PureQL operator this interpreter knows."
            );
    }

    public object? Evaluate(object node, Scope scope)
    {
        return _evaluate(node, scope);
    }

    private static Operator? Resolve(Type type)
    {
        string? name = All
            .Keys.Where(prefix => type.Name.StartsWith(prefix, StringComparison.Ordinal))
            .MaxBy(prefix => prefix.Length);

        return name is null ? null : new Operator(All[name]);
    }
}
