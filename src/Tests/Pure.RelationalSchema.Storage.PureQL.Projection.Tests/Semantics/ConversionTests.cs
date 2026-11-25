using System.Globalization;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.GroupExpressions;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.TotalExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// The conversions, as the specification defines them under "Conversions" and
// "Conversion results": asString writes the one spelling of a literal of every
// type but string; asInteger … asUuid of a string read exactly that spelling
// and fail on any other text with an execution error, where tryAs… gives null;
// both are lifted and of null give null; asDate / asTime of a datetime and
// asDatetime of a date in UTC (zones are under TimeZoneTests); asDecimal of an
// integer and asNullable widen without changing the value; asX(asString(v)) is
// v; and the conversions in where, a grouped and a total query.
public sealed class ConversionTests
{
    // Row 1 holds a value of every type, row 2 none: a lifted conversion is
    // null in row 2.
    private static readonly IStoredSchemaDataSet Cells = new InlineTable(
        "t",
        "cells",
        ("id", new LongColumnType()),
        ("n", new LongColumnType()),
        ("x", new DoubleColumnType()),
        ("b", new BoolColumnType()),
        ("d", new DateColumnType()),
        ("t", new TimeColumnType()),
        ("dt", new DateTimeColumnType()),
        ("u", new UuidColumnType())
    ).With(
        [
            "1",
            "-42",
            "19.9",
            "true",
            "0987-01-31",
            "18:30:00.125",
            "2024-01-31T23:30:00Z",
            "3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6f",
        ],
        ["2", "", "", "", "", "", "", ""]
    );

    // Dirty text: two integer literals, a postal code, no value and a word.
    private static readonly IStoredSchemaDataSet Texts = new InlineTable(
        "t",
        "texts",
        ("id", new LongColumnType()),
        ("s", new StringColumnType())
    ).With(["1", "12"], ["2", "007"], ["3", "12"], ["4", ""], ["5", "abc"]);

    private const string Uuid = "3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6f";

    private static readonly FieldInteger Id = new("t.cells", "id");

    private static readonly FieldIntegerNullable N = new("t.cells", "n");

    private static readonly FieldDecimalNullable X = new("t.cells", "x");

    private static readonly FieldBooleanNullable B = new("t.cells", "b");

    private static readonly FieldDateNullable D = new("t.cells", "d");

    private static readonly FieldTimeNullable T = new("t.cells", "t");

    private static readonly FieldDatetimeNullable Dt = new("t.cells", "dt");

    private static readonly FieldUuidNullable U = new("t.cells", "u");

    private static readonly FieldStringNullable S = new("t.texts", "s");

    [Theory]
    [InlineData("integer", "-42", "-42")]
    [InlineData("integer", "0", "0")]
    [InlineData("integer", "-9223372036854775808", "-9223372036854775808")]
    [InlineData("decimal", "0.125", "0.125")]
    [InlineData("decimal", "-0.5", "-0.5")]
    [InlineData("decimal", "1200", "1200")]
    [InlineData("boolean", "true", "true")]
    [InlineData("boolean", "false", "false")]
    [InlineData("date", "0987-01-31", "0987-01-31")]
    [InlineData("date", "9999-12-31", "9999-12-31")]
    [InlineData("time", "18:30:00", "18:30:00")]
    [InlineData("time", "18:30:00.125", "18:30:00.125")]
    [InlineData("time", "00:00:00.000001", "00:00:00.000001")]
    [InlineData("datetime", "2024-01-31T18:30:00Z", "2024-01-31T18:30:00Z")]
    [InlineData("datetime", "2024-01-31T18:30:00.25Z", "2024-01-31T18:30:00.25Z")]
    [InlineData("uuid", "3F2A6C1E-8B4D-4E2A-9C1F-1A2B3C4D5E6F", Uuid)]
    public void AsStringWritesTheLiteralSpellingOfTheValue(
        string type,
        string value,
        string expected
    )
    {
        Assert.Equal(expected, Value(Written(type, value)));
    }

    [Fact]
    public void AsStringWritesADecimalByItsValueNotByHowItWasComputed()
    {
        Assert.Equal(
            "0.125",
            Value(new AsStringDecimalStringProjection(Divide(Integer(1), Integer(8))))
        );
        Assert.Equal(
            "0.3333333333333333333333333333",
            Value(new AsStringDecimalStringProjection(Divide(Integer(1), Integer(3))))
        );
        Assert.Equal(
            "19.9",
            Value(new AsStringDecimalStringProjection(Multiply(Decimal("1.99"), Integer(10))))
        );
        Assert.Equal(
            "10",
            Value(new AsStringDecimalStringProjection(Multiply(Decimal("2.5"), Integer(4))))
        );
        Assert.Equal(
            "0",
            Value(new AsStringDecimalStringProjection(Multiply(Decimal("-1.5"), Integer(0))))
        );
    }

    [Fact]
    public void AsStringIsLiftedAndOfNullIsNullNotTheTextNull()
    {
        Assert.Equal(
            ["-42", "NULL"],
            Values(Shown(new AsStringDecimalStringNullableProjection(N)))
        );
        Assert.Equal(
            ["19.9", "NULL"],
            Values(Shown(new AsStringDecimalStringNullableProjection(X)))
        );
        Assert.Equal(
            ["true", "NULL"],
            Values(Shown(new AsStringBooleanStringNullableProjection(B)))
        );
        Assert.Equal(
            ["0987-01-31", "NULL"],
            Values(Shown(new AsStringDateStringNullableProjection(D)))
        );
        Assert.Equal(
            ["18:30:00.125", "NULL"],
            Values(Shown(new AsStringTimeStringNullableProjection(T)))
        );
        Assert.Equal(
            ["2024-01-31T23:30:00Z", "NULL"],
            Values(Shown(new AsStringDatetimeStringNullableProjection(Dt)))
        );
        Assert.Equal(
            [Uuid, "NULL"],
            Values(Shown(new AsStringUuidStringNullableProjection(U)))
        );
    }

    [Theory]
    [InlineData("integer", "-9223372036854775808")]
    [InlineData("integer", "9223372036854775807")]
    [InlineData("decimal", "19.9")]
    [InlineData("decimal", "1200")]
    [InlineData("decimal", "0.0000000000000000000000000001")]
    [InlineData("decimal", "-9999999999999999999999999999")]
    [InlineData("boolean", "true")]
    [InlineData("boolean", "false")]
    [InlineData("date", "2024-02-29")]
    [InlineData("time", "18:30:00")]
    [InlineData("time", "23:59:59.999999")]
    [InlineData("datetime", "2024-01-31T18:30:00.25Z")]
    [InlineData("uuid", Uuid)]
    public void TextInTheLiteralSpellingIsReadAndWrittenBackAsItIs(string type, string text)
    {
        Assert.Equal(text, Value(Reading(type, text)));
        Assert.Equal(text, Value(TryReading(type, new LiteralString(text))));
    }

    [Theory]
    [InlineData("integer", " 12")]
    [InlineData("integer", "+5")]
    [InlineData("integer", "007")]
    [InlineData("integer", "1e3")]
    [InlineData("integer", "-0")]
    [InlineData("integer", "9223372036854775808")]
    [InlineData("decimal", "1.50")]
    [InlineData("decimal", ".5")]
    [InlineData("decimal", "5.")]
    [InlineData("decimal", "-0")]
    [InlineData("decimal", "10000000000000000000000000000")]
    [InlineData("decimal", "0.00000000000000000000000000001")]
    [InlineData("boolean", "TRUE")]
    [InlineData("boolean", "1")]
    [InlineData("boolean", "")]
    [InlineData("date", "2024-1-5")]
    [InlineData("date", "2024-02-30")]
    [InlineData("date", "0000-01-01")]
    [InlineData("date", "2024-01-31T00:00:00Z")]
    [InlineData("time", "18:30")]
    [InlineData("time", "18:30:00.500")]
    [InlineData("time", "24:00:00")]
    [InlineData("time", "18:30:00.1234567")]
    [InlineData("datetime", "2024-01-31T18:30:00+00:00")]
    [InlineData("datetime", "2024-01-31T18:30:00")]
    [InlineData("datetime", "2024-01-31t18:30:00z")]
    [InlineData("datetime", "2024-01-31T18:30:00.500Z")]
    [InlineData("uuid", "3F2A6C1E-8B4D-4E2A-9C1F-1A2B3C4D5E6F")]
    [InlineData("uuid", "{3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6f}")]
    [InlineData("uuid", "3f2a6c1e8b4d4e2a9c1f1a2b3c4d5e6f")]
    public void TextInAnyOtherSpellingFailsAsAndIsNullForTryAs(string type, string text)
    {
        PureQLProjection projection = Selecting(Reading(type, text));

        _ = Assert.Throws<FormatException>(() => projection.ToList());
        Assert.Equal("NULL", Value(TryReading(type, new LiteralString(text))));
    }

    [Theory]
    [InlineData("integer")]
    [InlineData("decimal")]
    [InlineData("boolean")]
    [InlineData("date")]
    [InlineData("time")]
    [InlineData("datetime")]
    [InlineData("uuid")]
    public void ReadingANullStringGivesNullForAsAndTryAs(string type)
    {
        Assert.Equal("NULL", Value(NullReading(type)));
        Assert.Equal("NULL", Value(TryReading(type, new LiteralStringNullable())));
    }

    [Fact]
    public void ReadingAStringGivesTheTypeInTheName()
    {
        PureQLProjection projection = SelectingAll(
            new SelectItemProjection("integer", Read("integer", "12")),
            new SelectItemProjection("decimal", Read("decimal", "12")),
            new SelectItemProjection("boolean", Read("boolean", "true")),
            new SelectItemProjection("date", Read("date", "2024-01-31")),
            new SelectItemProjection("time", Read("time", "18:30:00")),
            new SelectItemProjection("datetime", Read("datetime", "2024-01-31T18:30:00Z")),
            new SelectItemProjection("uuid", Read("uuid", Uuid))
        );

        Assert.Equal(
            [
                new LongColumnType().Name.TextValue,
                "decimal",
                new BoolColumnType().Name.TextValue,
                new DateColumnType().Name.TextValue,
                new TimeColumnType().Name.TextValue,
                new DateTimeColumnType().Name.TextValue,
                new UuidColumnType().Name.TextValue,
            ],
            projection.TableSchema.Columns.Select(column => column.Type.Name.TextValue)
        );
    }

    [Fact]
    public void AsOfARowThatIsNotALiteralFailsWhileTheRowsAreRead()
    {
        PureQLProjection projection = OverTexts(new AsIntegerIntegerNullableProjection(S));

        _ = Assert.Throws<FormatException>(() => projection.ToList());
    }

    [Fact]
    public void AsOfRowsThatWhereLeavesOutIsNeverEvaluated()
    {
        PureQLProjection projection = OverTexts(
            new AsIntegerIntegerNullableProjection(S),
            new EqualStringRow(S, new LiteralString("12"))
        );

        Assert.Equal(["12", "12"], ResultColumn.Texts(projection, "value"));
    }

    [Fact]
    public void CoalesceOverTryAsSuppliesADefaultForDirtyText()
    {
        PureQLProjection projection = OverTexts(
            new CoalesceIntegerProjection([
                new TryAsIntegerProjection(S),
                new LiteralInteger(-1),
            ])
        );

        Assert.Equal(
            ["12", "-1", "12", "-1", "-1"],
            ResultColumn.Texts(projection, "value")
        );
    }

    [Fact]
    public void TryAsEqualToNullInWhereFindsTheRowsThatHoldSomethingElse()
    {
        PureQLProjection projection = OverTexts(
            new FieldInteger("t.texts", "id"),
            new EqualDecimalRow(new TryAsIntegerRow(S), new LiteralIntegerNullable())
        );

        Assert.Equal(["2", "4", "5"], ResultColumn.Texts(projection, "value"));
    }

    [Fact]
    public void GroupingByTryAsPutsEveryRowThatIsNotALiteralInTheNullGroup()
    {
        PureQLProjection projection = new(
            [Texts],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.texts"),
                groupBy: [new GroupKey("n", new TryAsIntegerRow(S))],
                select:
                [
                    new SelectItemGroup(
                        "n",
                        new CoalesceStringGroup([
                            new AsStringDecimalStringNullableGroup(
                                new KeyIntegerNullable("n")
                            ),
                            new LiteralString("NULL"),
                        ])
                    ),
                    new SelectItemGroup(
                        "rows",
                        new AsStringDecimalStringGroup(new CountGroup())
                    ),
                ],
                orderBy: [new OrderItemGroupColumn("n")]
            )
        );

        Assert.Equal(["12", "NULL"], ResultColumn.Texts(projection, "n"));
        Assert.Equal(["2", "3"], ResultColumn.Texts(projection, "rows"));
    }

    [Fact]
    public void ConversionsOfAggregatesAreReadInATotalQuery()
    {
        PureQLProjection projection = new(
            [Texts],
            new MainTotalQuery(
                from: new FromEntity("t.texts"),
                aggregate:
                [
                    new SelectItemTotal(
                        "sum",
                        new AsStringDecimalStringTotal(
                            new SumIntegerTotal(new TryAsIntegerRow(S))
                        )
                    ),
                    new SelectItemTotal(
                        "other",
                        new AsStringDecimalStringTotal(
                            new CountTotal(
                                new EqualDecimalRow(
                                    new TryAsIntegerRow(S),
                                    new LiteralIntegerNullable()
                                )
                            )
                        )
                    ),
                ]
            )
        );

        Assert.Equal(["24"], ResultColumn.Texts(projection, "sum"));
        Assert.Equal(["3"], ResultColumn.Texts(projection, "other"));
    }

    [Fact]
    public void AsStringThenAsOfTheTypeGivesTheValueAgain()
    {
        Assert.Equal(
            ["True", "True"],
            Values(
                new EqualDecimalProjection(
                    new AsIntegerIntegerNullableProjection(
                        new AsStringDecimalStringNullableProjection(N)
                    ),
                    N
                )
            )
        );
        Assert.Equal(
            ["True", "True"],
            Values(
                new EqualDecimalProjection(
                    new AsDecimalStringDecimalNullableProjection(
                        new AsStringDecimalStringNullableProjection(X)
                    ),
                    X
                )
            )
        );
        Assert.Equal(
            ["True", "True"],
            Values(
                new EqualBooleanProjection(
                    new AsBooleanBooleanNullableProjection(
                        new AsStringBooleanStringNullableProjection(B)
                    ),
                    B
                )
            )
        );
        Assert.Equal(
            ["True", "True"],
            Values(
                new EqualDateProjection(
                    new AsDateStringDateNullableProjection(
                        new AsStringDateStringNullableProjection(D)
                    ),
                    D
                )
            )
        );
        Assert.Equal(
            ["True", "True"],
            Values(
                new EqualTimeProjection(
                    new AsTimeStringTimeNullableProjection(
                        new AsStringTimeStringNullableProjection(T)
                    ),
                    T
                )
            )
        );
        Assert.Equal(
            ["True", "True"],
            Values(
                new EqualDatetimeProjection(
                    new AsDatetimeStringDatetimeNullableProjection(
                        new AsStringDatetimeStringNullableProjection(Dt)
                    ),
                    Dt
                )
            )
        );
        Assert.Equal(
            ["True", "True"],
            Values(
                new EqualUuidProjection(
                    new AsUuidUuidNullableProjection(
                        new AsStringUuidStringNullableProjection(U)
                    ),
                    U
                )
            )
        );
    }

    [Theory]
    [InlineData("2024-01-31T23:30:00Z", "2024-01-31", "23:30:00")]
    [InlineData("2024-12-31T23:59:59.999999Z", "2024-12-31", "23:59:59.999999")]
    [InlineData("0001-01-01T00:00:00Z", "0001-01-01", "00:00:00")]
    public void AsDateAndAsTimeReadADatetimeInUtc(
        string instant,
        string date,
        string time
    )
    {
        Assert.Equal(date, Value(new AsDateDatetimeDateProjection(Instant(instant))));
        Assert.Equal(time, Value(new AsTimeDatetimeTimeProjection(Instant(instant))));
    }

    [Theory]
    [InlineData("2024-01-31", "2024-01-31T00:00:00Z")]
    [InlineData("9999-12-31", "9999-12-31T00:00:00Z")]
    public void AsDatetimeOfADateIsItsMidnightInUtc(string date, string expected)
    {
        Assert.Equal(expected, Value(new AsDatetimeDateDatetimeProjection(Date(date))));
    }

    [Fact]
    public void ConversionsBetweenTemporalTypesAreLifted()
    {
        Assert.Equal(
            ["2024-01-31", ""],
            Values(new AsDateDatetimeDateNullableProjection(Dt))
        );
        Assert.Equal(
            ["23:30:00", ""],
            Values(new AsTimeDatetimeTimeNullableProjection(Dt))
        );
        Assert.Equal(
            ["0987-01-31T00:00:00Z", ""],
            Values(new AsDatetimeDateDatetimeNullableProjection(D))
        );
    }

    [Fact]
    public void AsDatetimeOfAsDateIsStartOfDay()
    {
        Assert.Equal(
            Values(new StartOfDayDatetimeNullableProjection(Dt)),
            Values(
                new AsDatetimeDateDatetimeNullableProjection(
                    new AsDateDatetimeDateNullableProjection(Dt)
                )
            )
        );
        Assert.Equal(
            ["2024-01-31T00:00:00Z", ""],
            Values(new StartOfDayDatetimeNullableProjection(Dt))
        );
    }

    [Fact]
    public void AsDecimalOfAnIntegerIsTheSameNumberAsADecimal()
    {
        PureQLProjection projection = SelectingAll(
            new SelectItemProjection(
                "max",
                new AsDecimalIntegerDecimalProjection(
                    new LiteralInteger(long.MaxValue)
                )
            ),
            new SelectItemProjection(
                "half",
                new DivideDecimalProjection([
                    new AsDecimalIntegerDecimalProjection(new LiteralInteger(7)),
                    Integer(2),
                ])
            ),
            new SelectItemProjection("n", new AsDecimalIntegerDecimalNullableProjection(N))
        );

        Assert.Equal(
            ["9223372036854775807", "9223372036854775807"],
            ResultColumn.Texts(projection, "max")
        );
        Assert.Equal(["3.5", "3.5"], ResultColumn.Texts(projection, "half"));
        Assert.Equal(["-42", ""], ResultColumn.Texts(projection, "n"));
        Assert.Equal(
            ["decimal", "decimal", "decimal"],
            projection.TableSchema.Columns.Select(column => column.Type.Name.TextValue)
        );
    }

    [Fact]
    public void AsNullableKeepsTheValueAndTheNameOfTheType()
    {
        PureQLProjection projection = SelectingAll(
            new SelectItemProjection(
                "integer",
                new AsNullableIntegerNullableProjection(new LiteralInteger(-42))
            ),
            new SelectItemProjection(
                "decimal",
                new AsNullableDecimalNullableProjection(Decimal("19.9"))
            ),
            new SelectItemProjection(
                "string",
                new AsNullableStringNullableProjection(new LiteralString("active"))
            ),
            new SelectItemProjection(
                "boolean",
                new AsNullableBooleanNullableProjection(new LiteralBoolean(false))
            ),
            new SelectItemProjection(
                "date",
                new AsNullableDateNullableProjection(Date("0987-01-31"))
            ),
            new SelectItemProjection(
                "time",
                new AsNullableTimeNullableProjection(Time("18:30:00.125"))
            ),
            new SelectItemProjection(
                "datetime",
                new AsNullableDatetimeNullableProjection(Instant("2024-01-31T23:30:00Z"))
            ),
            new SelectItemProjection(
                "uuid",
                new AsNullableUuidNullableProjection(new LiteralUuid(Guid.Parse(Uuid)))
            )
        );

        Assert.Equal(["-42", "-42"], ResultColumn.Texts(projection, "integer"));
        Assert.Equal(["19.9", "19.9"], ResultColumn.Texts(projection, "decimal"));
        Assert.Equal(["active", "active"], ResultColumn.Texts(projection, "string"));
        Assert.Equal(["False", "False"], ResultColumn.Texts(projection, "boolean"));
        Assert.Equal(["0987-01-31", "0987-01-31"], ResultColumn.Texts(projection, "date"));
        Assert.Equal(
            ["18:30:00.125", "18:30:00.125"],
            ResultColumn.Texts(projection, "time")
        );
        Assert.Equal(
            ["2024-01-31T23:30:00Z", "2024-01-31T23:30:00Z"],
            ResultColumn.Texts(projection, "datetime")
        );
        Assert.Equal([Uuid, Uuid], ResultColumn.Texts(projection, "uuid"));
        Assert.Equal(
            [
                new LongColumnType().Name.TextValue,
                "decimal",
                new StringColumnType().Name.TextValue,
                new BoolColumnType().Name.TextValue,
                new DateColumnType().Name.TextValue,
                new TimeColumnType().Name.TextValue,
                new DateTimeColumnType().Name.TextValue,
                new UuidColumnType().Name.TextValue,
            ],
            projection.TableSchema.Columns.Select(column => column.Type.Name.TextValue)
        );
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
            "boolean" => new AsStringBooleanStringProjection(
                new LiteralBoolean(bool.Parse(value))
            ),
            "date" => new AsStringDateStringProjection(Date(value)),
            "time" => new AsStringTimeStringProjection(Time(value)),
            "datetime" => new AsStringDatetimeStringProjection(Instant(value)),
            "uuid" => new AsStringUuidStringProjection(new LiteralUuid(Guid.Parse(value))),
            _ => throw new ArgumentException(type),
        };
    }

    // as… of a string literal, the value it reads.
    private static ExprProjection Read(string type, string text)
    {
        LiteralString operand = new(text);

        return type switch
        {
            "integer" => new AsIntegerIntegerProjection(operand),
            "decimal" => new AsDecimalStringDecimalProjection(operand),
            "boolean" => new AsBooleanBooleanProjection(operand),
            "date" => new AsDateStringDateProjection(operand),
            "time" => new AsTimeStringTimeProjection(operand),
            "datetime" => new AsDatetimeStringDatetimeProjection(operand),
            "uuid" => new AsUuidUuidProjection(operand),
            _ => throw new ArgumentException(type),
        };
    }

    // as… of a string literal, written back with asString.
    private static ExprProjection Reading(string type, string text)
    {
        LiteralString operand = new(text);

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
            "date" => new AsStringDateStringProjection(new AsDateStringDateProjection(operand)),
            "time" => new AsStringTimeStringProjection(new AsTimeStringTimeProjection(operand)),
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
            "date" => new AsStringDateStringNullableProjection(new TryAsDateProjection(text)),
            "time" => new AsStringTimeStringNullableProjection(new TryAsTimeProjection(text)),
            "datetime" => new AsStringDatetimeStringNullableProjection(
                new TryAsDatetimeProjection(text)
            ),
            "uuid" => new AsStringUuidStringNullableProjection(new TryAsUuidProjection(text)),
            _ => throw new ArgumentException(type),
        };

        return Shown(written);
    }

    // as… of the null string literal, written back with asString, NULL for null.
    private static ExprProjection NullReading(string type)
    {
        LiteralStringNullable text = new();
        StringNullableProjection written = type switch
        {
            "integer" => new AsStringDecimalStringNullableProjection(
                new AsIntegerIntegerNullableProjection(text)
            ),
            "decimal" => new AsStringDecimalStringNullableProjection(
                new AsDecimalStringDecimalNullableProjection(text)
            ),
            "boolean" => new AsStringBooleanStringNullableProjection(
                new AsBooleanBooleanNullableProjection(text)
            ),
            "date" => new AsStringDateStringNullableProjection(
                new AsDateStringDateNullableProjection(text)
            ),
            "time" => new AsStringTimeStringNullableProjection(
                new AsTimeStringTimeNullableProjection(text)
            ),
            "datetime" => new AsStringDatetimeStringNullableProjection(
                new AsDatetimeStringDatetimeNullableProjection(text)
            ),
            "uuid" => new AsStringUuidStringNullableProjection(
                new AsUuidUuidNullableProjection(text)
            ),
            _ => throw new ArgumentException(type),
        };

        return Shown(written);
    }

    // A nullable text with NULL written for null, which a result cell would
    // show as the empty string.
    private static ExprProjection Shown(StringNullableProjection text)
    {
        return new CoalesceStringProjection([text, new LiteralString("NULL")]);
    }

    private static DivideDecimalProjection Divide(
        DecimalProjection left,
        DecimalProjection right
    )
    {
        return new DivideDecimalProjection([left, right]);
    }

    private static MultiplyDecimalProjection Multiply(
        DecimalProjection left,
        DecimalProjection right
    )
    {
        return new MultiplyDecimalProjection([left, right]);
    }

    private static LiteralInteger Integer(long value)
    {
        return new LiteralInteger(value);
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

    // The query over the texts that selects one expression for the rows where
    // keeps, in the order of id.
    private static PureQLProjection OverTexts(
        ExprProjection expression,
        BooleanRow? where = null
    )
    {
        return new PureQLProjection(
            [Texts],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.texts"),
                where: where,
                select: [new SelectItemProjection("value", expression)],
                orderBy:
                [
                    new OrderItemProjectionExpression(new FieldInteger("t.texts", "id")),
                ]
            )
        );
    }

    // The query that selects the columns for every row, in the order of id.
    private static PureQLProjection SelectingAll(params SelectItemProjection[] columns)
    {
        return new PureQLProjection(
            [Cells],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.cells"),
                select: columns,
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );
    }

    // The query that selects one expression for every row, in the order of id.
    private static PureQLProjection Selecting(ExprProjection expression)
    {
        return SelectingAll(new SelectItemProjection("value", expression));
    }

    private static IReadOnlyList<string> Values(ExprProjection expression)
    {
        return ResultColumn.Texts(Selecting(expression), "value");
    }

    // The value of an expression that reads no row.
    private static string Value(ExprProjection expression)
    {
        return Values(expression)[0];
    }
}
