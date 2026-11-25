using System.Numerics;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// What the statistics of one column are computed from, read in one pass: the
// number of its non-null values, their exact sum and the exact sum of their
// squares, which may need more digits than a decimal holds.
internal sealed class ExactMoments
{
    private readonly ExactSum _sum = new ExactSum(Exact.Digits);
    private readonly ExactSum _squares = new ExactSum(2 * Exact.Digits);

    public long Count => _sum.Count;

    public static ExactMoments Of(IEnumerable<decimal?> values)
    {
        ExactMoments moments = new ExactMoments();

        foreach (decimal? value in values)
        {
            if (value is decimal number)
            {
                (BigInteger mantissa, int scale) = Exact.Parts(number);
                moments.Add(mantissa, scale);
            }
        }

        return moments;
    }

    public void Add(BigInteger mantissa, int scale)
    {
        _sum.Add(mantissa, scale);
        _squares.Add(mantissa * mantissa, 2 * scale);
    }

    // The sum of the values, in units of 10^-28.
    public BigInteger Sum()
    {
        return _sum.Total();
    }

    // n times the sum of the squares minus the square of the sum, in units of
    // 10^-56: n^2 times the population variance, and never negative.
    public BigInteger Spread()
    {
        BigInteger sum = _sum.Total();

        return (Count * _squares.Total()) - (sum * sum);
    }
}
