using System.Numerics;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The aggregates the specification computes from exact values: sums, means,
// percentiles, the mode and the statistics of one and of two columns. Each
// takes the values an aggregate sees, after its predicate, and skips the
// null ones itself. A result is the same in any order of the values: it is
// computed from exact sums, rounded once as divide and sqrt round, and only
// the result is checked against the range of its type. With no values, or
// only null ones, sum is 0 and every other result is null.
public static class PureQLAggregates
{
    private static readonly BigInteger Unit = Exact.PowerOfTen(Exact.Digits);

    private static readonly BigInteger SquareUnit = Exact.PowerOfTen(2 * Exact.Digits);

    // The exact sum of the non-null values, 0 for none. Only a sum beyond 64
    // bits is an error, a partial sum never is.
    public static long? Sum(IEnumerable<long?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        Int128 total = Int128.Zero;

        foreach (long? value in values)
        {
            total = checked(total + value.GetValueOrDefault());
        }

        return total < long.MinValue || total > long.MaxValue
            ? throw new OverflowException("An integer sum beyond 64 bits.")
            : (long)total;
    }

    // The exact sum of the non-null values, 0 for none. Only a sum of
    // magnitude 10^28 or more is an error, a partial sum never is.
    public static decimal? Sum(IEnumerable<decimal?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return Exact.Rounded(Total(values).Total(), Unit);
    }

    // The mean of the non-null values: their exact sum divided as divide
    // divides, although the sum itself may be beyond the range of a decimal.
    public static decimal? Average(IEnumerable<decimal?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        ExactSum total = Total(values);

        return total.Count == 0 ? null : Exact.Rounded(total.Total(), total.Count * Unit);
    }

    // The mean day, rounded to the nearest whole day; a mean exactly halfway
    // between two days is the later one.
    public static DateOnly? Average(IEnumerable<DateOnly?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return Date(TemporalUnits.Mean(Days(values), 1));
    }

    // The mean of the times since midnight, without wrapping, rounded to the
    // nearest microsecond; a mean exactly halfway between two is the later.
    public static TimeOnly? Average(IEnumerable<TimeOnly?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return Time(TemporalUnits.Mean(Ticks(values), TimeSpan.TicksPerMicrosecond));
    }

    // The mean instant, in UTC, rounded to the nearest microsecond; a mean
    // exactly halfway between two is the later one.
    public static DateTime? Average(IEnumerable<DateTime?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        return Instant(TemporalUnits.Mean(Ticks(values), TimeSpan.TicksPerMicrosecond));
    }

    // The middle of the non-null values in their order: the middle one when
    // their number is odd, otherwise the mean of the two middle ones. It is
    // the continuous percentile of 0.5.
    public static decimal? Median(IEnumerable<decimal?> values)
    {
        return PercentileContinuous(values, 0.5m);
    }

    public static DateOnly? Median(IEnumerable<DateOnly?> values)
    {
        return PercentileContinuous(values, 0.5m);
    }

    public static TimeOnly? Median(IEnumerable<TimeOnly?> values)
    {
        return PercentileContinuous(values, 0.5m);
    }

    public static DateTime? Median(IEnumerable<DateTime?> values)
    {
        return PercentileContinuous(values, 0.5m);
    }

    // The value at position fraction x (n - 1), counted from 0, among the n
    // non-null values in order, interpolated linearly between the two values
    // next to that position, exactly, and rounded once.
    public static decimal? PercentileContinuous(
        IEnumerable<decimal?> values,
        decimal fraction
    )
    {
        ArgumentNullException.ThrowIfNull(values);

        Percentiles.Check(fraction);
        List<decimal> present = [];

        foreach (decimal? value in values)
        {
            if (value is decimal number)
            {
                present.Add(number);
            }
        }

        return Percentiles.Continuous(present, fraction);
    }

    // A percentile of dates interpolates in days, as average averages:
    // rounded to the nearest whole day, a value exactly halfway to the later.
    public static DateOnly? PercentileContinuous(
        IEnumerable<DateOnly?> values,
        decimal fraction
    )
    {
        ArgumentNullException.ThrowIfNull(values);

        Percentiles.Check(fraction);

        return Date(TemporalUnits.Percentile(Days(values), fraction, 1));
    }

    // A percentile of times interpolates in the time since midnight, rounded
    // to the nearest microsecond, a value exactly halfway to the later.
    public static TimeOnly? PercentileContinuous(
        IEnumerable<TimeOnly?> values,
        decimal fraction
    )
    {
        ArgumentNullException.ThrowIfNull(values);

        Percentiles.Check(fraction);

        return Time(
            TemporalUnits.Percentile(
                Ticks(values),
                fraction,
                TimeSpan.TicksPerMicrosecond
            )
        );
    }

    // A percentile of instants, in UTC, rounded to the nearest microsecond, a
    // value exactly halfway to the later.
    public static DateTime? PercentileContinuous(
        IEnumerable<DateTime?> values,
        decimal fraction
    )
    {
        ArgumentNullException.ThrowIfNull(values);

        Percentiles.Check(fraction);

        return Instant(
            TemporalUnits.Percentile(
                Ticks(values),
                fraction,
                TimeSpan.TicksPerMicrosecond
            )
        );
    }

    // The value number ceiling(fraction x n), counted from 1, among the n
    // non-null values in the order of their type; the first value for the
    // fraction 0. It is one of the values, so it exists for every type; null
    // for no values.
    public static T PercentileDiscrete<T>(IEnumerable<T> values, decimal fraction)
    {
        ArgumentNullException.ThrowIfNull(values);

        Percentiles.Check(fraction);
        IComparer<T> order = Order<T>();
        List<T> ordered = [];

        foreach (T value in values)
        {
            if (value is not null)
            {
                ordered.Add(value);
            }
        }

        if (ordered.Count == 0)
        {
            return default!;
        }

        ordered.Sort(order);

        return Canonical(ordered[Percentiles.Rank(fraction, ordered.Count)]);
    }

    // The non-null value that occurs most often, compared as equal compares;
    // among several, the least by the order of the type. null for no values.
    public static T Mode<T>(IEnumerable<T> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        IComparer<T> order = Order<T>();

        // A key is never null, which a tuple of one value says to the
        // dictionary; it is equal to another as the values are.
        Dictionary<ValueTuple<T>, long> counts = [];

        foreach (T value in values)
        {
            if (value is not null)
            {
                ValueTuple<T> key = ValueTuple.Create(value);
                counts[key] = counts.GetValueOrDefault(key) + 1;
            }
        }

        T mode = default!;
        long most = 0;

        foreach ((ValueTuple<T> key, long count) in counts)
        {
            if (count > most || (count == most && order.Compare(key.Item1, mode) < 0))
            {
                mode = key.Item1;
                most = count;
            }
        }

        return Canonical(mode);
    }

    // The mean of the squared differences between the non-null values and
    // their mean, as one division of exact sums; 0 for one value.
    public static decimal? VariancePopulation(IEnumerable<decimal?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        ExactMoments moments = ExactMoments.Of(values);

        return moments.Count == 0
            ? null
            : Exact.Rounded(moments.Spread(), Squared(moments.Count, 0));
    }

    // The sum of those squared differences divided by n - 1; null also for
    // one value, which has no sample variance.
    public static decimal? VarianceSample(IEnumerable<decimal?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        ExactMoments moments = ExactMoments.Of(values);

        return moments.Count < 2
            ? null
            : Exact.Rounded(moments.Spread(), Squared(moments.Count, 1));
    }

    // The square root of the exact population variance, not of its rounded
    // value.
    public static decimal? StandardDeviationPopulation(IEnumerable<decimal?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        ExactMoments moments = ExactMoments.Of(values);

        return moments.Count == 0
            ? null
            : Exact.SquareRoot(moments.Spread(), Squared(moments.Count, 0));
    }

    // The square root of the exact sample variance; null also for one value.
    public static decimal? StandardDeviationSample(IEnumerable<decimal?> values)
    {
        ArgumentNullException.ThrowIfNull(values);

        ExactMoments moments = ExactMoments.Of(values);

        return moments.Count < 2
            ? null
            : Exact.SquareRoot(moments.Spread(), Squared(moments.Count, 1));
    }

    // Over the pairs of non-null values, the mean of the products of their
    // differences from the two means; 0 for one pair.
    public static decimal? CovariancePopulation(
        IEnumerable<(decimal?, decimal?)> pairs
    )
    {
        ArgumentNullException.ThrowIfNull(pairs);

        ExactPairMoments moments = ExactPairMoments.Of(pairs);

        return moments.Count == 0
            ? null
            : Exact.Rounded(moments.Product(), Squared(moments.Count, 0));
    }

    // The sum of those products divided by n - 1; null also for one pair.
    public static decimal? CovarianceSample(IEnumerable<(decimal?, decimal?)> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);

        ExactPairMoments moments = ExactPairMoments.Of(pairs);

        return moments.Count < 2
            ? null
            : Exact.Rounded(moments.Product(), Squared(moments.Count, 1));
    }

    // Pearson's coefficient: the covariance divided by the product of the
    // two standard deviations, as one exact ratio under one square root, so
    // it never falls outside -1 to 1. null also for one pair, and when either
    // column has the same value in every pair.
    public static decimal? Correlation(IEnumerable<(decimal?, decimal?)> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);

        ExactPairMoments moments = ExactPairMoments.Of(pairs);
        BigInteger spreads = moments.Left.Spread() * moments.Right.Spread();

        if (spreads.IsZero)
        {
            return null;
        }

        BigInteger product = moments.Product();
        decimal magnitude = Exact.SquareRoot(product * product, spreads);

        // A coefficient that rounds to zero has no sign.
        return product.Sign < 0 && magnitude != 0m ? -magnitude : magnitude;
    }

    // The slope of the least-squares line of y, the first value of a pair,
    // on x, the second: the population covariance divided by the population
    // variance of x. null also for one pair, and when x has the same value in
    // every pair.
    public static decimal? RegressionSlope(IEnumerable<(decimal?, decimal?)> pairs)
    {
        ArgumentNullException.ThrowIfNull(pairs);

        ExactPairMoments moments = ExactPairMoments.Of(pairs);
        BigInteger spread = moments.Right.Spread();

        return spread.IsZero ? null : Exact.Rounded(moments.Product(), spread);
    }

    // y of that line where x is 0, the mean of y minus the slope times the
    // mean of x, as one division: it is not computed from the rounded slope.
    // null in the same cases.
    public static decimal? RegressionIntercept(
        IEnumerable<(decimal?, decimal?)> pairs
    )
    {
        ArgumentNullException.ThrowIfNull(pairs);

        ExactPairMoments moments = ExactPairMoments.Of(pairs);
        BigInteger spread = moments.Right.Spread();

        return spread.IsZero
            ? null
            : Exact.Rounded(
                (moments.Left.Sum() * spread)
                    - (moments.Product() * moments.Right.Sum()),
                moments.Count * spread * Unit
            );
    }

    // The share of the variance of y that the line explains: the squared
    // covariance divided by the product of the two variances, as one
    // division, and 1 when y has the same value in every pair. null in the
    // same cases.
    public static decimal? RegressionRSquared(
        IEnumerable<(decimal?, decimal?)> pairs
    )
    {
        ArgumentNullException.ThrowIfNull(pairs);

        ExactPairMoments moments = ExactPairMoments.Of(pairs);
        BigInteger spread = moments.Right.Spread();
        BigInteger explained = moments.Left.Spread();
        BigInteger product = moments.Product();

        return spread.IsZero ? null
            : explained.IsZero ? 1m
            : Exact.Rounded(product * product, spread * explained);
    }

    private static ExactSum Total(IEnumerable<decimal?> values)
    {
        ExactSum total = new ExactSum(Exact.Digits);

        foreach (decimal? value in values)
        {
            if (value is decimal number)
            {
                (BigInteger mantissa, int scale) = Exact.Parts(number);
                total.Add(mantissa, scale);
            }
        }

        return total;
    }

    // What n^2 times a population variance, in units of 10^-56, is divided
    // by; n (n - 1) takes the place of n^2 for the sample form.
    private static BigInteger Squared(long count, int less)
    {
        return (BigInteger)count * (count - less) * SquareUnit;
    }

    private static IEnumerable<long?> Days(IEnumerable<DateOnly?> values)
    {
        return values.Select(value => (long?)value?.DayNumber);
    }

    private static IEnumerable<long?> Ticks(IEnumerable<TimeOnly?> values)
    {
        return values.Select(value => value?.Ticks);
    }

    // An instant is its ticks, read as UTC whatever its kind says.
    private static IEnumerable<long?> Ticks(IEnumerable<DateTime?> values)
    {
        return values.Select(value => value?.Ticks);
    }

    private static DateOnly? Date(long? day)
    {
        return day is long number ? DateOnly.FromDayNumber((int)number) : null;
    }

    // A time of day that carries a fraction of its last microsecond may round
    // to the end of the day, which is not a time.
    private static TimeOnly? Time(long? ticks)
    {
        return ticks is not long number ? null
            : number > TimeOnly.MaxValue.Ticks
                ? throw new ArgumentOutOfRangeException(
                    nameof(ticks),
                    "A time that rounds to 24:00:00, which is not a time of day."
                )
            : new TimeOnly(number);
    }

    private static DateTime? Instant(long? ticks)
    {
        return ticks is not long number ? null
            : number > DateTime.MaxValue.Ticks
                ? throw new ArgumentOutOfRangeException(
                    nameof(ticks),
                    "A datetime that rounds to a year after 9999."
                )
            : new DateTime(number, DateTimeKind.Utc);
    }

    // The order of a type, as the specification orders it: strings by code
    // point, false before true, the rest by value, which for a uuid is the
    // order of its 32 hexadecimal digits.
    private static IComparer<T> Order<T>()
    {
        IComparer<T>? order =
            CodePoints.Comparer as IComparer<T>
            ?? Comparer<long?>.Default as IComparer<T>
            ?? Comparer<decimal?>.Default as IComparer<T>
            ?? Comparer<bool?>.Default as IComparer<T>
            ?? Comparer<DateOnly?>.Default as IComparer<T>
            ?? Comparer<TimeOnly?>.Default as IComparer<T>
            ?? Comparer<DateTime?>.Default as IComparer<T>
            ?? Comparer<Guid?>.Default as IComparer<T>;

        return order
            ?? throw new NotSupportedException(
                $"Values of the type {typeof(T)} have no order in PureQL."
            );
    }

    // The one spelling of a value that rows may hold in several: a decimal
    // without the trailing zeros of its fraction, an instant in UTC. Equal
    // values are then the same whichever row they come from.
    private static T Canonical<T>(T value)
    {
        object? spelling = value switch
        {
            decimal number => Shortest(number),
            DateTime instant => DateTime.SpecifyKind(instant, DateTimeKind.Utc),
            _ => null,
        };

        return spelling is T canonical ? canonical : value;
    }

    private static decimal Shortest(decimal number)
    {
        (BigInteger mantissa, int scale) = Exact.Parts(number);

        return Exact.Decimal(BigInteger.Abs(mantissa), scale, mantissa.Sign < 0);
    }
}
