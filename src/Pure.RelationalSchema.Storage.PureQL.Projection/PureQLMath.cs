using System.Numerics;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The numbers of the specification: a 64-bit integer and an exact decimal
// m x 10^-s with |m| < 10^28 and 0 <= s <= 28. Every decimal result is the
// exact mathematical value rounded once, half away from zero, to the most
// decimal places that fit, so it has the same digits in every interpreter;
// a result of magnitude 10^28 or more is an OverflowException and a value
// outside a function's domain an ArgumentOutOfRangeException. An operand may
// be any System.Decimal, also one of the few with 29 digits that it holds
// (1.0000000000000000000000000001): it is read exactly.
//
// A power, a logarithm or a trigonometric function is computed in fixed
// point with more bits than a decimal has, as a range that holds the exact
// value, and the bits are doubled until both ends of the range round to the
// same decimal. That ends, because the exact value is never where the
// rounding changes: such a point is a decimal of at most 29 places, the
// functions are irrational at every decimal argument but the few handled
// before (Lindemann-Weierstrass for e^x, ln and the trigonometric
// functions; a ratio of logarithms or a root that is rational is a ratio of
// small integers or found exactly), and a rational power with more places
// than that is not such a point either.
public static class PureQLMath
{
    // pi rounded to the decimal precision.
    public const decimal Pi = 3.141592653589793238462643383m;

    // pi / 2 rounded to the decimal precision.
    private const decimal HalfPi = 1.570796326794896619231321692m;

    // The bits after the binary point of the first range computed: 2^-128
    // is ten digits beyond the 28 of a decimal, so a second range is rare.
    private const int Bits = 128;

    private const string Overflow = "A decimal of magnitude 10^28 or more.";

    // Twice the least magnitude that rounds to 10^28: 2 x 10^28 - 1.
    private static readonly BigInteger Edge = (2 * Exact.PowerOfTen(Exact.Digits)) - 1;

    // add, subtract and multiply of two decimals: the exact result, rounded
    // once when it needs more digits than a decimal has. An operator over a
    // list calls them one step at a time.
    public static decimal Add(decimal left, decimal right)
    {
        return Exact.Add(left, right);
    }

    public static decimal Subtract(decimal left, decimal right)
    {
        return Exact.Subtract(left, right);
    }

    public static decimal Multiply(decimal left, decimal right)
    {
        return Exact.Multiply(left, right);
    }

    // The exact quotient, rounded half away from zero to the decimal
    // precision.
    public static decimal Divide(decimal left, decimal right)
    {
        return Exact.Divide(left, right);
    }

    // integerDivide: the exact quotient truncated toward zero, never rounded
    // first, and an integer whatever the operands; one outside 64 bits is an
    // OverflowException.
    public static long IntegerDivide(decimal left, decimal right)
    {
        (BigInteger dividend, BigInteger divisor) = Aligned(left, right);

        return divisor.IsZero
            ? throw new DivideByZeroException()
            : (long)BigInteger.Divide(dividend, divisor);
    }

    // modulo: left - right x integerDivide(left, right), which is exact, has
    // the sign of left and no more decimal places than the operands.
    public static decimal Modulo(decimal left, decimal right)
    {
        (BigInteger dividend, BigInteger divisor) = Aligned(left, right);

        if (divisor.IsZero)
        {
            throw new DivideByZeroException();
        }

        BigInteger remainder = BigInteger.Remainder(dividend, divisor);
        int scale = Math.Max(Exact.Parts(left).Scale, Exact.Parts(right).Scale);

        return Exact.Decimal(BigInteger.Abs(remainder), scale, remainder.Sign < 0);
    }

    // modulo of two integers, with the sign of left. Nothing is left over
    // -1, also where the quotient itself is outside 64 bits.
    public static long Modulo(long left, long right)
    {
        return right == -1 ? 0 : left % right;
    }

    // round with digits: to that many decimal places, a negative number of
    // them to tens, hundreds and so on. A value exactly halfway goes away
    // from zero, or with halfEven to the neighbour whose last kept digit is
    // even. Digits the value does not have leave it as it is, and a unit
    // of 10^30 or more is above twice any decimal System.Decimal holds, so
    // the result is 0; a unit of 10^28 or 10^29 gives 0 or an overflow.
    public static decimal Round(decimal value, long digits, MidpointRounding mode)
    {
        Check(mode);
        (BigInteger mantissa, int scale) = Exact.Parts(value);

        return digits >= scale ? value
            : digits < -Exact.Digits - 1 ? 0m
            : Placed(
                Nearest(mantissa, Exact.PowerOfTen(scale - (int)digits), mode),
                (int)digits
            );
    }

    // truncate with digits: the digits beyond them are dropped, toward zero.
    public static decimal Truncate(decimal value, long digits)
    {
        (BigInteger mantissa, int scale) = Exact.Parts(value);

        return digits >= scale ? value
            : digits < -Exact.Digits ? 0m
            : Placed(
                BigInteger.Divide(mantissa, Exact.PowerOfTen(scale - (int)digits)),
                (int)digits
            );
    }

    // round of an integer, which stays an integer: only a negative number
    // of digits changes it, and a result outside 64 bits is an
    // OverflowException.
    public static long Round(long value, long digits, MidpointRounding mode)
    {
        Check(mode);

        if (digits >= 0)
        {
            return value;
        }

        if (digits < -19)
        {
            return 0;
        }

        BigInteger unit = Exact.PowerOfTen((int)-digits);

        return (long)(Nearest(value, unit, mode) * unit);
    }

    // truncate of an integer: to tens, hundreds and so on, toward zero.
    public static long Truncate(long value, long digits)
    {
        return digits >= 0 ? value
            : digits < -18 ? 0
            : value - (value % (long)Exact.PowerOfTen((int)-digits));
    }

    // dividend / divisor, computed exactly and rounded once to a whole
    // number.
    public static decimal RoundQuotient(
        decimal dividend,
        decimal divisor,
        MidpointRounding mode
    )
    {
        Check(mode);
        (BigInteger numerator, BigInteger denominator) = Aligned(dividend, divisor);

        return denominator.IsZero
            ? throw new DivideByZeroException()
            : Exact.Rounded(
                Nearest(
                    numerator * denominator.Sign,
                    BigInteger.Abs(denominator),
                    mode
                ),
                BigInteger.One
            );
    }

    // 10^exponent, for an exponent from 0 to 28.
    public static decimal PowerOfTen(long exponent)
    {
        return exponent is < 0 or > Exact.Digits
            ? throw new ArgumentOutOfRangeException(
                nameof(exponent),
                exponent,
                "A power of ten outside 0 to 28."
            )
            : (decimal)Exact.PowerOfTen((int)exponent);
    }

    // operand raised to exponent. Any number to the exponent 0 is 1, zero
    // included; zero takes no negative exponent and a negative operand only
    // a whole one.
    public static decimal Power(decimal operand, decimal exponent)
    {
        (BigInteger power, BigInteger root) = Ratio(exponent);

        if (power.IsZero)
        {
            return 1m;
        }

        (BigInteger numerator, BigInteger denominator) = Ratio(operand);

        if (numerator.IsZero)
        {
            return power.Sign > 0
                ? 0m
                : throw new ArgumentOutOfRangeException(
                    nameof(exponent),
                    exponent,
                    "Zero to a negative exponent."
                );
        }

        if (numerator.Sign < 0 && !root.IsOne)
        {
            throw new ArgumentOutOfRangeException(
                nameof(exponent),
                exponent,
                "A negative number to an exponent that is not a whole number."
            );
        }

        BigInteger magnitude = BigInteger.Abs(numerator);

        return Signed(
            magnitude == denominator
                ? 1m
                : PositivePower(magnitude, denominator, power, root),
            numerator.Sign < 0 && !power.IsEven
        );
    }

    // The square root, exact where a decimal holds it.
    public static decimal Sqrt(decimal operand)
    {
        (BigInteger mantissa, int scale) = Exact.Parts(operand);

        return mantissa.Sign < 0
            ? throw new ArgumentOutOfRangeException(
                nameof(operand),
                operand,
                "The square root of a negative number."
            )
            : Exact.SquareRoot(mantissa, Exact.PowerOfTen(scale));
    }

    // e raised to operand: 1 for 0, and irrational for every other decimal.
    public static decimal Exp(decimal operand)
    {
        (BigInteger mantissa, int scale) = Exact.Parts(operand);
        BigInteger denominator = Exact.PowerOfTen(scale);

        return mantissa.IsZero
            ? 1m
            : Converge(bits => Exponential(mantissa, denominator, bits));
    }

    // The natural logarithm: 0 for 1, and irrational for every other
    // decimal.
    public static decimal Ln(decimal operand)
    {
        (BigInteger numerator, BigInteger denominator) = Positive(
            operand,
            nameof(operand)
        );

        return numerator == denominator
            ? 0m
            : Converge(bits =>
                Near(
                    FixedPoint.Ln(numerator, denominator, bits),
                    FixedPoint.Error,
                    bits
                )
            );
    }

    // The logarithm to base 10: a whole number for a power of ten, and
    // irrational for every other decimal.
    public static decimal Log10(decimal operand)
    {
        (BigInteger numerator, BigInteger denominator) = Positive(
            operand,
            nameof(operand)
        );

        return numerator.IsOne && Zeros(denominator) is int below ? -below
            : denominator.IsOne && Zeros(numerator) is int above ? above
            : Converge(bits =>
                Quotient(
                    FixedPoint.Ln(numerator, denominator, bits),
                    FixedPoint.Ln10(bits)
                )
            );
    }

    // The logarithm of operand to base, ln operand / ln base. Where it is
    // rational, operand^n = base^m for whole numbers below 96, since a
    // mantissa below 2^96 is at most a 95th power: the logarithm is then
    // m / n, which has at most 6 decimal places and is never halfway between
    // two decimals, so its range is rounded like an irrational one.
    public static decimal Log(decimal operand, decimal @base)
    {
        (BigInteger numerator, BigInteger denominator) = Positive(
            operand,
            nameof(operand)
        );
        (BigInteger baseNumerator, BigInteger baseDenominator) = Positive(
            @base,
            nameof(@base)
        );

        if (baseNumerator == baseDenominator)
        {
            throw new ArgumentOutOfRangeException(
                nameof(@base),
                @base,
                "A logarithm to the base 1."
            );
        }

        if (numerator == denominator)
        {
            return 0m;
        }

        // ln base is at least |n - d| / max(n, d) in magnitude for a base
        // n / d, which may be as small as 10^-28. The bits it lacks before
        // its first one are added twice, since the error of the quotient
        // grows with the square of 1 / ln base; the divisor is then far from
        // 0 at every size.
        long close =
            BigInteger.Max(baseNumerator, baseDenominator).GetBitLength()
            - BigInteger.Abs(baseNumerator - baseDenominator).GetBitLength();
        int more = (2 * (int)close) + 8;

        return Converge(bits =>
            Quotient(
                FixedPoint.Ln(numerator, denominator, bits + more),
                FixedPoint.Ln(baseNumerator, baseDenominator, bits + more)
            )
        );
    }

    // The sine of an angle in radians: 0 for 0, and irrational for every
    // other decimal, as are the cosine and the tangent.
    public static decimal Sin(decimal operand)
    {
        (BigInteger mantissa, int scale) = Exact.Parts(operand);
        BigInteger denominator = Exact.PowerOfTen(scale);

        return mantissa.IsZero
            ? 0m
            : Converge(bits =>
                Near(
                    FixedPoint.SinCos(mantissa, denominator, bits).Sine,
                    FixedPoint.Error,
                    bits
                )
            );
    }

    // The cosine of an angle in radians: 1 for 0.
    public static decimal Cos(decimal operand)
    {
        (BigInteger mantissa, int scale) = Exact.Parts(operand);
        BigInteger denominator = Exact.PowerOfTen(scale);

        return mantissa.IsZero
            ? 1m
            : Converge(bits =>
                Near(
                    FixedPoint.SinCos(mantissa, denominator, bits).Cosine,
                    FixedPoint.Error,
                    bits
                )
            );
    }

    // The tangent is finite for every decimal, since pi / 2 is not one: next
    // to a pole it is computed with more bits, until its range is narrow.
    public static decimal Tan(decimal operand)
    {
        (BigInteger mantissa, int scale) = Exact.Parts(operand);
        BigInteger denominator = Exact.PowerOfTen(scale);

        return mantissa.IsZero
            ? 0m
            : Converge(bits => Tangent(mantissa, denominator, bits));
    }

    // The angle from -pi/2 to pi/2 with this sine.
    public static decimal Asin(decimal operand)
    {
        (BigInteger mantissa, int scale) = Exact.Parts(operand);
        BigInteger sine = BigInteger.Abs(mantissa);
        BigInteger one = Exact.PowerOfTen(scale);

        return sine > one
                ? throw new ArgumentOutOfRangeException(
                    nameof(operand),
                    operand,
                    "The arc sine of a number outside -1 to 1."
                )
            : mantissa.IsZero ? 0m
            : Signed(
                sine == one
                    ? HalfPi
                    : Converge(bits => Near(ArcSine(sine, one, bits), 5, bits)),
                mantissa.Sign < 0
            );
    }

    // The angle from 0 to pi with this cosine: pi/2 less the arc sine.
    public static decimal Acos(decimal operand)
    {
        (BigInteger mantissa, int scale) = Exact.Parts(operand);
        BigInteger cosine = BigInteger.Abs(mantissa);
        BigInteger one = Exact.PowerOfTen(scale);

        return cosine > one
                ? throw new ArgumentOutOfRangeException(
                    nameof(operand),
                    operand,
                    "The arc cosine of a number outside -1 to 1."
                )
            : mantissa.IsZero ? HalfPi
            : mantissa == one ? 0m
            : cosine == one ? Pi
            : Converge(bits =>
                Near(
                    (FixedPoint.Pi(bits) >> 1)
                        - (mantissa.Sign * ArcSine(cosine, one, bits)),
                    7,
                    bits
                )
            );
    }

    // The angle from -pi/2 to pi/2 with this tangent.
    public static decimal Atan(decimal operand)
    {
        (BigInteger mantissa, int scale) = Exact.Parts(operand);
        BigInteger tangent = BigInteger.Abs(mantissa);
        BigInteger one = Exact.PowerOfTen(scale);

        return mantissa.IsZero
            ? 0m
            : Signed(
                Converge(bits => Near(ArcTangent(tangent, one, bits), 4, bits)),
                mantissa.Sign < 0
            );
    }

    // The angle of the point (x, y), above -pi up to pi.
    public static decimal Atan2(decimal y, decimal x)
    {
        (BigInteger height, BigInteger width) = Aligned(y, x);

        if (height.IsZero)
        {
            return width.Sign > 0 ? 0m
                : width.Sign < 0 ? Pi
                : throw new ArgumentOutOfRangeException(
                    nameof(x),
                    x,
                    "The angle of the point (0, 0)."
                );
        }

        if (width.IsZero)
        {
            return Signed(HalfPi, height.Sign < 0);
        }

        BigInteger rise = BigInteger.Abs(height);
        BigInteger run = BigInteger.Abs(width);

        return Signed(
            Converge(bits =>
            {
                BigInteger angle = ArcTangent(rise, run, bits);

                return Near(
                    width.Sign < 0 ? FixedPoint.Pi(bits) - angle : angle,
                    6,
                    bits
                );
            }),
            height.Sign < 0
        );
    }

    // The decimal that a real number rounds to, from ranges that hold it:
    // an attempt with more bits gives a narrower range, and null while two
    // numbers of its range round differently.
    private static decimal Converge(Func<int, decimal?> attempt)
    {
        int bits = Bits;
        decimal? rounded = attempt(bits);

        while (rounded is null)
        {
            bits *= 2;
            rounded = attempt(bits);
        }

        return rounded.Value;
    }

    // The decimal that every number of a range rounds to, from the low end
    // to the high end, each a ratio with a positive denominator, or with
    // the denominator 0 for an end without a bound; null when the two ends
    // round differently. Rounding never decreases as a number grows, so the
    // ends decide for all the numbers between them, and the range is out of
    // the range of a decimal when both of them are.
    private static decimal? Rounded(
        BigInteger lowNumerator,
        BigInteger lowDenominator,
        BigInteger highNumerator,
        BigInteger highDenominator
    )
    {
        decimal? low = Fitting(lowNumerator, lowDenominator);
        decimal? high = Fitting(highNumerator, highDenominator);

        return low is null && high is null ? throw new OverflowException(Overflow)
            : low == high ? low
            : null;
    }

    // A ratio rounded to the decimal precision, or null when it rounds to a
    // magnitude of 10^28 or more: it is at least 10^28 - 1/2 then.
    private static decimal? Fitting(BigInteger numerator, BigInteger denominator)
    {
        return 2 * BigInteger.Abs(numerator) >= Edge * denominator
            ? null
            : Exact.Rounded(numerator, denominator);
    }

    // The rounding of a number that is within error units of value x 2^-bits.
    private static decimal? Near(BigInteger value, int error, int bits)
    {
        BigInteger one = BigInteger.One << bits;

        return Rounded(value - error, one, value + error, one);
    }

    // The rounding of a quotient, where the dividend and the divisor are
    // fixed-point numbers of one size, each within the error of FixedPoint,
    // and the divisor is far more than that error away from 0.
    private static decimal? Quotient(BigInteger dividend, BigInteger divisor)
    {
        BigInteger low = (dividend * divisor.Sign) - FixedPoint.Error;
        BigInteger high = (dividend * divisor.Sign) + FixedPoint.Error;
        BigInteger least = BigInteger.Abs(divisor) - FixedPoint.Error;
        BigInteger greatest = BigInteger.Abs(divisor) + FixedPoint.Error;

        return Rounded(
            low,
            low.Sign < 0 ? least : greatest,
            high,
            high.Sign < 0 ? greatest : least
        );
    }

    // e^(numerator / denominator), where the ratio may itself be an
    // approximation that moves the result by less than one unit of 2^-bits
    // and is less than 1/2 away from the exact exponent: e^65 is above
    // 10^28, and e^-66 is below half of 10^-28, so it rounds to 0.
    private static decimal? Exponential(
        BigInteger numerator,
        BigInteger denominator,
        int bits
    )
    {
        return 2 * numerator >= 131 * denominator ? throw new OverflowException(Overflow)
            : 2 * numerator <= -133 * denominator ? 0m
            : Near(
                FixedPoint.Exp(numerator, denominator, bits),
                FixedPoint.Error + 1,
                bits
            );
    }

    // (numerator / denominator)^(power / root) for a positive ratio that is
    // not 1 and an exponent in lowest terms that is not 0. The result is
    // rational only when the numerator and the denominator are powers of
    // that root. A whole number, or a ratio raised to at most the 29th
    // power, is then computed exactly. A ratio raised to a higher power has
    // more than 29 decimal places, and every other result is irrational:
    // neither is where the rounding changes, so the range of
    // e^(exponent x ln operand) decides.
    private static decimal PositivePower(
        BigInteger numerator,
        BigInteger denominator,
        BigInteger power,
        BigInteger root
    )
    {
        BigInteger times = BigInteger.Abs(power);

        if (
            Root(numerator, root) is BigInteger top
            && Root(denominator, root) is BigInteger bottom
        )
        {
            if (power.Sign < 0)
            {
                (top, bottom) = (bottom, top);
            }

            if (bottom.IsOne && times >= 94)
            {
                throw new OverflowException(Overflow);
            }

            if (bottom.IsOne || times <= 29)
            {
                return Exact.Rounded(
                    BigInteger.Pow(top, (int)times),
                    BigInteger.Pow(bottom, (int)times)
                );
            }
        }

        // The exponent is below `whole` in magnitude and the logarithm to
        // base 2 of the operand below `gap`, so the result is below
        // 2^(whole x gap), and below 2^95 unless it overflows. The logarithm
        // is computed with as many more bits as the exponent and the result
        // have, since its error is multiplied by both.
        BigInteger whole = (times / root) + 1;
        long gap = Math.Abs(numerator.GetBitLength() - denominator.GetBitLength()) + 1;
        int more = (int)whole.GetBitLength() + (int)BigInteger.Min(95, whole * gap) + 3;

        return Converge(bits =>
            Exponential(
                power * FixedPoint.Ln(numerator, denominator, bits + more),
                root << (bits + more),
                bits
            )
        );
    }

    // The whole number whose power of this degree is the value, a positive
    // integer below 2^96, or null when there is none. Above 1 the value is
    // at most a 95th power, of 2.
    private static BigInteger? Root(BigInteger value, BigInteger degree)
    {
        if (value.IsOne || degree.IsOne)
        {
            return value;
        }

        if (degree > 95)
        {
            return null;
        }

        int exponent = (int)degree;
        BigInteger low = BigInteger.One;
        BigInteger high = BigInteger.One << (int)((value.GetBitLength() / exponent) + 1);

        while (high - low > 1)
        {
            BigInteger middle = (low + high) >> 1;

            if (BigInteger.Pow(middle, exponent) <= value)
            {
                low = middle;
            }
            else
            {
                high = middle;
            }
        }

        return BigInteger.Pow(low, exponent) == value ? low : null;
    }

    // The tangent as a range of its magnitude: the sine and the cosine are
    // each within the error of FixedPoint. A cosine that may be 0 leaves the
    // range without an upper bound, and its low end is then far above 10^28.
    private static decimal? Tangent(
        BigInteger numerator,
        BigInteger denominator,
        int bits
    )
    {
        (BigInteger sine, BigInteger cosine) = FixedPoint.SinCos(
            numerator,
            denominator,
            bits
        );
        BigInteger rise = BigInteger.Abs(sine);
        BigInteger run = BigInteger.Abs(cosine);

        return Rounded(
            BigInteger.Max(rise - FixedPoint.Error, 0),
            run + FixedPoint.Error,
            rise + FixedPoint.Error,
            BigInteger.Max(run - FixedPoint.Error, 0)
        )
            is decimal tangent
            ? Signed(tangent, (sine.Sign < 0) != (cosine.Sign < 0))
            : null;
    }

    // asin(numerator / denominator) x 2^bits within 5 units, for a ratio x
    // between 0 and 1: 2 atan(x / (1 + sqrt(1 - x^2))), where the root is
    // taken with more bits than the result has.
    private static BigInteger ArcSine(
        BigInteger numerator,
        BigInteger denominator,
        int bits
    )
    {
        int size = bits + FixedPoint.Guard;
        BigInteger square = denominator * denominator;
        BigInteger cosine = Exact.IntegerSquareRoot(
            ((square - (numerator * numerator)) << (2 * size)) / square
        );

        return 2
            * FixedPoint.Atan(
                numerator << size,
                denominator * ((BigInteger.One << size) + cosine),
                bits
            );
    }

    // atan(numerator / denominator) x 2^bits within 4 units, for a positive
    // ratio: above 1 it is pi/2 less the arc tangent of the inverse.
    private static BigInteger ArcTangent(
        BigInteger numerator,
        BigInteger denominator,
        int bits
    )
    {
        return numerator <= denominator
            ? FixedPoint.Atan(numerator, denominator, bits)
            : (FixedPoint.Pi(bits) >> 1) - FixedPoint.Atan(denominator, numerator, bits);
    }

    // The mantissas of two decimals at the larger of their scales: their
    // ratio is the ratio of the decimals.
    private static (BigInteger Left, BigInteger Right) Aligned(
        decimal left,
        decimal right
    )
    {
        (BigInteger leftMantissa, int leftScale) = Exact.Parts(left);
        (BigInteger rightMantissa, int rightScale) = Exact.Parts(right);
        int scale = Math.Max(leftScale, rightScale);

        return (
            leftMantissa * Exact.PowerOfTen(scale - leftScale),
            rightMantissa * Exact.PowerOfTen(scale - rightScale)
        );
    }

    // A decimal as a ratio in lowest terms, with a positive denominator.
    private static (BigInteger Numerator, BigInteger Denominator) Ratio(decimal value)
    {
        (BigInteger mantissa, int scale) = Exact.Parts(value);
        BigInteger denominator = Exact.PowerOfTen(scale);
        BigInteger common = BigInteger.GreatestCommonDivisor(mantissa, denominator);

        return (mantissa / common, denominator / common);
    }

    // The ratio of an operand of a logarithm, which is positive.
    private static (BigInteger Numerator, BigInteger Denominator) Positive(
        decimal value,
        string name
    )
    {
        (BigInteger numerator, BigInteger denominator) = Ratio(value);

        return numerator.Sign > 0
            ? (numerator, denominator)
            : throw new ArgumentOutOfRangeException(
                name,
                value,
                "A logarithm of a number, or to a base, that is not positive."
            );
    }

    // The number of zeros of a power of ten, or null for any other positive
    // integer.
    private static int? Zeros(BigInteger value)
    {
        int zeros = 0;

        while ((value % 10).IsZero)
        {
            value /= 10;
            zeros++;
        }

        return value.IsOne ? zeros : null;
    }

    // numerator / denominator, for a positive denominator, rounded to a
    // whole number: a ratio exactly halfway goes away from zero or to the
    // even neighbour.
    private static BigInteger Nearest(
        BigInteger numerator,
        BigInteger denominator,
        MidpointRounding mode
    )
    {
        BigInteger quotient = BigInteger.DivRem(
            BigInteger.Abs(numerator),
            denominator,
            out BigInteger remainder
        );
        int half = (2 * remainder).CompareTo(denominator);

        if (
            half > 0
            || (half == 0 && (mode == MidpointRounding.AwayFromZero || !quotient.IsEven))
        )
        {
            quotient++;
        }

        return numerator.Sign * quotient;
    }

    // A whole number of units of 10^-digits as a decimal.
    private static decimal Placed(BigInteger units, int digits)
    {
        return Exact.Rounded(
            units * Exact.PowerOfTen(Math.Max(-digits, 0)),
            Exact.PowerOfTen(Math.Max(digits, 0))
        );
    }

    // The value with a sign; a zero has none.
    private static decimal Signed(decimal value, bool negative)
    {
        return negative && value != 0m ? -value : value;
    }

    private static void Check(MidpointRounding mode)
    {
        if (mode is not (MidpointRounding.AwayFromZero or MidpointRounding.ToEven))
        {
            throw new ArgumentOutOfRangeException(
                nameof(mode),
                mode,
                "A rounding mode other than half away from zero and half to even."
            );
        }
    }
}
