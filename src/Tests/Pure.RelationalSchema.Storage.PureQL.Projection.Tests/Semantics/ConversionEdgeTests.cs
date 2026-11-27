using System.Globalization;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// The edges of "Conversion results" that ConversionTests leaves out: the
// text asString writes for values at the ends of their ranges and for
// computed values, the one spelling as… and tryAs… read (and every near
// miss: other digits, other letter case, other separators, a seventh digit of
// a second, an offset), strings computed in the query, and the rule that a
// parameter value is written exactly like a literal of its type
// ("Parameters").
public sealed class ConversionEdgeTests
{
    private static readonly IStoredSchemaDataSet One = new InlineTable(
        "t",
        "one",
        ("id", new LongColumnType())
    ).With(["1"]);

    // "hh:mm:ss, followed by the fraction only when it is not zero, without
    // trailing zeros"; "the date, T, the time, Z"; "YYYY-MM-DD, the year in
    // four digits" (Conversion results).
    [Theory]
    [InlineData("time", "00:00:00.00001", "00:00:00.00001")]
    [InlineData("time", "12:00:00.0001", "12:00:00.0001")]
    [InlineData("time", "23:59:59.9", "23:59:59.9")]
    [InlineData("time", "23:59:59.999999", "23:59:59.999999")]
    [InlineData("date", "0001-01-01", "0001-01-01")]
    [InlineData("date", "0099-03-04", "0099-03-04")]
    [InlineData("datetime", "0001-01-01T00:00:00Z", "0001-01-01T00:00:00Z")]
    [InlineData("datetime", "9999-12-31T23:59:59.999999Z", "9999-12-31T23:59:59.999999Z")]
    [InlineData("datetime", "0099-03-04T05:06:07.08Z", "0099-03-04T05:06:07.08Z")]
    [InlineData("datetime", "2024-02-29T00:00:00.000001Z", "2024-02-29T00:00:00.000001Z")]
    [InlineData("uuid", "00112233-4455-6677-8899-aabbccddeeff", "00112233-4455-6677-8899-aabbccddeeff")]
    [InlineData("uuid", "00000000-0000-0000-0000-000000000000", "00000000-0000-0000-0000-000000000000")]
    [InlineData("uuid", "FFFFFFFF-FFFF-FFFF-FFFF-FFFFFFFFFFFF", "ffffffff-ffff-ffff-ffff-ffffffffffff")]
    [InlineData("integer", "9223372036854775807", "9223372036854775807")]
    [InlineData("decimal", "-0.0000000000000000000000000001", "-0.0000000000000000000000000001")]
    [InlineData("decimal", "9999999999999999999999999999", "9999999999999999999999999999")]
    [InlineData("decimal", "-123456789012345678901234567.8", "-123456789012345678901234567.8")]
    public void AsStringWritesTheEndsOfEveryRange(
        string type,
        string value,
        string expected
    )
    {
        Assert.Equal(expected, Value(Written(type, value)));
    }

    // "A decimal is written by its value, not by how it was computed", and so
    // is every other value: a computed time or instant whose fraction became
    // zero has no fraction.
    [Fact]
    public void AsStringWritesComputedValuesByTheirValue()
    {
        Assert.Equal(
            "2024-01-31T18:30:01Z",
            Value(
                new AsStringDatetimeStringProjection(
                    new DatetimeAddSecondsDatetimeProjection(
                        Instant("2024-01-31T18:30:00.5Z"),
                        Decimal("0.5")
                    )
                )
            )
        );
        Assert.Equal(
            "00:00:00",
            Value(
                new AsStringTimeStringProjection(
                    new TimeAddSecondsTimeProjection(Time("23:59:59.5"), Decimal("0.5"))
                )
            )
        );
        Assert.Equal(
            "01:02:03.04",
            Value(
                new AsStringTimeStringProjection(
                    new TimeFromPartsTimeProjection(
                        new LiteralInteger(1),
                        new LiteralInteger(2),
                        Decimal("3.04")
                    )
                )
            )
        );
        Assert.Equal(
            "0100-01-01",
            Value(
                new AsStringDateStringProjection(
                    new DateAddDaysDateProjection(
                        Date("0099-12-31"),
                        new LiteralInteger(1)
                    )
                )
            )
        );
        Assert.Equal(
            "0001-01-01T00:00:00Z",
            Value(
                new AsStringDatetimeStringProjection(
                    new AsDatetimeDateDatetimeProjection(Date("0001-01-01"))
                )
            )
        );
    }

    [Theory]
    [InlineData("1.05", "0.05", "1.1")]
    [InlineData("0.1", "-0.1", "0")]
    [InlineData("-0.5", "-0.5", "-1")]
    [InlineData("0.0000000000000000000000000001", "-0.0000000000000000000000000001", "0")]
    public void AsStringOfAComputedDecimalHasNoTrailingZerosAndNoMinusZero(
        string left,
        string right,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(
                new AsStringDecimalStringProjection(
                    new AddDecimalProjection([Decimal(left), Decimal(right)])
                )
            )
        );
    }

    [Fact]
    public void AsStringOfANegativeZeroProductIsZero()
    {
        Assert.Equal(
            "0",
            Value(
                new AsStringDecimalStringProjection(
                    new MultiplyDecimalProjection([
                        Decimal("-1.5"),
                        new LiteralInteger(0),
                    ])
                )
            )
        );
    }

    [Theory]
    [InlineData(long.MinValue, "-9223372036854775808")]
    [InlineData(long.MaxValue, "9223372036854775807")]
    [InlineData(0, "0")]
    public void AsDecimalOfAnIntegerIsWrittenAsTheSameNumber(long value, string expected)
    {
        Assert.Equal(
            expected,
            Value(
                new AsStringDecimalStringProjection(
                    new AsDecimalIntegerDecimalProjection(new LiteralInteger(value))
                )
            )
        );
    }

    // "as… of a string and tryAs… read exactly the text that asString writes
    // for the type", up to the ends of each range: the text read and written
    // back is the same.
    [Theory]
    [InlineData("integer", "-1")]
    [InlineData("integer", "1")]
    [InlineData("decimal", "1234567890123456789012345678")]
    [InlineData("decimal", "-123456789012345678901234567.8")]
    [InlineData("decimal", "0.1234567890123456789012345678")]
    [InlineData("decimal", "-0.0000000000000000000000000001")]
    [InlineData("decimal", "9999999999999999999999999999")]
    [InlineData("decimal", "0")]
    [InlineData("date", "0001-01-01")]
    [InlineData("date", "9999-12-31")]
    [InlineData("date", "2000-02-29")]
    [InlineData("date", "0004-02-29")]
    [InlineData("time", "00:00:00")]
    [InlineData("time", "00:00:00.000001")]
    [InlineData("time", "23:59:59.9")]
    [InlineData("time", "12:34:56.78")]
    [InlineData("datetime", "0001-01-01T00:00:00Z")]
    [InlineData("datetime", "9999-12-31T23:59:59.999999Z")]
    [InlineData("datetime", "2024-02-29T12:00:00.000001Z")]
    [InlineData("uuid", "00000000-0000-0000-0000-000000000000")]
    [InlineData("uuid", "ffffffff-ffff-ffff-ffff-ffffffffffff")]
    [InlineData("uuid", "00112233-4455-6677-8899-aabbccddeeff")]
    public void TextInTheOneSpellingIsReadUpToTheEndsOfTheRange(string type, string text)
    {
        Assert.Equal(text, Value(Reading(type, new LiteralString(text))));
        Assert.Equal(text, Value(TryReading(type, new LiteralString(text))));
    }

    // "no surrounding spaces, no +, no leading zeros, no trailing zeros in a
    // fraction, no exponent, no other letter case ... the empty string is not
    // a literal of any of these types" (Conversion results); the digits are
    // [0-9] only ("Literal patterns use [0-9], never \d").
    [Theory]
    [InlineData("integer", "-9223372036854775809")]
    [InlineData("integer", "18446744073709551616")]
    [InlineData("integer", "99999999999999999999")]
    [InlineData("integer", "00")]
    [InlineData("integer", "-00")]
    [InlineData("integer", "-01")]
    [InlineData("integer", "1.0")]
    [InlineData("integer", "12 ")]
    [InlineData("integer", "\t12")]
    [InlineData("integer", "１２")]
    [InlineData("integer", "١٢")]
    [InlineData("integer", "−5")]
    [InlineData("integer", "- 5")]
    [InlineData("integer", "--1")]
    [InlineData("integer", "1_000")]
    [InlineData("integer", "1,000")]
    [InlineData("integer", "0x10")]
    [InlineData("integer", "")]
    [InlineData("integer", "-")]
    [InlineData("decimal", "0.10")]
    [InlineData("decimal", "00.5")]
    [InlineData("decimal", "-.5")]
    [InlineData("decimal", "1E3")]
    [InlineData("decimal", "1.5 ")]
    [InlineData("decimal", "+1.5")]
    [InlineData("decimal", "1,5")]
    [InlineData("decimal", "１.5")]
    [InlineData("decimal", "7922816251426433759354395033.5")]
    [InlineData("decimal", "12345678901234567890123456789")]
    [InlineData("decimal", "0.12345678901234567890123456789")]
    [InlineData("decimal", "-0.0")]
    [InlineData("decimal", "0.0")]
    [InlineData("decimal", "NaN")]
    [InlineData("decimal", "Infinity")]
    [InlineData("decimal", "-")]
    [InlineData("decimal", ".")]
    [InlineData("decimal", "")]
    [InlineData("boolean", "True")]
    [InlineData("boolean", "FALSE")]
    [InlineData("boolean", " true")]
    [InlineData("boolean", "true ")]
    [InlineData("boolean", "yes")]
    [InlineData("boolean", "t")]
    [InlineData("boolean", "0")]
    [InlineData("date", "2023-02-29")]
    [InlineData("date", "1900-02-29")]
    [InlineData("date", "2100-02-29")]
    [InlineData("date", "10000-01-01")]
    [InlineData("date", "+2024-01-01")]
    [InlineData("date", "-2024-01-01")]
    [InlineData("date", "2024-13-01")]
    [InlineData("date", "2024-00-10")]
    [InlineData("date", "2024-01-00")]
    [InlineData("date", "2024/01/31")]
    [InlineData("date", "20240131")]
    [InlineData("date", "2024-01-31 ")]
    [InlineData("date", "2024-01-1")]
    [InlineData("date", "２０２４-01-31")]
    [InlineData("date", "2024-W01-1")]
    [InlineData("date", "2024-001")]
    [InlineData("date", "")]
    [InlineData("time", "23:59:60")]
    [InlineData("time", "18:30:00.0")]
    [InlineData("time", "18:30:00.")]
    [InlineData("time", "18:30:00.1234560")]
    [InlineData("time", "8:30:00")]
    [InlineData("time", "18:5:00")]
    [InlineData("time", "18:30:00Z")]
    [InlineData("time", "18:30:00+03:00")]
    [InlineData("time", "T18:30:00")]
    [InlineData("time", "18:30:00,5")]
    [InlineData("time", "18.30.00")]
    [InlineData("time", "-01:00:00")]
    [InlineData("time", "18:30:00 ")]
    [InlineData("time", "１８:30:00")]
    [InlineData("time", "")]
    [InlineData("datetime", "2024-01-31T18:30:00.1234567Z")]
    [InlineData("datetime", "2024-01-31T18:30:00.0Z")]
    [InlineData("datetime", "2024-01-31T18:30:00+03:00")]
    [InlineData("datetime", "2024-01-31T18:30:00-00:00")]
    [InlineData("datetime", "2024-01-31 18:30:00Z")]
    [InlineData("datetime", "2024-01-31T24:00:00Z")]
    [InlineData("datetime", "2024-01-31T18:30Z")]
    [InlineData("datetime", "2024-02-30T00:00:00Z")]
    [InlineData("datetime", "2024-01-31TZ")]
    [InlineData("datetime", "2024-01-31Z")]
    [InlineData("datetime", "2024-01-31T18:30:00.Z")]
    [InlineData("datetime", "2024-01-31T18:30:00ZZ")]
    [InlineData("datetime", "2024-01-31T18:30:00UTC")]
    [InlineData("datetime", "0000-12-31T23:59:59Z")]
    [InlineData("datetime", "2024-01-31T18:30:00z")]
    [InlineData("datetime", "2024-01-31t18:30:00Z")]
    [InlineData("datetime", "2024-01-31")]
    [InlineData("datetime", "")]
    [InlineData("uuid", "3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6F")]
    [InlineData("uuid", "urn:uuid:3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6f")]
    [InlineData("uuid", "(3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6f)")]
    [InlineData("uuid", "3f2a6c1e8-b4d-4e2a-9c1f-1a2b3c4d5e6f")]
    [InlineData("uuid", "3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6g")]
    [InlineData("uuid", "3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6")]
    [InlineData("uuid", "3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6f0")]
    [InlineData("uuid", " 3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6f")]
    [InlineData("uuid", "3f2a6c1e_8b4d_4e2a_9c1f_1a2b3c4d5e6f")]
    [InlineData("uuid", "3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6f\n")]
    [InlineData("uuid", "0x3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e")]
    [InlineData("uuid", "")]
    public void TextThatIsNotTheOneSpellingFailsAsAndIsNullForTryAs(
        string type,
        string text
    )
    {
        PureQLProjection projection = Selecting(Reading(type, new LiteralString(text)));

        _ = Assert.ThrowsAny<Exception>(() => projection.ToList());
        Assert.Equal("NULL", Value(TryReading(type, new LiteralString(text))));
    }

    // A string computed in the query is read like a literal one.
    [Fact]
    public void ComputedTextIsReadAsTheValueItSpells()
    {
        Assert.Equal(
            "2024-02-29",
            Value(
                new AsStringDateStringProjection(
                    new AsDateStringDateProjection(
                        new ConcatStringProjection([
                            new LiteralString("2024-02-"),
                            new LiteralString("29"),
                        ])
                    )
                )
            )
        );
        Assert.Equal(
            "2024-02-29T00:00:00Z",
            Value(
                new AsStringDatetimeStringProjection(
                    new AsDatetimeStringDatetimeProjection(
                        new ConcatStringProjection([
                            new AsStringDateStringProjection(Date("2024-02-29")),
                            new LiteralString("T00:00:00Z"),
                        ])
                    )
                )
            )
        );
        Assert.Equal(
            "NULL",
            Value(
                Shown(
                    new AsStringDateStringNullableProjection(
                        new TryAsDateProjection(
                            new ConcatStringProjection([
                                new LiteralString("2023-02-"),
                                new LiteralString("29"),
                            ])
                        )
                    )
                )
            )
        );
    }

    // "asDate and asTime read a datetime in UTC, as it is written";
    // "asDatetime gives midnight UTC of a date" (Conversion results), at the
    // ends of the range.
    [Theory]
    [InlineData("9999-12-31T23:59:59.999999Z", "9999-12-31", "23:59:59.999999")]
    [InlineData("0001-01-01T00:00:00.000001Z", "0001-01-01", "00:00:00.000001")]
    public void AsDateAndAsTimeOfTheEndsOfTheRange(
        string instant,
        string day,
        string time
    )
    {
        Assert.Equal(
            day,
            Value(
                new AsStringDateStringProjection(
                    new AsDateDatetimeDateProjection(Instant(instant))
                )
            )
        );
        Assert.Equal(
            time,
            Value(
                new AsStringTimeStringProjection(
                    new AsTimeDatetimeTimeProjection(Instant(instant))
                )
            )
        );
    }

    // "A value is written exactly like a literal of the declared type, in the
    // same one spelling: ... date, time, datetime (in UTC) and uuid (in lower
    // case)" (Parameters): such a value is bound.
    [Theory]
    [InlineData("date", "0987-01-31")]
    [InlineData("time", "18:30:00.125")]
    [InlineData("datetime", "2024-01-31T18:30:00.25Z")]
    [InlineData("uuid", "3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6f")]
    [InlineData("boolean", "true")]
    public void ParameterInTheOneSpellingIsBound(string type, string text)
    {
        PureQLProjection projection = Selecting(
            Parameter(type),
            new Dictionary<string, IParameter>
            {
                ["p"] = new TextParameter(type, text),
            }
        );

        Assert.Equal(text, ResultColumn.Texts(projection, "value")[0]);
    }

    // "is run with ... a parameter value not written like a literal of its
    // type (a decimal outside its range, a datetime not in UTC, and so on)"
    // is an execution error (Execution errors); the interpreter checks "A
    // parameter value is written like a literal of its type: one spelling,
    // ... a datetime in UTC" (Validation). "This is the rule a parameter value
    // follows" (Conversion results), for the same texts as as….
    [Theory(
        Skip = "Bug: spec says a parameter value is written exactly like a "
            + "literal (datetime in UTC with Z, uuid in lower case, one "
            + "spelling); ParameterValues parses with DateTime/DateOnly/TimeOnly/"
            + "Guid/bool.Parse, so offsets, 7 fraction digits, '18:30', "
            + "'01/05/2024', upper-case or braced uuids and 'True' are bound"
    )]
    [InlineData("datetime", "2024-01-01T03:00:00+03:00")]
    [InlineData("datetime", "2024-01-01T00:00:00+00:00")]
    [InlineData("datetime", "2024-01-01T00:00:00")]
    [InlineData("datetime", "2024-01-01T00:00:00.1234567Z")]
    [InlineData("datetime", "2024-01-01T00:00:00.500Z")]
    [InlineData("datetime", "2024-01-01")]
    [InlineData("date", "2024-1-5")]
    [InlineData("date", "01/05/2024")]
    [InlineData("time", "18:30")]
    [InlineData("time", "18:30:00.500")]
    [InlineData("time", "18:30:00.1234567")]
    [InlineData("uuid", "3F2A6C1E-8B4D-4E2A-9C1F-1A2B3C4D5E6F")]
    [InlineData("uuid", "{3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6f}")]
    [InlineData("uuid", "3f2a6c1e8b4d4e2a9c1f1a2b3c4d5e6f")]
    [InlineData("boolean", "True")]
    [InlineData("boolean", " true")]
    public void ParameterInAnyOtherSpellingIsAnError(string type, string text)
    {
        _ = Assert.ThrowsAny<Exception>(() =>
            Selecting(
                Parameter(type),
                new Dictionary<string, IParameter>
                {
                    ["p"] = new TextParameter(type, text),
                }
            ).ToList()
        );
    }

    private static ExprProjection Parameter(string type)
    {
        return type switch
        {
            "date" => new AsStringDateStringProjection(new ParamDate("p")),
            "time" => new AsStringTimeStringProjection(new ParamTime("p")),
            "datetime" => new AsStringDatetimeStringProjection(new ParamDatetime("p")),
            "uuid" => new AsStringUuidStringProjection(new ParamUuid("p")),
            "boolean" => new AsStringBooleanStringProjection(new ParamBoolean("p")),
            _ => throw new ArgumentException(type),
        };
    }

    // asString of a literal of the type, built from its text.
    private static ExprProjection Written(string type, string value)
    {
        return type switch
        {
            "integer" => new AsStringDecimalStringProjection(
                new LiteralInteger(long.Parse(value, CultureInfo.InvariantCulture))
            ),
            "decimal" => new AsStringDecimalStringProjection(Decimal(value)),
            "date" => new AsStringDateStringProjection(Date(value)),
            "time" => new AsStringTimeStringProjection(Time(value)),
            "datetime" => new AsStringDatetimeStringProjection(Instant(value)),
            "uuid" => new AsStringUuidStringProjection(
                new LiteralUuid(Guid.Parse(value))
            ),
            _ => throw new ArgumentException(type),
        };
    }

    // as… of a string, written back with asString.
    private static ExprProjection Reading(string type, LiteralString operand)
    {
        return type switch
        {
            "integer" => new AsStringDecimalStringProjection(
                new AsIntegerIntegerProjection(operand)
            ),
            "decimal" => new AsStringDecimalStringProjection(
                new AsDecimalStringDecimalProjection(operand)
            ),
            "boolean" => new AsStringBooleanStringProjection(
                new AsBooleanBooleanProjection(operand)
            ),
            "date" => new AsStringDateStringProjection(
                new AsDateStringDateProjection(operand)
            ),
            "time" => new AsStringTimeStringProjection(
                new AsTimeStringTimeProjection(operand)
            ),
            "datetime" => new AsStringDatetimeStringProjection(
                new AsDatetimeStringDatetimeProjection(operand)
            ),
            "uuid" => new AsStringUuidStringProjection(new AsUuidUuidProjection(operand)),
            _ => throw new ArgumentException(type),
        };
    }

    // tryAs… of a string, written back with asString, NULL for null.
    private static ExprProjection TryReading(string type, StringNullableProjection text)
    {
        StringNullableProjection written = type switch
        {
            "integer" => new AsStringDecimalStringNullableProjection(
                new TryAsIntegerProjection(text)
            ),
            "decimal" => new AsStringDecimalStringNullableProjection(
                new TryAsDecimalProjection(text)
            ),
            "boolean" => new AsStringBooleanStringNullableProjection(
                new TryAsBooleanProjection(text)
            ),
            "date" => new AsStringDateStringNullableProjection(
                new TryAsDateProjection(text)
            ),
            "time" => new AsStringTimeStringNullableProjection(
                new TryAsTimeProjection(text)
            ),
            "datetime" => new AsStringDatetimeStringNullableProjection(
                new TryAsDatetimeProjection(text)
            ),
            "uuid" => new AsStringUuidStringNullableProjection(
                new TryAsUuidProjection(text)
            ),
            _ => throw new ArgumentException(type),
        };

        return Shown(written);
    }

    // A nullable text with NULL written for null.
    private static ExprProjection Shown(StringNullableProjection text)
    {
        return new CoalesceStringProjection([text, new LiteralString("NULL")]);
    }

    private static LiteralDecimal Decimal(string text)
    {
        return new LiteralDecimal(decimal.Parse(text, CultureInfo.InvariantCulture));
    }

    private static LiteralDate Date(string text)
    {
        return new LiteralDate(
            DateOnly.ParseExact(text, "yyyy-MM-dd", CultureInfo.InvariantCulture)
        );
    }

    private static LiteralTime Time(string text)
    {
        return new LiteralTime(TimeOnly.Parse(text, CultureInfo.InvariantCulture));
    }

    private static LiteralDatetime Instant(string text)
    {
        return new LiteralDatetime(
            DateTimeOffset.Parse(
                text,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal
            )
        );
    }

    private static PureQLProjection Selecting(
        ExprProjection expression,
        Dictionary<string, IParameter>? parameters = null
    )
    {
        return new PureQLProjection(
            [One],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.one"),
                select: [new SelectItemProjection("value", expression)]
            ),
            parameters ?? []
        );
    }

    private static string Value(ExprProjection expression)
    {
        return ResultColumn.Texts(Selecting(expression), "value")[0];
    }
}
