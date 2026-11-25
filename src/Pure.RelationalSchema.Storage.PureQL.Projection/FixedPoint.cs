using System.Collections.Concurrent;
using System.Numerics;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The elementary functions in binary fixed point, for results that are
// rounded to a decimal afterwards. A real number v is held as an integer near
// v x 2^bits. Every function takes its argument exactly, as a ratio of two
// integers, and returns a result less than two units of 2^-bits away from the
// exact value: it computes with Guard more bits than it returns, and its
// rounding errors, a few per term of a series, stay far below 2^Guard units.
// Nothing here is binary floating point.
internal static class FixedPoint
{
    // The largest distance, in units of 2^-bits, of a result from the exact
    // value.
    public const int Error = 2;

    // The bits computed beyond the ones returned.
    public const int Guard = 32;

    // Constants are computed once for a size that is a multiple of this
    // number of bits, and cut down to the size asked for.
    private const int Step = 256;

    // The times the argument of e^x is halved, and the result squared.
    private const int Squarings = 8;

    // The times the angle of an arc tangent is halved.
    private const int Halvings = 3;

    private static readonly ConcurrentDictionary<int, BigInteger> PiCache = new();

    private static readonly ConcurrentDictionary<int, BigInteger> Ln2Cache = new();

    private static readonly ConcurrentDictionary<int, BigInteger> Ln10Cache = new();

    // pi, by Machin's formula: 16 atan(1/5) - 4 atan(1/239).
    public static BigInteger Pi(int bits)
    {
        return Constant(
            PiCache,
            bits,
            static size =>
                (16 * InverseSeries(5, size, true)) - (4 * InverseSeries(239, size, true))
        );
    }

    // ln 2 = 2 atanh(1/3).
    public static BigInteger Ln2(int bits)
    {
        return Constant(Ln2Cache, bits, static size => 2 * InverseSeries(3, size, false));
    }

    // ln 10 = 3 ln 2 + ln 1.25 = 6 atanh(1/3) + 2 atanh(1/9).
    public static BigInteger Ln10(int bits)
    {
        return Constant(
            Ln10Cache,
            bits,
            static size =>
                (6 * InverseSeries(3, size, false)) + (2 * InverseSeries(9, size, false))
        );
    }

    // e^(numerator / denominator), for a positive denominator and a ratio
    // below 128 in magnitude. The argument is x = k ln 2 + r with r from 0
    // to ln 2, and e^x = 2^k (e^(r / 2^8))^(2^8), the inner power by its
    // Taylor series. The squarings double the error eight times and 2^k
    // scales it, so a result of 2^k is computed with k more bits.
    public static BigInteger Exp(BigInteger numerator, BigInteger denominator, int bits)
    {
        // 2^k <= e^x has k <= x / ln 2 < (3 / 2) x.
        int extra =
            numerator.Sign > 0 ? (int)(((numerator / denominator) + 1) * 3 / 2) + 1 : 0;
        int size = bits + Guard + extra;
        BigInteger ln2 = Ln2(size);
        BigInteger argument = Floor(numerator << size, denominator);
        BigInteger doublings = Floor(argument, ln2);
        BigInteger rest = (argument - (doublings * ln2)) >> Squarings;
        BigInteger term = BigInteger.One << size;
        BigInteger sum = term;

        for (int n = 1; !term.IsZero; n++)
        {
            term = ((term * rest) >> size) / n;
            sum += term;
        }

        for (int i = 0; i < Squarings; i++)
        {
            sum = (sum * sum) >> size;
        }

        return sum >> (Guard + extra - (int)doublings);
    }

    // ln(numerator / denominator), both positive. The ratio is 2^k f with f
    // from 1 / sqrt(2) to sqrt(2), and ln f = 2 atanh((f - 1) / (f + 1)).
    public static BigInteger Ln(BigInteger numerator, BigInteger denominator, int bits)
    {
        int size = bits + Guard;
        int exponent = (int)(numerator.GetBitLength() - denominator.GetBitLength());
        int shift = size + 1 - exponent;
        BigInteger one = BigInteger.One << size;
        BigInteger root = one * 181;

        // The ratio over 2^exponent is between 1/2 and 2; here with one more
        // bit, which either of the two cases below uses up.
        BigInteger fraction =
            (numerator << Math.Max(shift, 0)) / (denominator << Math.Max(-shift, 0));

        if (fraction > (root >> 6))
        {
            exponent++;
            fraction >>= 2;
        }
        else if (fraction < (root >> 7))
        {
            exponent--;
        }
        else
        {
            fraction >>= 1;
        }

        BigInteger difference = fraction - one;
        BigInteger series = OddSeries(
            (BigInteger.Abs(difference) << size) / (fraction + one),
            size,
            false
        );

        return ((2 * difference.Sign * series) + (exponent * Ln2(size))) >> Guard;
    }

    // The sine and the cosine of numerator / denominator, for a positive
    // denominator. The angle is q pi/2 + r with r from -pi/4 to pi/4, where
    // pi has as many more bits as q has, so that r is as exact as the angle;
    // then the two Taylor series of r, placed by the quadrant q.
    public static (BigInteger Sine, BigInteger Cosine) SinCos(
        BigInteger numerator,
        BigInteger denominator,
        int bits
    )
    {
        int size = bits + Guard;
        BigInteger magnitude = BigInteger.Abs(numerator);
        long whole = magnitude.GetBitLength() - denominator.GetBitLength();
        int extra = (int)Math.Max(whole, 0) + 3;
        BigInteger quarter = Pi(size + extra) >> 1;
        BigInteger angle = (magnitude << (size + extra)) / denominator;
        BigInteger turns = (angle + (quarter >> 1)) / quarter;
        BigInteger rest = (angle - (turns * quarter)) >> extra;
        BigInteger square = (rest * rest) >> size;
        BigInteger term = BigInteger.Abs(rest);
        BigInteger sine = term;

        for (int n = 2; !term.IsZero; n += 2)
        {
            term = ((term * square) >> size) / (n * (n + 1));
            sine += (n & 2) != 0 ? -term : term;
        }

        term = BigInteger.One << size;
        BigInteger cosine = term;

        for (int n = 1; !term.IsZero; n += 2)
        {
            term = ((term * square) >> size) / (n * (n + 1));
            cosine += (n & 2) == 0 ? -term : term;
        }

        sine *= rest.Sign;

        (sine, cosine) = (int)(turns & 3) switch
        {
            0 => (sine, cosine),
            1 => (cosine, -sine),
            2 => (-sine, -cosine),
            _ => (-cosine, sine),
        };

        return ((numerator.Sign * sine) >> Guard, cosine >> Guard);
    }

    // atan(numerator / denominator), for a ratio from 0 to 1. The angle is
    // halved three times, tan(a / 2) = tan a / (1 + sqrt(1 + tan^2 a)), which
    // leaves a tangent below 0.1 for the series.
    public static BigInteger Atan(BigInteger numerator, BigInteger denominator, int bits)
    {
        int size = bits + Guard;
        BigInteger one = BigInteger.One << size;
        BigInteger tangent = (numerator << size) / denominator;

        for (int i = 0; i < Halvings; i++)
        {
            BigInteger secant = Exact.IntegerSquareRoot(
                (one << size) + (tangent * tangent)
            );
            tangent = (tangent << size) / (one + secant);
        }

        return OddSeries(tangent, size, true) >> (Guard - Halvings);
    }

    // A constant of the size asked for, cut from one computed with Guard more
    // bits than the next multiple of Step.
    private static BigInteger Constant(
        ConcurrentDictionary<int, BigInteger> cache,
        int bits,
        Func<int, BigInteger> compute
    )
    {
        int size = (bits + Step - 1) / Step * Step;

        if (!cache.TryGetValue(size, out BigInteger constant))
        {
            constant = cache.GetOrAdd(size, compute(size + Guard) >> Guard);
        }

        return constant >> (size - bits);
    }

    // 1/n - 1/(3 n^3) + 1/(5 n^5) - ..., which is atan(1/n), or the same sum
    // with every sign positive, which is atanh(1/n), times 2^size.
    private static BigInteger InverseSeries(int n, int size, bool alternating)
    {
        BigInteger power = (BigInteger.One << size) / n;
        BigInteger sum = power;
        int square = n * n;

        for (int odd = 3; !power.IsZero; odd += 2)
        {
            power /= square;
            BigInteger term = power / odd;
            sum += alternating && (odd & 2) != 0 ? -term : term;
        }

        return sum;
    }

    // t - t^3/3 + t^5/5 - ..., which is atan t, or the same sum with every
    // sign positive, which is atanh t, for t from 0 to 1/4 at this size.
    private static BigInteger OddSeries(BigInteger argument, int size, bool alternating)
    {
        BigInteger square = (argument * argument) >> size;
        BigInteger power = argument;
        BigInteger sum = argument;

        for (int odd = 3; !power.IsZero; odd += 2)
        {
            power = (power * square) >> size;
            BigInteger term = power / odd;
            sum += alternating && (odd & 2) != 0 ? -term : term;
        }

        return sum;
    }

    // The largest integer not above dividend / divisor, for a positive
    // divisor.
    private static BigInteger Floor(BigInteger dividend, BigInteger divisor)
    {
        BigInteger quotient = BigInteger.DivRem(
            dividend,
            divisor,
            out BigInteger remainder
        );

        return remainder.Sign < 0 ? quotient - 1 : quotient;
    }
}
