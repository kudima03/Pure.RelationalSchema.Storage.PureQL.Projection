using System.Numerics;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Where a fraction falls among n values in order. A fraction is a decimal
// from 0 to 1, so a position is an exact ratio and never a rounded one.
internal static class Percentiles
{
    // A fraction outside 0 to 1 is an execution error.
    public static void Check(decimal fraction)
    {
        if (fraction is < 0m or > 1m)
        {
            throw new ArgumentOutOfRangeException(
                nameof(fraction),
                "The fraction of a percentile is from 0 to 1."
            );
        }
    }

    // The position fraction x (n - 1), counted from 0: the index of the value
    // at or below it and how far it is on the way to the next value, as
    // part / whole, which is 0 at the last value.
    public static (int Lower, BigInteger Part, BigInteger Whole) Position(
        decimal fraction,
        int count
    )
    {
        (BigInteger mantissa, int scale) = Exact.Parts(fraction);
        BigInteger whole = Exact.PowerOfTen(scale);
        BigInteger lower = BigInteger.DivRem(
            mantissa * (count - 1),
            whole,
            out BigInteger part
        );

        return ((int)lower, part, whole);
    }

    // The index, counted from 0, of the value number ceiling(fraction x n),
    // counted from 1; the first value for the fraction 0.
    public static int Rank(decimal fraction, int count)
    {
        (BigInteger mantissa, int scale) = Exact.Parts(fraction);
        BigInteger whole = Exact.PowerOfTen(scale);
        BigInteger number = ((mantissa * count) + whole - 1) / whole;

        return number.IsZero ? 0 : (int)number - 1;
    }

    // The continuous percentile of decimals, which this sorts: the value at
    // the position, interpolated linearly between its two neighbours from
    // exact values and rounded once. null for no values.
    public static decimal? Continuous(List<decimal> values, decimal fraction)
    {
        if (values.Count == 0)
        {
            return null;
        }

        values.Sort();
        (int lower, BigInteger part, BigInteger whole) = Position(
            fraction,
            values.Count
        );
        (BigInteger below, int belowScale) = Exact.Parts(values[lower]);
        (BigInteger above, int aboveScale) = Exact.Parts(
            values[Math.Min(lower + 1, values.Count - 1)]
        );
        int scale = Math.Max(belowScale, aboveScale);
        below *= Exact.PowerOfTen(scale - belowScale);
        above *= Exact.PowerOfTen(scale - aboveScale);

        return Exact.Rounded(
            (below * whole) + (part * (above - below)),
            whole * Exact.PowerOfTen(scale)
        );
    }
}
