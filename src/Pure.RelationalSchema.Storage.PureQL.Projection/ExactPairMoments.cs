using System.Numerics;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// What the statistics of two columns are computed from, read in one pass. A
// pair counts only when both of its values are non-null, so the moments of
// each column are those of the pairs that count.
internal sealed class ExactPairMoments
{
    private readonly ExactSum _products = new ExactSum(2 * Exact.Digits);

    public long Count => _products.Count;

    // The first values of the pairs: left, and the y of a regression.
    public ExactMoments Left { get; } = new ExactMoments();

    // The second values of the pairs: right, and the x of a regression.
    public ExactMoments Right { get; } = new ExactMoments();

    public static ExactPairMoments Of(IEnumerable<(decimal?, decimal?)> pairs)
    {
        ExactPairMoments moments = new ExactPairMoments();

        foreach ((decimal? first, decimal? second) in pairs)
        {
            if (first is decimal left && second is decimal right)
            {
                (BigInteger leftMantissa, int leftScale) = Exact.Parts(left);
                (BigInteger rightMantissa, int rightScale) = Exact.Parts(right);
                moments.Left.Add(leftMantissa, leftScale);
                moments.Right.Add(rightMantissa, rightScale);
                moments._products.Add(
                    leftMantissa * rightMantissa,
                    leftScale + rightScale
                );
            }
        }

        return moments;
    }

    // n times the sum of the products minus the product of the two sums, in
    // units of 10^-56: n^2 times the population covariance.
    public BigInteger Product()
    {
        return (Count * _products.Total()) - (Left.Sum() * Right.Sum());
    }
}
