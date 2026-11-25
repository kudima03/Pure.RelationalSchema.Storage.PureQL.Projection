using System.Numerics;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The exact sum of numbers m x 10^-s, the same in any order of the numbers:
// the mantissas of one scale are added as integers and the scales meet only
// in the total. No step is rounded and no partial sum is checked against a
// range, so only the result of an aggregate is. The numbers have at most the
// given number of decimal places.
internal sealed class ExactSum(int places)
{
    private readonly BigInteger[] _sums = new BigInteger[places + 1];

    // How many numbers were added.
    public long Count { get; private set; }

    public void Add(BigInteger mantissa, int scale)
    {
        _sums[scale] += mantissa;
        Count++;
    }

    // The sum as a whole number of units of 10^-places.
    public BigInteger Total()
    {
        BigInteger total = BigInteger.Zero;

        foreach (BigInteger sum in _sums)
        {
            total = (total * 10) + sum;
        }

        return total;
    }
}
