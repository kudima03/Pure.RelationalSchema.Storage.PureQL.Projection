#!/usr/bin/env python3
"""Writes PureQLMathVectors.cs: the test vectors of PureQLMathTests.

Every expected value is the exact mathematical result rounded once, half away
from zero, to the most decimal places that fit a PureQL decimal (28
significant digits, at most 28 decimal places), or `overflow` / `domain` /
`zero` for the three execution errors.

Two oracles, neither of which shares code with the C# implementation:

* mpmath at 150 digits or more. A value is accepted only when everything
  within 10^-110 of it (relative and absolute) rounds to the same decimal;
  otherwise the precision is doubled. Results that are rational (whole powers,
  exact roots, logarithms of powers of ten) are computed with fractions.
* Python's `decimal` module at 80 to 250 digits, with its own exp, ln, log10
  and sqrt, and Taylor series written here for sin, cos and atan. Every
  vector of the mpmath oracle is checked against it (`--no-check` skips that).

Usage: python3 generate_math_vectors.py OUTPUT.cs [--no-check]
       python3 generate_math_vectors.py OUTPUT.tsv --stress COUNT SEED
The second form writes COUNT fresh random vectors per function as text, for a
run outside the test suite.
Requires mpmath (pip install mpmath); the seed is fixed, so the output is
reproducible.
"""

import decimal
import random
import sys
from fractions import Fraction

from mpmath import mp, mpf

LIMIT = 10**28
# System.Decimal holds a mantissa below 2^96, so a few decimals of 29 digits,
# which the specification itself uses as operands (1.0000000000000000000000000001).
# The operands here go up to that; every expected value is within 28 digits.
HELD = 2**96
MAX = LIMIT - 1
RANDOM = random.Random(20261124)


# ---------------------------------------------------------------- decimals


def spell(mantissa, scale):
    """The one spelling of mantissa x 10^-scale."""
    negative = mantissa < 0
    mantissa = abs(mantissa)
    while scale > 0 and mantissa % 10 == 0:
        mantissa //= 10
        scale -= 1
    digits = str(mantissa).rjust(scale + 1, "0")
    text = digits[: len(digits) - scale] + ("." + digits[len(digits) - scale :] if scale else "")
    return ("-" if negative and mantissa else "") + text


def fraction(text):
    return Fraction(text)


def rounded(value):
    """A fraction rounded half away from zero to the decimal precision."""
    magnitude = abs(value)
    for scale in range(28, -1, -1):
        mantissa = (2 * magnitude.numerator * 10**scale + magnitude.denominator) // (
            2 * magnitude.denominator
        )
        if mantissa < LIMIT:
            return spell(-mantissa if value < 0 else mantissa, scale)
    return "overflow"


def number(digits, scale, negative=False):
    """A random decimal with that many significant digits and that scale."""
    mantissa = RANDOM.randrange(10 ** (digits - 1), 10**digits)
    return spell(-mantissa if negative else mantissa, scale)


def anything(signed=True, least=-28, most=28):
    """A random decimal of any magnitude from 10^least to 10^most."""
    while True:
        digits = RANDOM.randint(1, 28)
        scale = RANDOM.randint(0, 28)
        if least <= digits - scale <= most:
            return number(digits, scale, signed and RANDOM.random() < 0.5)


def between(low, high, scale=None):
    """A random decimal from low to high with up to `scale` decimal places."""
    scale = RANDOM.randint(0, 27) if scale is None else scale
    low, high = Fraction(low), Fraction(high)
    while True:
        units = RANDOM.randint(int(low * 10**scale), int(high * 10**scale))
        if abs(units) < LIMIT:
            return spell(units, scale)
        scale -= 1


# ------------------------------------------------------------ mpmath oracle


def exact(value):
    """An mpf as a fraction."""
    sign, mantissa, exponent, _ = value._mpf_
    return Fraction(-mantissa if sign else mantissa) * Fraction(2) ** exponent


def real(function, *arguments):
    """function(arguments) rounded, at a precision where the rounding is safe."""
    digits = 150
    while True:
        mp.dps = digits
        value = exact(function(*[mpf(argument) for argument in arguments]))
        slack = Fraction(1, 10 ** (digits - 40))
        low = value - abs(value) * slack - slack
        high = value + abs(value) * slack + slack
        if rounded(low) == rounded(high):
            return rounded(value)
        digits *= 2
        if digits > 5000:
            raise ArithmeticError(f"no safe rounding: {function} {arguments}")


def root(value, degree):
    """The whole root of a whole number, or None."""
    low, high = 0, 1 << (value.bit_length() // degree + 1)
    while high - low > 1:
        middle = (low + high) // 2
        if middle**degree <= value:
            low = middle
        else:
            high = middle
    return low if low**degree == value else None


def power(operand, exponent):
    x, y = fraction(operand), fraction(exponent)
    if y == 0:
        return "1"
    if x == 0:
        return "0" if y > 0 else "domain"
    if x < 0 and y.denominator != 1:
        return "domain"
    negative = x < 0 and y.numerator % 2 == 1
    x = abs(x)
    if x == 1:
        return "-1" if negative else "1"
    mp.dps = 60
    estimate = mpf(y.numerator) / y.denominator * mp.log(mpf(x.numerator) / x.denominator)
    if estimate > 70:
        return "overflow"
    if estimate < -70:
        return "0"
    top = root(x.numerator, y.denominator) if y.denominator < 100 else None
    bottom = root(x.denominator, y.denominator) if y.denominator < 100 else None
    if top is not None and bottom is not None and abs(y.numerator) <= 2000:
        result = rounded(Fraction(top, bottom) ** y.numerator)
    else:
        result = real(lambda a, b: mp.power(a, b), spell_fraction(x), exponent)
    return "-" + result if negative and result not in ("0", "overflow") else result


def spell_fraction(value):
    scale = 0
    while (value * 10**scale).denominator != 1:
        scale += 1
    return spell(int(value * 10**scale), scale)


def sqrt(operand):
    x = fraction(operand)
    if x < 0:
        return "domain"
    top, bottom = root(x.numerator, 2), root(x.denominator, 2)
    if top is not None and bottom is not None:
        return rounded(Fraction(top, bottom))
    return real(mp.sqrt, operand)


def exp(operand):
    x = fraction(operand)
    if x == 0:
        return "1"
    if x > 70:
        return "overflow"
    if x < -70:
        return "0"
    return real(mp.exp, operand)


def ln(operand):
    x = fraction(operand)
    if x <= 0:
        return "domain"
    return "0" if x == 1 else real(mp.log, operand)


def log10(operand):
    x = fraction(operand)
    if x <= 0:
        return "domain"
    for power_of_ten in range(-28, 28):
        if x == Fraction(10) ** power_of_ten:
            return str(power_of_ten)
    return real(mp.log10, operand)


def log(operand, base):
    x, b = fraction(operand), fraction(base)
    if x <= 0 or b <= 0 or b == 1:
        return "domain"
    if x == 1:
        return "0"
    return real(lambda a, c: mp.log(a) / mp.log(c), operand, base)


def sin(operand):
    return "0" if fraction(operand) == 0 else real(mp.sin, operand)


def cos(operand):
    return "1" if fraction(operand) == 0 else real(mp.cos, operand)


def tan(operand):
    return "0" if fraction(operand) == 0 else real(mp.tan, operand)


def asin(operand):
    x = fraction(operand)
    if abs(x) > 1:
        return "domain"
    return "0" if x == 0 else real(mp.asin, operand)


def acos(operand):
    x = fraction(operand)
    if abs(x) > 1:
        return "domain"
    return "0" if x == 1 else real(mp.acos, operand)


def atan(operand):
    return "0" if fraction(operand) == 0 else real(mp.atan, operand)


def atan2(y, x):
    if fraction(y) == 0 and fraction(x) == 0:
        return "domain"
    if fraction(y) == 0 and fraction(x) > 0:
        return "0"
    return real(mp.atan2, y, x)


# ----------------------------------------------------------- decimal oracle
# Python's decimal module: correctly rounded exp, ln, log10 and sqrt, and
# Taylor series for the functions it lacks. A value is compared only where
# everything within 10^-60 of it rounds to one decimal.

D = decimal.Decimal


def settled(value, digits=60):
    value = Fraction(value)
    slack = Fraction(1, 10**digits)
    low = value - abs(value) * slack - slack
    high = value + abs(value) * slack + slack
    return rounded(value) if rounded(low) == rounded(high) else None


def d_pi():
    def inverse(n):
        total, term, k = D(0), D(1) / n, 0
        while term != 0:
            total += term / (2 * k + 1) * (-1 if k % 2 else 1)
            term /= n * n
            k += 1
            if term.adjusted() < -decimal.getcontext().prec - 5:
                break
        return total

    return 16 * inverse(5) - 4 * inverse(239)


def d_sin_cos(x):
    half = d_pi() / 2
    turns = (x / half).to_integral_value(rounding=decimal.ROUND_HALF_EVEN)
    rest = x - turns * half
    sine, cosine, term, n = D(0), D(0), D(1), 0
    while term == 0 or term.adjusted() > -decimal.getcontext().prec - 5:
        if n % 2:
            sine += term * (-1 if n % 4 == 3 else 1)
        else:
            cosine += term * (-1 if n % 4 == 2 else 1)
        n += 1
        term = term * rest / n
        if term == 0:
            break
    quadrant = int(turns) % 4
    return [(sine, cosine), (cosine, -sine), (-sine, -cosine), (-cosine, sine)][quadrant]


def d_atan(x):
    if x < 0:
        return -d_atan(-x)
    if x > 1:
        return d_pi() / 2 - d_atan(1 / x)
    halvings = 0
    while x > D("0.05"):
        x = x / (1 + (1 + x * x).sqrt())
        halvings += 1
    total, power_of_x, k = D(0), x, 0
    while power_of_x != 0 and power_of_x.adjusted() > -decimal.getcontext().prec - 5:
        total += power_of_x / (2 * k + 1) * (-1 if k % 2 else 1)
        power_of_x *= x * x
        k += 1
    return total * 2**halvings


def d_atan2(y, x):
    if x > 0:
        return d_atan(y / x)
    if x < 0:
        return d_atan(y / x) + (d_pi() if y >= 0 else -d_pi())
    return d_pi() / 2 if y > 0 else -d_pi() / 2


def second_opinion(name, arguments, expected):
    """The same vector from the decimal module, or None where it has none."""
    if expected in ("domain", "overflow"):
        return None
    decimal.getcontext().prec = 250
    values = [D(argument) for argument in arguments]
    x = values[0]
    try:
        if name == "Sqrt":
            value = x.sqrt()
        elif name == "Exp":
            value = x.exp()
        elif name == "Ln":
            value = x.ln()
        elif name == "Log10":
            value = x.log10()
        elif name == "Log":
            value = x.ln() / values[1].ln()
        elif name == "Power":
            if x <= 0 or values[1] == values[1].to_integral_value():
                return None
            value = (values[1] * x.ln()).exp()
        elif name == "Sin":
            value = d_sin_cos(x)[0]
        elif name == "Cos":
            value = d_sin_cos(x)[1]
        elif name == "Tan":
            sine, cosine = d_sin_cos(x)
            value = sine / cosine
        elif name == "Asin":
            value = d_atan2(x, (1 - x * x).sqrt())
        elif name == "Acos":
            value = d_atan2((1 - x * x).sqrt(), x)
        elif name == "Atan":
            value = d_atan(x)
        elif name == "Atan2":
            value = d_atan2(x, values[1])
        else:
            return None
    except decimal.DecimalException:
        return None
    return settled(value)


# ------------------------------------------------------- exact arithmetic
# Fractions only: sums, products and quotients rounded once, the truncated
# quotient and what is left of it, and rounding to a number of digits.

INT64 = 2**63


def whole(value):
    """A whole number as an integer of 64 bits, or the error."""
    return str(value) if -INT64 <= value < INT64 else "overflow"


def truncated(value):
    return -(-value.numerator // value.denominator) if value < 0 else value.numerator // value.denominator


def nearest(value, even):
    """A fraction rounded to a whole number: a tie away from zero, or to the even neighbour."""
    magnitude = abs(value)
    floor = magnitude.numerator // magnitude.denominator
    rest = magnitude - floor
    if rest > Fraction(1, 2) or (rest == Fraction(1, 2) and (not even or floor % 2 == 1)):
        floor += 1
    return -floor if value < 0 else floor


def arithmetic(left, right):
    x, y = fraction(left), fraction(right)
    return rounded(x + y), rounded(x - y), rounded(x * y), rounded(x / y) if y else "zero"


def remainders(left, right):
    x, y = fraction(left), fraction(right)
    if y == 0:
        return "zero", "zero"
    quotient = truncated(x / y)
    return whole(quotient), rounded(x - y * quotient)


def to_digits(value, digits):
    x, digits = fraction(value), int(digits)
    if digits >= (len(value.split(".")[1]) if "." in value else 0):
        return value, value, value
    if digits < -40:
        return "0", "0", "0"
    unit = Fraction(10) ** -digits
    return (
        rounded(nearest(x / unit, False) * unit),
        rounded(nearest(x / unit, True) * unit),
        rounded(truncated(x / unit) * unit),
    )


def integers_to_digits(value, digits):
    x, digits = Fraction(int(value)), int(digits)
    if digits >= 0:
        return value, value, value
    if digits < -40:
        return "0", "0", "0"
    unit = Fraction(10) ** -digits
    return (
        whole(int(nearest(x / unit, False) * unit)),
        whole(int(nearest(x / unit, True) * unit)),
        whole(int(truncated(x / unit) * unit)),
    )


def rounded_quotients(dividend, divisor):
    x, y = fraction(dividend), fraction(divisor)
    if y == 0:
        return "zero", "zero"
    return rounded(Fraction(nearest(x / y, False))), rounded(Fraction(nearest(x / y, True)))


def tie(places):
    """A random decimal whose last digit is a 5 at that decimal place."""
    digits = RANDOM.randint(0, 27 - max(-places, 0))
    units = RANDOM.randrange(0, 10**digits) * 10 + 5
    return spell(units * RANDOM.choice([1, -1]) * 10 ** max(-places, 0), max(places, 0))


def pairs():
    yield from [("0.1", "0.2"), ("1", "3"), ("2", "3"), ("2", "6"), ("7.5", "2"), ("-7.5", "2")]
    yield from [("7.5", "-2"), ("1", "0.3"), ("19.95", "0.05"), ("3", ABOVE_ONE), ("-7", "2")]
    yield from [("0.6666666666666666666666666667", "2"), ("1.5", TINY), ("0.0000000000000000000000000002", "3")]
    yield from [(LARGEST, LARGEST), (LARGEST, "1"), (LARGEST, negate(LARGEST)), (LARGEST, TINY)]
    yield from [(TINY, LARGEST), (TINY, TINY), ("0", "5"), ("5", "0"), ("0", "0"), (TINY, "3")]
    yield from [("1000000000000000000000000000", "0.0000000000000000000000000003")]
    yield from [("9223372036854775807", "1"), ("9223372036854775808", "1"), ("-9223372036854775808", "1")]
    yield from [("-9223372036854775809", "1"), ("9223372036854775807.5", "-1"), ("-9223372036854775808.9", "1")]
    yield from [("4611686018427387904", "0.5"), ("4611686018427387903.9", "0.5"), ("5000000000000000000000000000", "5000000000000000000000000000")]
    yield from [("0.5", "0.5"), ("2.5", "1"), ("-2.5", "1"), ("3.5", "1"), ("0.125", "0.05"), ("0.175", "0.05")]
    for _ in range(60):
        yield anything(), anything()
    for _ in range(60):
        yield between(-1000, 1000, RANDOM.randint(0, 8)), between(-50, 50, RANDOM.randint(0, 4))
    for _ in range(20):
        yield anything(least=10, most=28), anything(least=-10, most=5)
    for _ in range(30):
        divisor = between(-50, 50, RANDOM.randint(0, 3))
        half = fraction(divisor) * (2 * RANDOM.randint(-500, 500) + 1) / 2
        yield spell_fraction(half), divisor


def digit_counts():
    yield from [("2.5", "0"), ("3.5", "0"), ("-2.5", "0"), ("2.345", "2"), ("2.355", "2"), ("1234.5", "-2")]
    yield from [("2.5000001", "0"), ("2.7", "0"), ("-2.7", "0"), ("1234.567", "2"), ("1299", "-2"), ("1250", "-2")]
    yield from [("1350", "-2"), ("0", "3"), ("0", "-3"), (LARGEST, "-1"), (LARGEST, "0"), (LARGEST, "-27")]
    yield from [(LARGEST, "-28"), (LARGEST, "-29"), (LARGEST, "-30"), ("4999999999999999999999999999", "-28")]
    yield from [("5000000000000000000000000000", "-28"), ("-5000000000000000000000000000", "-28")]
    yield from [("5000000000000000000000000000", "-29"), (TINY, "27"), (TINY, "28"), (TINY, "29"), (TINY, "0")]
    yield from [("0.0000000000000000000000000005", "27"), ("0.0000000000000000000000000015", "27")]
    yield from [("-0.0000000000000000000000000005", "27"), ("0.9999999999999999999999999999", "27")]
    yield from [("0.9999999999999999999999999999", "0"), ("999999999999999999999999999.9", "0")]
    yield from [("9999999999999999999999999.995", "2"), ("0.5", "0"), ("-0.5", "0"), ("1.5", "0"), ("0.4", "0")]
    yield from [("-0.4", "0"), ("15", "-1"), ("25", "-1"), ("-15", "-1"), ("5", "-1"), ("50", "-2"), ("150", "-2")]
    yield from [("12.5", "1"), ("12.5", "5"), ("12.5", "28"), ("12.5", "9223372036854775807")]
    yield from [("12.5", "-9223372036854775808"), ("12.5", "-2"), ("62.5", "-2"), ("50", "-3"), ("500", "-3")]
    yield from [("79228162514264337593543950335", "-28"), ("79228162514264337593543950335", "-29")]
    yield from [("49999999999999999999999999999", "-29"), ("50000000000000000000000000000", "-29")]
    yield from [("79228162514264337593543950335", "-30"), ("7.9228162514264337593543950335", "27")]
    for _ in range(80):
        yield anything(), str(RANDOM.randint(-30, 30))
    for _ in range(60):
        places = RANDOM.randint(-20, 27)
        yield tie(places + 1), str(places)


def integer_digit_counts():
    largest, least = str(INT64 - 1), str(-INT64)
    yield from [("1250", "-2"), ("1350", "-2"), ("1299", "-2"), ("-1250", "-2"), ("7", "0"), ("7", "3")]
    yield from [(largest, "-3"), (largest, "-1"), (largest, "0"), (largest, "-18"), (largest, "-19")]
    yield from [(largest, "-20"), (least, "-1"), (least, "-18"), (least, "-19"), (least, "-20"), (least, "0")]
    yield from [("4999999999999999999", "-19"), ("5000000000000000000", "-19"), ("-5000000000000000000", "-19")]
    yield from [("9223372036854775800", "-2"), ("9223372036854775750", "-2"), ("9223372036854775650", "-2")]
    yield from [("5", "-1"), ("15", "-1"), ("25", "-1"), ("-5", "-1"), ("-15", "-1"), ("0", "-5"), ("1", "-1")]
    yield from [("123", "9223372036854775807"), ("123", "-9223372036854775808"), ("4", "-1"), ("6", "-1")]
    for _ in range(60):
        yield str(RANDOM.randint(-INT64, INT64 - 1) // 10 ** RANDOM.randint(0, 18)), str(RANDOM.randint(-20, 3))
    for _ in range(30):
        places = RANDOM.randint(1, 17)
        yield str((RANDOM.randint(-(10**(18 - places)), 10**(18 - places)) * 10 + 5) * 10 ** (places - 1)), str(-places)


# ----------------------------------------------------------------- vectors

PI = "3.141592653589793238462643383"
HALF_PI = "1.570796326794896619231321692"
TINY = "0.0000000000000000000000000001"
LARGEST = "9999999999999999999999999999"
ABOVE_ONE = "1.0000000000000000000000000001"
BELOW_ONE = "0.9999999999999999999999999999"


def negate(text):
    return text[1:] if text.startswith("-") else ("-" + text if fraction(text) else text)


def multiples_of_half_pi(count):
    """Decimals next to whole multiples of pi / 2, where a result is tiny or huge."""
    mp.dps = 120
    for _ in range(count):
        turns = RANDOM.choice([1, 2, 3, 4, 5, 7, 11, 100, 355, 10**6, 10**12, 10**20, 10**26])
        turns *= RANDOM.choice([1, 1, 1, RANDOM.randint(1, 99)])
        value = exact(mp.pi * turns / 2)
        whole = len(str(int(value)))
        if whole > 28:
            continue
        scale = RANDOM.randint(0, 28 - whole)
        units = int(value * 10**scale) + RANDOM.choice([0, 0, 1, -1])
        text = spell(units, scale)
        yield negate(text) if RANDOM.random() < 0.3 else text


def angles():
    # cos x = 1 - x^2/2 + x^4/24 - ... is next to a point halfway between
    # two decimals when x is an odd multiple of 10^-14.
    for odd in range(5, 61, 2):
        yield spell(odd, 14)
    yield from ["0.0000000000000100001", "0.00000000000001000000000001", "0.0000000001000000000001"]
    yield from ["0", "1", "-1", "2", "3", "0.5", PI, negate(PI), HALF_PI, negate(HALF_PI)]
    yield from ["1.5707963267948966192313216916", "1.5707963267948966192313216917"]
    yield from ["4.712388980384689857693965075", "4.7123889803846898576939650749"]
    yield from ["6.283185307179586476925286766", "0.7853981633974483096156608458"]
    yield from ["1000000000000000000000000000", LARGEST, negate(LARGEST), TINY, negate(TINY)]
    yield from ["0.00000000000001", "0.00000000000003", "0.0000000000000100000000000001"]
    yield from ["0.0000000000000000000000000002", "0.000000001", "0.0000000001817"]
    yield from ["355", "22", "100", "1000000", "0.1", "-0.1", "12345678901234567890.12345678"]
    for _ in range(150):
        yield between(-100, 100)
    for _ in range(70):
        yield anything(least=3, most=28)
    for _ in range(30):
        yield anything(least=-28, most=-3)
    yield from multiples_of_half_pi(70)


def unary(name):
    if name == "Sqrt":
        yield from ["0", "1", "2", "3", "4", "6.25", "0.25", "0.0001", TINY, LARGEST, "0.5"]
        yield from ["0.0000000000000000000000000004", "0.0000000000000000000000000002"]
        yield from ["99999999999999.99999999999999", "1000000000000000000000000000", "-1"]
        yield from [negate(TINY), "-4", negate(LARGEST), "152415787532388367501905199.9"]
        for _ in range(250):
            yield anything(signed=False)
        for _ in range(40):
            digits = RANDOM.randint(1, 14)
            scale = RANDOM.randint(0, 14)
            side = RANDOM.randrange(10 ** (digits - 1), 10**digits)
            yield spell(side * side, 2 * scale)
    elif name == "Exp":
        yield from ["0", "1", "-1", "2", "0.5", "10", "-10", "64", "65", "-100", "100", "64.4"]
        yield from ["64.47238260383327859993154561", "64.47238260383327859993154562"]
        yield from ["64.4723826038332785999315456", "64.5", "-64.5", "-65", "-66", "-67"]
        yield from ["-65.16553", "-65.16552978439322389838467182", "-65.1655297843932238983846718"]
        yield from ["-65.16552978439322389838467183", "-64.47238260383327859993154562"]
        yield from [TINY, negate(TINY), "0.0000000000000000000000000005"]
        yield from ["-0.0000000000000000000000000005", "0.0000000000000000000000000015"]
        yield from ["0.0000000000000000000000000025", "-0.0000000000000000000000000015"]
        yield from ["0.00000000000001", "-0.00000000000001", "0.00000000000003", LARGEST]
        yield from [negate(LARGEST), "66", "70", "-70", "1000", "-1000", "0.6931471805599453094172321215"]
        yield from ["2.302585092994045684017991455", "63.99999999999999999999999999"]
        # e^x = 1 + x + x^2/2 + ... is next to a point halfway between two
        # decimals when x is an odd multiple of 5 x 10^-28.
        for odd in range(3, 61, 2):
            yield spell(5 * odd, 28)
        for _ in range(200):
            yield between(-64, 64)
        for _ in range(40):
            yield anything(least=-28, most=-8)
        for _ in range(25):
            yield between(64, 66)
        for _ in range(25):
            yield between(-67, -60)
        for _ in range(10):
            yield anything(least=3, most=28)
    elif name in ("Ln", "Log10"):
        yield from ["1", "2", "10", "0.5", "0.1", "3", "100", "1000", LARGEST, TINY, "0", "-1"]
        yield from [negate(TINY), ABOVE_ONE, BELOW_ONE, "2.718281828459045235360287471"]
        yield from ["2.718281828459045235360287472", "1.5", "0.75", "1.4142135623730950488016887"]
        yield from ["0.7071067811865475244008443621", "1.414213562373095048801688725"]
        yield from ["0.7071067811865475244008443622", "1.99999999999", "0.50000000001", "4", "8"]
        for count in range(2, 12):
            yield spell(10**28 + count, 28)
            yield spell(10**28 - count, 28)
        for places in (5, 10, 14, 20, 25):
            for _ in range(5):
                yield spell(10**28 + RANDOM.randint(-(10**places), 10**places), 28)
        if name == "Log10":
            for zeros in range(-28, 28):
                yield spell_fraction(Fraction(10) ** zeros)
            for zeros in range(-27, 27, 3):
                yield spell_fraction(Fraction(10) ** zeros * (1 + Fraction(1, 10 ** (27 - abs(zeros)))))
            yield from ["9.999999999999999999999999999", "99.99999999999999999999999999"]
            yield from ["0.0999999999999999999999999999", "20", "0.02", "5", "0.2", "31.6227766"]
        for _ in range(230):
            yield anything(signed=False)
    elif name in ("Sin", "Cos", "Tan"):
        yield from angles()
    elif name in ("Asin", "Acos"):
        yield from ["0", "1", "-1", "0.5", "-0.5", TINY, negate(TINY), BELOW_ONE, negate(BELOW_ONE)]
        yield from [ABOVE_ONE, negate(ABOVE_ONE), "2", "-1.5", LARGEST, "0.7071067811865475244008443621"]
        yield from ["0.8660254037844386467637231707", "0.9999999999999999999999999998", "0.1"]
        yield from ["0.00000000000001", "-0.00000000000001", "0.99999999999999", "-0.99999999999999"]
        yield from ["0.000000001", "0.0000000001817", "0.9", "-0.9", "0.99", "0.999", "0.6", "0.8"]
        for count in range(2, 22):
            yield spell((10**28 - count * RANDOM.randint(1, 10**6)) * RANDOM.choice([1, -1]), 28)
        for _ in range(220):
            yield between(-1, 1, RANDOM.randint(1, 28))
        for _ in range(40):
            yield anything(least=-28, most=-3)
    elif name == "Atan":
        yield from ["0", "1", "-1", "0.5", "2", TINY, negate(TINY), LARGEST, negate(LARGEST)]
        yield from [ABOVE_ONE, BELOW_ONE, "1.732050807568877293527446342", "0.5773502691896257645091487805"]
        yield from ["0.4142135623730950488016887242", "2.414213562373095048801688724", "1000"]
        yield from ["0.001", "0.00000000000001", "100000000000000", "0.1989123673796580069", "0.2", "0.25"]
        for _ in range(270):
            yield anything()
        for _ in range(40):
            yield between(-3, 3)


def binary(name):
    if name == "Log":
        yield from [("8", "2"), ("8", "4"), ("2", "8"), ("4", "8"), ("1000", "10"), ("0.001", "10")]
        yield from [("0.25", "2"), ("2", "0.25"), ("0.125", "0.5"), ("1", "7"), ("7", "7")]
        yield from [("0.00001", "100"), ("32", "0.5"), ("27", "9"), ("9", "27"), ("1024", "32")]
        yield from [("0.0000000000000000000000000256", "0.02"), ("1.21", "1.1"), ("1.1", "1.21")]
        yield from [("152587890625", "5"), ("5", "152587890625"), ("0.00032", "0.2")]
        yield from [("6.25", "2.5"), ("2.5", "6.25"), ("2.5", "0.4"), ("0.4", "6.25")]
        yield from [("2", "1"), ("0", "2"), ("-1", "2"), ("2", "0"), ("2", "-2"), ("1", "1")]
        yield from [(TINY, "2"), ("2", TINY), (negate(TINY), "10"), ("1", "0"), ("0", "1")]
        yield from [(LARGEST, "10"), (TINY, "10"), ("10", LARGEST), ("10", TINY)]
        yield from [(LARGEST, TINY), (TINY, LARGEST), (LARGEST, LARGEST), (TINY, TINY)]
        yield from [("1000000000000000000000000000", ABOVE_ONE), ("2", ABOVE_ONE)]
        yield from [("2", BELOW_ONE), ("0.5", BELOW_ONE), (ABOVE_ONE, "1.0000000000000000000000000002")]
        yield from [(ABOVE_ONE, BELOW_ONE), (BELOW_ONE, ABOVE_ONE), (ABOVE_ONE, "10")]
        yield from [("1.0000000000000000000000000002", ABOVE_ONE), ("1.0000000000000000000000000003", ABOVE_ONE)]
        yield from [("1.00000000000001", "1.0000000000000000000000001"), ("1.0001", "1.000000000001")]
        yield from [("1.0000000000000000000000009999", ABOVE_ONE), ("1.000000000000000000000001", ABOVE_ONE)]
        for _ in range(170):
            yield anything(signed=False), anything(signed=False)
        for _ in range(60):
            yield between("0.01", 1000), between("0.01", 100)
        for _ in range(30):
            close = spell(10**28 + RANDOM.randint(-(10**20), 10**20), 28)
            yield anything(signed=False, least=-3, most=3), close
        for _ in range(20):
            close = spell(10**28 + RANDOM.randint(-(10**10), 10**10), 28)
            yield spell(10**28 + RANDOM.randint(-(10**10), 10**10), 28), close
    elif name == "Power":
        yield from [("1.5", "2"), ("2", "-2"), ("-2", "3"), ("0", "0"), ("7", "0"), ("-7.5", "0")]
        yield from [("2", "10"), ("6.25", "0.5"), ("8", "0.3333333333333333333333333333"), ("2", "0.5")]
        yield from [("0", "5"), ("0", "0.5"), ("0", "-1"), ("0", "-0.5"), ("0", TINY), ("0", negate(TINY))]
        yield from [("-2", "0.5"), ("-8", "0.3333333333333333333333333333"), ("-2", "-0.5")]
        yield from [("-2", "2"), ("-2", "-3"), ("-1", "3"), ("-1", "4"), ("1", LARGEST), ("-1", LARGEST)]
        yield from [("-1", "9999999999999999999999999998"), ("1", "-0.5"), ("-1", "0.5"), ("-1", "-1")]
        yield from [("10", "28"), ("10", "27"), ("10", "-28"), ("10", "-29"), ("10", "-27"), ("-10", "27")]
        yield from [("2", "93"), ("2", "94"), ("-2", "93"), ("-2", "94"), ("2", "-93"), ("2", "-94"), ("2", "-95")]
        yield from [("0.5", "29"), ("0.5", "28"), ("0.5", "30"), ("-0.5", "29"), ("2", "-29"), ("2", "-30")]
        yield from [("0.25", "14.5"), ("4", "-14.5"), ("0.2", "40"), ("5", "-40"), ("0.5", "93"), ("0.5", "-93")]
        yield from [("0.5", "-94"), ("0.5", "94"), ("0.5", "95"), ("0.5", "96"), ("0.5", "100"), ("0.5", "1000")]
        yield from [("1.5", "29"), ("1.5", "30"), ("1.5", "159"), ("1.5", "160"), ("-1.5", "159"), ("1.5", "-159")]
        yield from [("1.5", "-164"), ("2.5", "29"), ("0.4", "29"), ("0.4", "30"), ("2.5", "-29"), ("2.5", "-30")]
        yield from [("0.05", "21"), ("0.05", "22"), ("0.05", "23"), ("20", "-21"), ("20", "-22"), ("0.02", "17")]
        yield from [("4", "0.5"), ("4", "1.5"), ("4", "-0.5"), ("0.25", "-0.5"), ("0.25", "0.5"), ("2.25", "1.5")]
        yield from [("32", "0.2"), ("32", "0.6"), ("32", "-0.4"), ("0.00032", "0.2"), ("243", "0.4"), ("1024", "0.1")]
        yield from [("1024", "1.3"), ("256", "0.125"), ("256", "0.625"), ("65536", "0.0625"), ("3", "0.5")]
        yield from [("1.21", "0.5"), ("1.21", "14.5"), ("1.21", "15"), ("0.0001", "0.25"), ("0.0001", "-7.25")]
        yield from [("81", "0.25"), ("81", "0.75"), ("16", "0.75"), ("0.0016", "0.75"), ("2", "0.25")]
        yield from [("4294967296", "0.03125"), ("18446744073709551616", "0.015625"), ("2", "0.015625")]
        yield from [("1208925819614629174706176", "0.0125"), ("1208925819614629174706176", "1.1125")]
        yield from [(ABOVE_ONE, "10000000000"), (ABOVE_ONE, "5000000000000000000000000000"), (ABOVE_ONE, LARGEST)]
        yield from [(BELOW_ONE, LARGEST), (ABOVE_ONE, negate(LARGEST)), (BELOW_ONE, negate(LARGEST))]
        yield from [(ABOVE_ONE, "30"), (ABOVE_ONE, "29"), ("1.0000000000000000000000000005", "31")]
        yield from [("1.0000000000000000000000000005", "29"), ("1.0000000000000000000000000005", "1")]
        yield from [(BELOW_ONE, "0.5"), (ABOVE_ONE, "0.5"), (LARGEST, "1"), (LARGEST, "2"), (LARGEST, "0.5")]
        yield from [(LARGEST, TINY), (TINY, TINY), (TINY, "1"), (TINY, "2"), (TINY, "0.5"), (TINY, "-1")]
        yield from [(TINY, "-0.9999999999999999999999999999"), (TINY, "-1.0000000000000000000000000001")]
        yield from [(LARGEST, "-1"), (LARGEST, "-0.5"), (LARGEST, "0.9999999999999999999999999999")]
        yield from [("2.718281828459045235360287471", "64"), ("2.718281828459045235360287471", "65")]
        yield from [("2.718281828459045235360287471", "-65"), ("2.718281828459045235360287471", "-66")]
        yield from [("10", "27.99999999999999999999999999"), ("10", "-28.3"), ("10", "-28.31"), ("10", "0.5")]
        yield from [("1000000", "4.666666666666666666666666667"), ("3", "58.69"), ("3", "58.7"), ("7", "33.13")]
        yield from [("0.3", "53.5"), ("0.3", "54.5"), ("0.3", "-53.5"), ("123.456", "7.89"), ("-123.456", "7")]
        # (1 + 5 x 10^-28)^n = 1 + 5 n x 10^-28 + ... is next to a point
        # halfway between two decimals for an odd n: exact up to n = 29,
        # a range from there on.
        for odd in range(3, 100, 4):
            yield "1.0000000000000000000000000005", str(odd)
            yield "0.9999999999999999999999999995", str(odd + 2)
            yield "1.00000000000000000000000005", str(odd * 100 + 1)
        for _ in range(130):
            yield between("0.00001", 100000), between(-20, 20)
        for _ in range(80):
            base = between(-50, 50, RANDOM.randint(0, 6))
            yield base, str(RANDOM.randint(-45, 45))
        for _ in range(40):
            close = spell(10**28 + RANDOM.randint(-(10**RANDOM.randint(1, 27)), 10**RANDOM.randint(1, 27)), 28)
            yield close, anything(least=1, most=28)
        for _ in range(40):
            degree = RANDOM.choice([2, 4, 5, 8, 10, 16, 20, 25])
            top = RANDOM.randint(1, int(MAX ** (1 / degree)))
            bottom = RANDOM.choice([1, 1, 2, 5, 10, 4, 100])
            value = Fraction(top, bottom) ** degree
            if value.numerator > MAX or len(spell_fraction(value).replace(".", "")) > 28:
                continue
            times = RANDOM.randint(-3 * degree, 3 * degree)
            yield spell_fraction(value), spell_fraction(Fraction(times, degree))
        for _ in range(70):
            yield anything(signed=False), anything()
        for _ in range(20):
            yield anything(signed=False, least=-2, most=2), anything(least=-28, most=-2)
    elif name == "Atan2":
        yield from [("1", "-1"), ("0", "1"), ("0", "-1"), ("1", "0"), ("-1", "0"), ("0", "0"), ("1", "1")]
        yield from [("-1", "-1"), ("-1", "1"), (TINY, "1"), (negate(TINY), "1"), (TINY, "-1")]
        yield from [(negate(TINY), "-1"), ("1", TINY), ("1", negate(TINY)), ("-1", TINY), (TINY, TINY)]
        yield from [(TINY, LARGEST), (negate(TINY), LARGEST), (TINY, negate(LARGEST)), (LARGEST, TINY)]
        yield from [(negate(TINY), negate(LARGEST)), (LARGEST, LARGEST), (LARGEST, negate(TINY))]
        yield from [("0", LARGEST), ("0", TINY), ("0", negate(TINY)), (TINY, "0"), (negate(LARGEST), "0")]
        yield from [("3", "4"), ("4", "3"), ("-3", "4"), ("3", "-4"), ("0.5", "0.5"), ("2.5", "2.5")]
        yield from [("1", "1.732050807568877293527446342"), ("1.732050807568877293527446342", "1")]
        yield from [("0.00000000000001", "100000000000000"), ("0.000000000000005", "100000000000000")]
        yield from [("0.0000000000000000000000000005", "1"), ("0.0000000000000000000000000005", "-1")]
        for _ in range(180):
            yield anything(), anything()
        for _ in range(80):
            yield between(-10, 10), between(-10, 10)
        for _ in range(40):
            value = anything(least=-5, most=5)
            other = spell_fraction(fraction(value) + RANDOM.choice([1, -1]) * Fraction(RANDOM.randint(0, 9), 10**28))
            if len(other.replace(".", "").replace("-", "").lstrip("0")) <= 28:
                yield value, RANDOM.choice([other, negate(other)])


UNARY = {
    "Sqrt": sqrt,
    "Exp": exp,
    "Ln": ln,
    "Log10": log10,
    "Sin": sin,
    "Cos": cos,
    "Tan": tan,
    "Asin": asin,
    "Acos": acos,
    "Atan": atan,
}
BINARY = {"Log": log, "Power": power, "Atan2": atan2}
EXACT = {
    "Arithmetic": (arithmetic, pairs),
    "Remainders": (remainders, pairs),
    "RoundedQuotients": (rounded_quotients, pairs),
    "Digits": (to_digits, digit_counts),
    "IntegerDigits": (integers_to_digits, integer_digit_counts),
}
ERRORS = ("domain", "overflow", "zero")


def valid(text, limit=LIMIT):
    """Whether the text is the one spelling of a decimal with a mantissa below the limit."""
    value = fraction(text)
    digits = text.lstrip("-").replace(".", "")
    places = len(text.split(".")[1]) if "." in text else 0
    return abs(int(digits)) < limit and places <= 28 and spell(int(value * 10**places), places) == text


def rows(name, oracle, vectors, check):
    seen, result, checked = set(), [], 0
    for arguments in vectors:
        arguments = (arguments,) if isinstance(arguments, str) else tuple(arguments)
        if arguments in seen:
            continue
        seen.add(arguments)
        expected = oracle(*arguments)
        expected = (expected,) if isinstance(expected, str) else tuple(expected)
        if name not in EXACT:
            assert all(valid(argument, HELD) for argument in arguments), (name, arguments)
            assert all(cell in ERRORS or valid(cell) for cell in expected), (name, arguments, expected)
        if check and name not in EXACT:
            other = second_opinion(name, arguments, expected[0])
            if other is not None:
                checked += 1
                assert other == expected[0], (name, arguments, expected, other)
        result.append(arguments + expected)
    confirmed = "" if name in EXACT else f", {checked} confirmed by the decimal module"
    print(f"{name}: {len(result)} vectors{confirmed}", file=sys.stderr)
    return result


def write(output, name, table):
    kind = ", ".join(["string"] * len(table[0]))
    output.append("")
    output.append(f"    public static TheoryData<{kind}> {name} =>")
    output.append("        new()")
    output.append("        {")
    for row in table:
        line = "            { " + ", ".join(f'"{cell}"' for cell in row) + " },"
        if len(line) <= 90:
            output.append(line)
        else:
            output.append("            {")
            output.append(",\n".join(f'                "{cell}"' for cell in row))
            output.append("            },")
    output.append("        };")


def stress(path, count, seed):
    """Fresh random vectors as lines of tab-separated text: name, operands, expected."""
    global RANDOM
    RANDOM = random.Random(seed)
    with open(path, "w", encoding="utf-8") as file:
        for name, oracle in list(UNARY.items()) + list(BINARY.items()):
            seen = set()
            while len(seen) < count:
                for arguments in unary(name) if name in UNARY else binary(name):
                    arguments = (arguments,) if isinstance(arguments, str) else tuple(arguments)
                    if arguments not in seen:
                        seen.add(arguments)
                        file.write("\t".join((name,) + arguments + (oracle(*arguments),)) + "\n")
            print(f"{name}: {len(seen)} vectors", file=sys.stderr)


def main():
    if "--stress" in sys.argv:
        stress(sys.argv[1], int(sys.argv[3]), int(sys.argv[4]))
        return
    path = sys.argv[1]
    check = "--no-check" not in sys.argv
    output = [
        "namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;",
        "",
        "// The test vectors of PureQLMathTests: operands, then the exact value rounded",
        "// half away from zero to the decimal precision, or the execution error",
        "// (overflow, domain, or zero for a division by zero). Written by",
        "// tools/generate_math_vectors.py: the powers, logarithms and trigonometry from",
        "// mpmath at 150 digits and more, confirmed by Python's decimal module, and",
        "// the arithmetic from fractions. Do not edit by hand.",
        "internal static class PureQLMathVectors",
        "{",
    ]
    for name, oracle in UNARY.items():
        write(output, name, rows(name, oracle, unary(name), check))
    for name, oracle in BINARY.items():
        write(output, name, rows(name, oracle, binary(name), check))
    for name, (oracle, vectors) in EXACT.items():
        write(output, name, rows(name, oracle, vectors(), check))
    del output[10]
    output.append("}")
    with open(path, "w", encoding="utf-8") as file:
        file.write("\n".join(output) + "\n")


if __name__ == "__main__":
    main()
