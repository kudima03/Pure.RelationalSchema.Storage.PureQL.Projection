#!/usr/bin/env python3
"""Writes PureQLAggregatesVectorTests.cs: randomised vectors for PureQLAggregates.

The expected values come from an oracle that shares no code and no formula with
the C# implementation: every statistic is computed from its textbook definition
(differences from the mean) over fractions.Fraction, so nothing is rounded
before the result, and the result is rounded as the specification rounds a
decimal: half away from zero, to 28 significant digits and at most 28 decimal
places, with the decimal module (ROUND_HALF_UP at precision 28). A square root
is taken with the decimal module at 150 digits and the rounded candidate is
then proved against the exact fraction, so a tie cannot be misread.

    python3 generate_aggregate_vectors.py > PureQLAggregatesVectorTests.cs

The vectors depend only on the seed: the same script writes the same file. For
a longer run against other vectors, pass another seed and a number of cases per
aggregate, and put the output in place of the file for one test run:

    python3 generate_aggregate_vectors.py 7 300 > PureQLAggregatesVectorTests.cs
"""

import random
import sys
from collections import Counter
from datetime import date
from decimal import ROUND_HALF_UP, Context, Decimal
from fractions import Fraction

SEED = 20261124
CASES = 14
LIMIT = 10**28
PLACES = 28
TICKS_PER_DAY = 864_000_000_000
MAX_DAY = date.max.toordinal() - 1
INT64_MIN = -(2**63)
INT64_MAX = 2**63 - 1

if len(sys.argv) > 1:
    SEED = int(sys.argv[1])
if len(sys.argv) > 2:
    CASES = int(sys.argv[2])

rng = random.Random(SEED)


# ---------------------------------------------------------------- rounding


def spell(negative, mantissa, scale):
    """m x 10^-s as the specification spells a decimal: no trailing zeros."""
    if mantissa == 0:
        return "0"
    digits = str(mantissa).rjust(scale + 1, "0")
    whole, fraction = (digits[:-scale], digits[-scale:]) if scale else (digits, "")
    fraction = fraction.rstrip("0")
    return ("-" if negative else "") + whole + ("." + fraction if fraction else "")


def rounded(value):
    """A fraction as a decimal result: rounded once, half away from zero."""
    magnitude = abs(value)
    if magnitude < 1:
        mantissa = (2 * magnitude.numerator * 10**PLACES + magnitude.denominator) // (
            2 * magnitude.denominator
        )
        return spell(value < 0, mantissa, PLACES)
    context = Context(prec=PLACES, rounding=ROUND_HALF_UP)
    quotient = context.divide(Decimal(magnitude.numerator), Decimal(magnitude.denominator))
    if quotient >= LIMIT:
        return "overflow"
    _, digits, exponent = quotient.as_tuple()
    mantissa = int("".join(map(str, digits)))
    if exponent > 0:
        mantissa, exponent = mantissa * 10**exponent, 0
    return spell(value < 0, mantissa, -exponent)


def root(value):
    """The square root of a fraction that is not negative, rounded once."""
    if value == 0:
        return "0"
    context = Context(prec=150)
    approximate = context.sqrt(
        context.divide(Decimal(value.numerator), Decimal(value.denominator))
    )
    whole = int(approximate)
    scale = PLACES if whole == 0 else PLACES - len(str(whole))
    while scale >= 0:
        mantissa = int(approximate.scaleb(scale).to_integral_value(ROUND_HALF_UP))
        # Prove the candidate: m - 1/2 <= sqrt(v) x 10^s < m + 1/2.
        scaled = value * 10 ** (2 * scale)
        while mantissa > 0 and not Fraction(2 * mantissa - 1, 2) ** 2 <= scaled:
            mantissa -= 1
        while not scaled < Fraction(2 * mantissa + 1, 2) ** 2:
            mantissa += 1
        if mantissa < LIMIT:
            return spell(False, mantissa, scale)
        scale -= 1
    return "overflow"


# ------------------------------------------------------------------ oracle


def present(values):
    return [Fraction(value) for value in values if value is not None]


def counted(pairs):
    return [
        (Fraction(first), Fraction(second))
        for first, second in pairs
        if first is not None and second is not None
    ]


def mean(numbers):
    return sum(numbers, Fraction(0)) / len(numbers)


def squares(numbers):
    centre = mean(numbers)
    return sum(((number - centre) ** 2 for number in numbers), Fraction(0))


def products(pairs):
    first = mean([pair[0] for pair in pairs])
    second = mean([pair[1] for pair in pairs])
    return sum(((a - first) * (b - second) for a, b in pairs), Fraction(0))


def interpolated(numbers, fraction):
    ordered = sorted(numbers)
    position = Fraction(fraction) * (len(ordered) - 1)
    lower = position.numerator // position.denominator
    share = position - lower
    if share == 0:
        return ordered[lower]
    return ordered[lower] + share * (ordered[lower + 1] - ordered[lower])


def ranked(numbers, fraction):
    ordered = sorted(numbers)
    number = -((-Fraction(fraction) * len(ordered)) // 1)
    return ordered[max(int(number), 1) - 1]


def most_frequent(numbers):
    counts = Counter(numbers)
    most = max(counts.values())
    return min(number for number, count in counts.items() if count == most)


def sum_of(values):
    return rounded(sum(present(values), Fraction(0)))


def average(values):
    numbers = present(values)
    return rounded(mean(numbers)) if numbers else "null"


def percentile_continuous(values, fraction):
    numbers = present(values)
    return rounded(interpolated(numbers, fraction)) if numbers else "null"


def median(values):
    return percentile_continuous(values, "0.5")


def percentile_discrete(values, fraction):
    numbers = present(values)
    return rounded(ranked(numbers, fraction)) if numbers else "null"


def mode(values):
    numbers = present(values)
    return rounded(most_frequent(numbers)) if numbers else "null"


def variance_population(values):
    numbers = present(values)
    return rounded(squares(numbers) / len(numbers)) if numbers else "null"


def variance_sample(values):
    numbers = present(values)
    return rounded(squares(numbers) / (len(numbers) - 1)) if len(numbers) > 1 else "null"


def deviation_population(values):
    numbers = present(values)
    return root(squares(numbers) / len(numbers)) if numbers else "null"


def deviation_sample(values):
    numbers = present(values)
    return root(squares(numbers) / (len(numbers) - 1)) if len(numbers) > 1 else "null"


def covariance_population(pairs):
    rows = counted(pairs)
    return rounded(products(rows) / len(rows)) if rows else "null"


def covariance_sample(pairs):
    rows = counted(pairs)
    return rounded(products(rows) / (len(rows) - 1)) if len(rows) > 1 else "null"


def spreads(rows):
    return squares([row[0] for row in rows]), squares([row[1] for row in rows])


def correlation(pairs):
    rows = counted(pairs)
    if not rows:
        return "null"
    first, second = spreads(rows)
    if first == 0 or second == 0:
        return "null"
    product = products(rows)
    magnitude = root(product**2 / (first * second))
    return "-" + magnitude if product < 0 and magnitude != "0" else magnitude


# A regression pair is (y, x).
def regression_slope(pairs):
    rows = counted(pairs)
    if not rows or spreads(rows)[1] == 0:
        return "null"
    return rounded(products(rows) / spreads(rows)[1])


def regression_intercept(pairs):
    rows = counted(pairs)
    if not rows or spreads(rows)[1] == 0:
        return "null"
    slope = products(rows) / spreads(rows)[1]
    return rounded(mean([row[0] for row in rows]) - slope * mean([row[1] for row in rows]))


def regression_r_squared(pairs):
    rows = counted(pairs)
    if not rows or spreads(rows)[1] == 0:
        return "null"
    explained, spread = spreads(rows)
    if explained == 0:
        return "1"
    return rounded(products(rows) ** 2 / (spread * explained))


def integer_sum(values):
    total = sum(value for value in values if value is not None)
    return str(total) if INT64_MIN <= total <= INT64_MAX else "overflow"


def nearest(value, unit):
    """The nearest multiple of the unit; one exactly halfway is the later."""
    shifted = value / unit + Fraction(1, 2)
    return (shifted.numerator // shifted.denominator) * unit


def unit_mean(values, unit):
    numbers = present(values)
    return nearest(mean(numbers), unit) if numbers else None


def unit_percentile(values, fraction, unit):
    numbers = present(values)
    return nearest(interpolated(numbers, fraction), unit) if numbers else None


# ------------------------------------------------------------------ values


def text(mantissa, scale):
    """m x 10^-s with every one of its s decimal places, zeros included."""
    digits = str(abs(mantissa)).rjust(scale + 1, "0")
    sign = "-" if mantissa < 0 else ""
    return sign + (digits[:-scale] + "." + digits[-scale:] if scale else digits)


def small():
    return str(rng.randint(-100, 100))


def money():
    return text(rng.randint(-(10**7), 10**7), 2)


def wide():
    digits = rng.randint(1, 28)
    return text(rng.randint(-(10**digits) + 1, 10**digits - 1), rng.randint(0, 28))


def huge():
    return text(rng.choice([-1, 1]) * rng.randint(10**26, LIMIT - 1), 0)


def tiny():
    return text(rng.randint(-50, 50), 28)


def near():
    return text(NEAR[0] + rng.randint(-3, 3), 28)


def repeated():
    return rng.choice(["1", "1.0", "1.00", "2", "2.50", "2.5", "-3", "-3.000", "0", "0.0"])


NEAR = [0]
PROFILES = [small, money, wide, huge, tiny, near, repeated]


def column(index):
    """A column of one kind of numbers, some of them null; now and then empty."""
    profile = PROFILES[index % len(PROFILES)]
    NEAR[0] = rng.randint(10**20, 10**27)
    count = rng.choice([0, 1, 1, 2, 2, 3, 4, 5, 6, 8]) if index >= len(PROFILES) else 5
    return [None if rng.random() < 0.15 else profile() for _ in range(count)]


def pairs(index):
    count = rng.choice([0, 1, 2, 2, 3, 4, 5, 7]) if index >= len(PROFILES) else 5
    first = PROFILES[index % len(PROFILES)]
    second = PROFILES[(index * 3 + 1) % len(PROFILES)]
    NEAR[0] = rng.randint(10**20, 10**27)
    rows = [
        (
            None if rng.random() < 0.12 else first(),
            None if rng.random() < 0.12 else second(),
        )
        for _ in range(count)
    ]
    kind = rng.random()
    if kind < 0.15:
        constant = second()
        rows = [(a, None if b is None else constant) for a, b in rows]
    elif kind < 0.3:
        constant = first()
        rows = [(None if a is None else constant, b) for a, b in rows]
    elif kind < 0.4:
        rows = [(b, b) for _, b in rows]
    return rows


def fraction():
    if rng.random() < 0.25:
        return text(rng.randint(0, LIMIT), 28)
    return rng.choice(["0", "1", "0.5", "0.9", "0.25", "0.1", "0.99", "1.0", "0.50", "0.75"])


def integers(index):
    count = rng.choice([0, 1, 2, 3, 5, 8])
    if index % 2 == 0:
        return [None if rng.random() < 0.2 else rng.randint(-1000, 1000) for _ in range(count)]
    edges = [INT64_MAX, INT64_MIN, INT64_MAX - 7, INT64_MIN + 7, 1, -1, 2**62, -(2**62)]
    return [None if rng.random() < 0.15 else rng.choice(edges) for _ in range(count)]


def days(index):
    count = rng.choice([0, 1, 2, 2, 3, 4, 6])
    start = rng.choice([0, 719_162 - 3, 738_000, MAX_DAY - 400, rng.randint(0, MAX_DAY - 400)])
    width = rng.choice([1, 3, 30, 400])
    return [None if rng.random() < 0.15 else start + rng.randint(0, width) for _ in range(count)]


def day_ticks(index):
    """Times of day in ticks; every third column carries 100 ns ticks."""
    count = rng.choice([0, 1, 2, 2, 3, 4, 6])
    step = 1 if index % 3 == 2 else 10
    start = rng.choice([0, 12 * 36_000_000_000, TICKS_PER_DAY - 40_000, rng.randint(0, TICKS_PER_DAY - 40_000)])
    width = rng.choice([30, 3_000, 30_000])
    return [
        None if rng.random() < 0.15 else start + rng.randint(0, width // step) * step
        for _ in range(count)
    ]


def instant_ticks(index):
    count = rng.choice([0, 1, 2, 2, 3, 4, 6])
    step = 1 if index % 3 == 2 else 10
    span = (MAX_DAY - 4) * TICKS_PER_DAY
    start = rng.choice([0, 621_355_968_000_000_000 - 50, rng.randint(0, span), rng.randint(0, span)])
    width = rng.choice([40, 5_000, TICKS_PER_DAY * 3])
    return [
        None if rng.random() < 0.15 else start + rng.randint(0, width // step) * step
        for _ in range(count)
    ]


# ----------------------------------------------------------------- spelling


def day_text(day):
    return date.fromordinal(day + 1).isoformat()


def time_text(ticks):
    seconds, part = divmod(ticks, 10_000_000)
    return f"{seconds // 3600:02}:{seconds // 60 % 60:02}:{seconds % 60:02}.{part:07}"


def instant_text(ticks):
    day, rest = divmod(ticks, TICKS_PER_DAY)
    return f"{day_text(day)}T{time_text(rest)}"


def listed(values, spelling=str):
    return " ".join("null" if value is None else spelling(value) for value in values)


def paired(rows):
    return " ".join(
        ("null" if a is None else a) + ":" + ("null" if b is None else b) for a, b in rows
    )


def result(value, spelling, suffix=""):
    return "null" if value is None else spelling(value) + suffix


# ----------------------------------------------------------------- emitting


def literal(value):
    """A C# string literal, broken at spaces so that no line passes 90 columns."""
    parts, line = [], ""
    for token in value.split(" "):
        if line and len(line) + len(token) > 66:
            parts.append(line)
            line = ""
        line += token + " "
    parts.append(line[:-1])
    return [f'"{parts[0]}"'] + [f'    + "{part}"' for part in parts[1:]]


def attribute(row):
    arguments = [literal(argument) for argument in row]
    short = "    [InlineData(" + ", ".join(lines[0] for lines in arguments) + ")]"
    if len(short) <= 90 and all(len(lines) == 1 for lines in arguments):
        return short
    spread = ",\n".join("\n".join("        " + line for line in lines) for lines in arguments)
    return f"    [InlineData(\n{spread}\n    )]"


def theory(name, parameters, body, rows):
    lines = ["    [Theory]"]
    lines.extend(attribute(row) for row in dict.fromkeys(rows))
    signature = f"    public void {name}(" + ", ".join(f"string {p}" for p in parameters) + ")"
    if len(signature) > 90:
        spread = ",\n".join(f"        string {parameter}" for parameter in parameters)
        signature = f"    public void {name}(\n{spread}\n    )"
    lines.append(signature)
    lines.append("    {")
    lines.extend("        " + line if line else "" for line in body)
    lines.append("    }")
    return "\n".join(lines)


def decimal_theory(name, method, oracle):
    rows = []
    for index in range(CASES):
        values = column(index)
        rows.append((listed(values), oracle(values)))
    body = [
        "decimal?[] column = AggregateVectors.Decimals(values);",
        "string actual = AggregateVectors.Text(",
        f"    () => PureQLAggregates.{method}(column)",
        ");",
        "",
        "Assert.Equal(expected, actual);",
    ]
    return theory(name, ["values", "expected"], body, rows)


def fraction_theory(name, method, oracle):
    rows = []
    for index in range(CASES + 6):
        values, share = column(index), fraction()
        rows.append((listed(values), share, oracle(values, share)))
    body = [
        "decimal?[] column = AggregateVectors.Decimals(values);",
        "decimal share = AggregateVectors.Decimal(fraction);",
        "string actual = AggregateVectors.Text(",
        f"    () => PureQLAggregates.{method}(column, share)",
        ");",
        "",
        "Assert.Equal(expected, actual);",
    ]
    return theory(name, ["values", "fraction", "expected"], body, rows)


def pair_theory(name, method, oracle):
    rows = []
    for index in range(CASES + 4):
        values = pairs(index)
        rows.append((paired(values), oracle(values)))
    body = [
        "(decimal?, decimal?)[] rows = AggregateVectors.Pairs(pairs);",
        "string actual = AggregateVectors.Text(",
        f"    () => PureQLAggregates.{method}(rows)",
        ");",
        "",
        "Assert.Equal(expected, actual);",
    ]
    return theory(name, ["pairs", "expected"], body, rows)


def integer_theory():
    rows = []
    for index in range(CASES):
        values = integers(index)
        rows.append((listed(values), integer_sum(values)))
    body = [
        "long?[] column = AggregateVectors.Integers(values);",
        "string actual = AggregateVectors.Text(() => PureQLAggregates.Sum(column));",
        "",
        "Assert.Equal(expected, actual);",
    ]
    return theory("SumOfIntegersIsExactWhateverThePartialSums", ["values", "expected"], body, rows)


def temporal_theories(noun, kind, source, spelling, unit, suffix):
    means, medians, percentiles = [], [], []
    for index in range(CASES):
        values = source(index)
        means.append((listed(values, spelling), result(unit_mean(values, unit), spelling, suffix)))
        values = source(index)
        medians.append(
            (listed(values, spelling), result(unit_percentile(values, "0.5", unit), spelling, suffix))
        )
        values, share = source(index), fraction()
        percentiles.append(
            (
                listed(values, spelling),
                share,
                result(unit_percentile(values, share, unit), spelling, suffix),
            )
        )

    def body(call):
        return [
            f"{kind}?[] column = AggregateVectors.{noun}(values);",
            f"{kind}? actual = PureQLAggregates.{call};",
            "",
            "Assert.Equal(expected, AggregateVectors.Text(actual));",
        ]

    return [
        theory(
            f"AverageOf{noun}RoundsHalfToTheLater",
            ["values", "expected"],
            body("Average(column)"),
            means,
        ),
        theory(
            f"MedianOf{noun}RoundsHalfToTheLater",
            ["values", "expected"],
            body("Median(column)"),
            medians,
        ),
        theory(
            f"PercentileContinuousOf{noun}RoundsHalfToTheLater",
            ["values", "fraction", "expected"],
            [
                f"{kind}?[] column = AggregateVectors.{noun}(values);",
                "decimal share = AggregateVectors.Decimal(fraction);",
                f"{kind}? actual = PureQLAggregates.PercentileContinuous(column, share);",
                "",
                "Assert.Equal(expected, AggregateVectors.Text(actual));",
            ],
            percentiles,
        ),
    ]


def main():
    theories = [
        integer_theory(),
        decimal_theory("SumOfDecimalsIsExact", "Sum", sum_of),
        decimal_theory("AverageIsTheExactMeanRoundedOnce", "Average", average),
        decimal_theory("MedianIsTheMiddleOrTheMeanOfTheTwoMiddleValues", "Median", median),
        fraction_theory(
            "PercentileContinuousInterpolatesExactly",
            "PercentileContinuous",
            percentile_continuous,
        ),
        fraction_theory(
            "PercentileDiscreteIsTheValueAtTheCeilingOfTheRank",
            "PercentileDiscrete",
            percentile_discrete,
        ),
        decimal_theory("ModeIsTheMostFrequentValueAndTheLeastOfSeveral", "Mode", mode),
        decimal_theory(
            "VariancePopulationIsOneDivisionOfExactSums",
            "VariancePopulation",
            variance_population,
        ),
        decimal_theory(
            "VarianceSampleIsOneDivisionOfExactSums", "VarianceSample", variance_sample
        ),
        decimal_theory(
            "StandardDeviationPopulationIsTheRootOfTheExactVariance",
            "StandardDeviationPopulation",
            deviation_population,
        ),
        decimal_theory(
            "StandardDeviationSampleIsTheRootOfTheExactVariance",
            "StandardDeviationSample",
            deviation_sample,
        ),
        pair_theory(
            "CovariancePopulationCountsThePairsWithBothValues",
            "CovariancePopulation",
            covariance_population,
        ),
        pair_theory(
            "CovarianceSampleCountsThePairsWithBothValues",
            "CovarianceSample",
            covariance_sample,
        ),
        pair_theory("CorrelationIsOneExactRatioUnderOneRoot", "Correlation", correlation),
        pair_theory("RegressionSlopeIsOneDivision", "RegressionSlope", regression_slope),
        pair_theory(
            "RegressionInterceptIsNotComputedFromTheRoundedSlope",
            "RegressionIntercept",
            regression_intercept,
        ),
        pair_theory(
            "RegressionRSquaredIsOneDivision", "RegressionRSquared", regression_r_squared
        ),
        *temporal_theories("Dates", "DateOnly", days, day_text, 1, ""),
        *temporal_theories("Times", "TimeOnly", day_ticks, time_text, 10, ""),
        *temporal_theories("Instants", "DateTime", instant_ticks, instant_text, 10, "Z"),
    ]
    header = [
        "namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Functions;",
        "",
        "// Randomised vectors for PureQLAggregates. This file is written by",
        "// tools/generate_aggregate_vectors.py and is not edited by hand: the",
        "// expected values are computed there from exact fractions, by the",
        "// definition of each aggregate, and rounded once as the specification",
        "// rounds. A column is a list of values separated by spaces, a pair is",
        "// first:second, and a sum outside the range of its type is \"overflow\".",
        "public sealed class PureQLAggregatesVectorTests",
        "{",
    ]
    sys.stdout.write("\n".join(header) + "\n" + "\n\n".join(theories) + "\n}\n")


if __name__ == "__main__":
    main()
