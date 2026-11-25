using System.Numerics;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Exact decimal arithmetic, as the specification defines a decimal: a value
// m x 10^-s with an integer |m| < 10^28 and 0 <= s <= 28. A result is computed
// exactly, as a ratio of two integers, and rounded once, half away from zero,
// to the most decimal places that fit; a result of magnitude 10^28 or more is
// an OverflowException. System.Decimal holds every such value, but its own
// arithmetic keeps 29 digits where they fit in 96 bits, rounds half to even
// and reaches 7.9 x 10^28, so results are computed here instead.
internal static class Exact
{
    public const int Digits = 28;

    private static readonly BigInteger[] Powers =
    [
        .. Enumerable.Range(0, (2 * Digits) + 2).Select(power => BigInteger.Pow(10, power)),
    ];

    private static readonly BigInteger Limit = Powers[Digits];

    // 10^13: two decimals with mantissas below it and at most 14 decimal
    // places add, subtract and multiply exactly in System.Decimal, within 28
    // digits, so those operations skip the big integers.
    private const ulong SmallMantissa = 10_000_000_000_000UL;

    public static BigInteger PowerOfTen(int power)
    {
        return power < Powers.Length ? Powers[power] : BigInteger.Pow(10, power);
    }

    // The integer m and the scale s of a decimal: value = m / 10^s.
    public static (BigInteger Mantissa, int Scale) Parts(decimal value)
    {
        Span<int> bits = stackalloc int[4];
        _ = decimal.GetBits(value, bits);
        BigInteger mantissa =
            ((BigInteger)(uint)bits[2] << 64)
            | ((BigInteger)(uint)bits[1] << 32)
            | (uint)bits[0];
        int scale = (bits[3] >> 16) & 0xFF;

        return (bits[3] < 0 ? -mantissa : mantissa, scale);
    }

    // numerator / denominator, rounded half away from zero to the most
    // decimal places that fit a decimal.
    public static decimal Rounded(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (numerator.IsZero)
        {
            return 0m;
        }

        bool negative = (numerator.Sign < 0) != (denominator.Sign < 0);
        BigInteger n = BigInteger.Abs(numerator);
        BigInteger d = BigInteger.Abs(denominator);

        for (int scale = Scale(n / d); scale >= 0; scale--)
        {
            BigInteger mantissa = ((2 * n * PowerOfTen(scale)) + d) / (2 * d);

            if (mantissa < Limit)
            {
                return Decimal(mantissa, scale, negative);
            }
        }

        throw new OverflowException("A decimal of magnitude 10^28 or more.");
    }

    // The square root of numerator / denominator, which is not negative,
    // rounded once like every decimal result.
    public static decimal SquareRoot(BigInteger numerator, BigInteger denominator)
    {
        if (denominator.IsZero)
        {
            throw new DivideByZeroException();
        }

        if (numerator.IsZero)
        {
            return 0m;
        }

        if ((numerator.Sign < 0) != (denominator.Sign < 0))
        {
            throw new ArgumentOutOfRangeException(
                nameof(numerator),
                "The square root of a negative number."
            );
        }

        BigInteger n = BigInteger.Abs(numerator);
        BigInteger d = BigInteger.Abs(denominator);

        for (int scale = Scale(IntegerSquareRoot(n / d)); scale >= 0; scale--)
        {
            // floor(sqrt(v) x 10^s + 1/2) = (isqrt(floor(4 v 10^2s)) + 1) / 2
            BigInteger mantissa =
                (IntegerSquareRoot(4 * n * PowerOfTen(2 * scale) / d) + 1) / 2;

            if (mantissa < Limit)
            {
                return Decimal(mantissa, scale, false);
            }
        }

        throw new OverflowException("A decimal of magnitude 10^28 or more.");
    }

    // The largest integer whose square is not above the value.
    public static BigInteger IntegerSquareRoot(BigInteger value)
    {
        if (value.Sign < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(value));
        }

        if (value < 2)
        {
            return value;
        }

        BigInteger root = BigInteger.One << (int)((value.GetBitLength() + 1) / 2);

        while (true)
        {
            BigInteger next = (root + (value / root)) >> 1;

            if (next >= root)
            {
                return root;
            }

            root = next;
        }
    }

    public static decimal Add(decimal left, decimal right)
    {
        if (IsSmall(left) && IsSmall(right))
        {
            return left + right;
        }

        (BigInteger l, int ls) = Parts(left);
        (BigInteger r, int rs) = Parts(right);
        int scale = Math.Max(ls, rs);

        return Rounded(
            (l * PowerOfTen(scale - ls)) + (r * PowerOfTen(scale - rs)),
            PowerOfTen(scale)
        );
    }

    public static decimal Subtract(decimal left, decimal right)
    {
        return Add(left, -right);
    }

    public static decimal Multiply(decimal left, decimal right)
    {
        if (IsSmall(left) && IsSmall(right))
        {
            return left * right;
        }

        (BigInteger l, int ls) = Parts(left);
        (BigInteger r, int rs) = Parts(right);

        return Rounded(l * r, PowerOfTen(ls + rs));
    }

    public static decimal Divide(decimal left, decimal right)
    {
        (BigInteger l, int ls) = Parts(left);
        (BigInteger r, int rs) = Parts(right);

        return Rounded(l * PowerOfTen(rs), r * PowerOfTen(ls));
    }

    // The decimal of a mantissa below 10^28, without the trailing zeros of
    // its fraction.
    public static decimal Decimal(BigInteger mantissa, int scale, bool negative)
    {
        while (scale > 0 && (mantissa % 10).IsZero)
        {
            mantissa /= 10;
            scale--;
        }

        return new decimal(
            unchecked((int)(uint)(mantissa & uint.MaxValue)),
            unchecked((int)(uint)((mantissa >> 32) & uint.MaxValue)),
            unchecked((int)(uint)(mantissa >> 64)),
            negative && !mantissa.IsZero,
            (byte)scale
        );
    }

    // The most decimal places a value with this whole part has: 28 digits in
    // all, at most 28 after the point. Rounding may still carry into one more
    // digit, which costs a decimal place.
    private static int Scale(BigInteger whole)
    {
        int digits = whole.IsZero ? 0 : Length(whole);
        return digits > Digits
            ? throw new OverflowException("A decimal of magnitude 10^28 or more.")
            : Math.Min(Digits, Digits - digits);
    }

    private static int Length(BigInteger value)
    {
        int digits = (int)(value.GetBitLength() * 30103 / 100000) + 1;
        return value >= PowerOfTen(digits) ? digits + 1
            : value < PowerOfTen(digits - 1) ? digits - 1
            : digits;
    }

    // Whether the mantissa is below 10^13 and the scale at most 14.
    private static bool IsSmall(decimal value)
    {
        Span<int> bits = stackalloc int[4];
        _ = decimal.GetBits(value, bits);

        return bits[2] == 0
            && (((ulong)(uint)bits[1] << 32) | (uint)bits[0]) < SmallMantissa
            && ((bits[3] >> 16) & 0xFF) <= 14;
    }
}
