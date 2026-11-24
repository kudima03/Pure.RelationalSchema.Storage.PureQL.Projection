using System.Numerics;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Means and percentiles of dates and times, which are counted in whole
// units from the first one: the days of a date, the ticks of a time of day
// or of an instant. A result is computed exactly and rounded once to the
// nearest whole unit of the type, a day or a microsecond. A date and an
// instant have no zero, so a result exactly halfway between two units is the
// later one, whatever the day an interpreter counts from.
internal static class TemporalUnits
{
    // The mean of the non-null values, a multiple of the unit; null for none.
    public static long? Mean(IEnumerable<long?> values, long unit)
    {
        Int128 sum = Int128.Zero;
        long count = 0;

        foreach (long? value in values)
        {
            if (value is long number)
            {
                sum += number;
                count++;
            }
        }

        return count == 0 ? null : Nearest(sum, count, unit);
    }

    // The continuous percentile of the non-null values, a multiple of the
    // unit: the value at the position fraction x (n - 1), interpolated
    // linearly between its two neighbours; null for no values.
    public static long? Percentile(
        IEnumerable<long?> values,
        decimal fraction,
        long unit
    )
    {
        List<long> ordered = [];

        foreach (long? value in values)
        {
            if (value is long number)
            {
                ordered.Add(number);
            }
        }

        if (ordered.Count == 0)
        {
            return null;
        }

        ordered.Sort();
        (int lower, BigInteger part, BigInteger whole) = Percentiles.Position(
            fraction,
            ordered.Count
        );
        BigInteger below = ordered[lower];
        BigInteger above = ordered[Math.Min(lower + 1, ordered.Count - 1)];

        return Nearest((below * whole) + (part * (above - below)), whole, unit);
    }

    // The multiple of the unit nearest to numerator / denominator, the later
    // of two that are as near. Days and ticks are never negative, so the
    // division is the floor.
    private static long Nearest(BigInteger numerator, BigInteger denominator, long unit)
    {
        BigInteger half = unit * denominator;

        return (long)(((2 * numerator) + half) / (2 * half)) * unit;
    }
}
