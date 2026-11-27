using Pure.Primitives.Abstractions.String;
using Pure.Primitives.Number;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.ListParameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Parameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.GroupExpressions;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.TotalExpressions;
using Decimal = Pure.Primitives.Number.Decimal;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Parameters at the edges of their values and inside the constructs of
// specification 3: a decimal of 28 digits is read exactly and one of 29 is
// rejected; a list parameter may be empty and may repeat a value, and holds
// no null; a typed null and a list are read in set operations, inline rows,
// recursive subqueries, windows and the predicates of aggregates; date math
// is bound again at the instant every run starts, wherever it stands.
public sealed class ParameterEdgeTests
{
    private const string Thursday = "2026-10-29T08:30:15Z";

    // Item 4 has no name and no price.
    private static readonly IStoredSchemaDataSet Items = new InlineTable(
        "t",
        "items",
        ("id", new LongColumnType()),
        ("name", new StringColumnType()),
        ("price", new DoubleColumnType()),
        ("day", new DateColumnType())
    ).With(
        ["1", "pen", "1.5", "2026-10-26"],
        ["2", "ink", "2", "2026-10-27"],
        ["3", "pad", "3", "2026-10-28"],
        ["4", "", "", "2026-10-29"]
    );

    private static readonly FieldInteger Id = new("t.items", "id");

    private static readonly FieldStringNullable Name = new("t.items", "name");

    // ---- Parameters: "a decimal (a string within its range)"; Integer and
    // decimal arithmetic: "Every value is m x 10^-s with an integer
    // |m| < 10^28 and 0 <= s <= 28 ... a parameter outside it is an
    // execution error, so literals and parameters are read exactly".

    [Theory]
    [InlineData("1234567890123456789012345678")]
    [InlineData("-9999999999999999999999999999")]
    [InlineData("0.0000000000000000000000000001")]
    [InlineData("0.1234567890123456789012345678")]
    [InlineData("123456789012345.6789012345678")]
    [InlineData("-0.5")]
    public void DecimalParameterOfAtMost28DigitsIsReadExactly(string value)
    {
        Assert.Equal([value], Selected(new ParamDecimal("p"), "decimal", value));
    }

    [Theory]
    [InlineData("12345678901234567890123456789")]
    [InlineData("10000000000000000000000000000")]
    [InlineData("1234567890123456789012345678.9")]
    public void DecimalParameterOfMoreThan28SignificantDigitsIsRejected(string value)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            Selected(new ParamDecimal("p"), "decimal", value)
        );
    }

    [Fact]
    public void DecimalParameterOfMoreThan28DigitsAfterRoundingIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            Selected(new ParamDecimal("p"), "decimal", "1.00000000000000000000000000001")
        );
    }

    [Theory(
        Skip = "Bug: spec allows at most 28 decimal places in a decimal parameter (an "
            + "execution error otherwise); the library rounds the text to 28 places"
    )]
    [InlineData("0.00000000000000000000000000001")]
    [InlineData("0.12345678901234567890123456789")]
    public void DecimalParameterOfMoreThan28DecimalPlacesIsRejected(string value)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            Selected(new ParamDecimal("p"), "decimal", value)
        );
    }

    [Theory]
    [InlineData("9223372036854775807")]
    [InlineData("-9223372036854775808")]
    public void IntegerParameterAtTheBoundsOf64BitsIsReadExactly(string value)
    {
        Assert.Equal([value], Selected(new ParamInteger("p"), "integer", value));
    }

    [Fact(
        Skip = "Bug: spec says an integer is a JSON number with no fractional part, "
            + "so 5.0 is the integer 5; the library rejects the text 5.0"
    )]
    public void IntegerParameterWrittenWithAZeroFractionIsThatInteger()
    {
        // Types: an integer is a "JSON integer ...: a number with no
        // fractional part, so 5.0 counts"; Parameters: "a JSON number for
        // integer".
        Assert.Equal(["5"], Selected(new ParamInteger("p"), "integer", "5.0"));
    }

    [Fact]
    public void IntegerParameterBelowTheBoundsOf64BitsIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            Selected(new ParamInteger("p"), "integer", "-9223372036854775809")
        );
    }

    [Theory(
        Skip = "Bug: spec rejects a datetime parameter not in UTC (written with Z); "
            + "the library converts an offset, or none, to UTC and accepts it"
    )]
    [InlineData("2024-01-01T03:00:00+03:00")]
    [InlineData("2024-01-01T00:00:00+00:00")]
    [InlineData("2024-01-01T00:00:00")]
    public void DatetimeParameterNotWrittenInUtcIsRejected(string text)
    {
        // Validation (interpreter): "A parameter value is written like a
        // literal of its type: ..., a datetime in UTC"; Execution errors: "a
        // datetime not in UTC". This contradicts the existing
        // ParameterBindingTests.DatetimeTextWithAnOffsetIsTheInstantItDenotesInUtc.
        _ = Assert.Throws<ArgumentException>(() =>
            Selected(Param("datetime"), "datetime", text)
        );
    }

    [Theory(
        Skip = "Bug: spec requires a parameter value written in the one spelling of a "
            + "literal of its type; the library parses it leniently (.NET Parse)"
    )]
    [InlineData("decimal", "1e2")]
    [InlineData("decimal", "1E-2")]
    [InlineData("time", "18:30")]
    [InlineData("time", "18:30:00.1234567")]
    [InlineData("date", "01/05/2024")]
    [InlineData("uuid", "{3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6f}")]
    [InlineData("uuid", "3F2A6C1E-8B4D-4E2A-9C1F-1A2B3C4D5E6F")]
    [InlineData("uuid", "3f2a6c1e8b4d4e2a9c1f1a2b3c4d5e6f")]
    [InlineData("integer", "+5")]
    [InlineData("integer", " 5")]
    public void ParameterValueNotWrittenLikeALiteralOfItsTypeIsRejected(
        string type,
        string text
    )
    {
        // Parameters: "A value is written exactly like a literal of the
        // declared type, in the same one spelling"; Validation (interpreter):
        // "A parameter value is written like a literal of its type: one
        // spelling, a decimal within its range, a datetime in UTC".
        _ = Assert.Throws<ArgumentException>(() => Selected(Param(type), type, text));
    }

    [Theory]
    [InlineData("date", "0001-01-01")]
    [InlineData("date", "9999-12-31")]
    [InlineData("datetime", "9999-12-31T23:59:59.999999Z")]
    [InlineData("time", "23:59:59.999999")]
    [InlineData("uuid", "3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6f")]
    public void ParameterValueAtTheEdgeOfItsTypeIsReadAsWritten(string type, string text)
    {
        Assert.Equal([text], Selected(Param(type), type, text));
    }

    // ---- Lists: "A list parameter is a JSON array of non-null values of
    // the element type, possibly empty, and may repeat a value"; "A null
    // value is in a list exactly when the list contains null".

    [Fact]
    public void ListParameterThatRepeatsAValueKeepsEachRowOnce()
    {
        Assert.Equal(
            ["1", "3"],
            Ids(
                new InStringRow(Name, new ListParamString("l")),
                Strings("pen", "pen", "pad", "pad", "pen")
            )
        );
    }

    [Fact]
    public void EmptyListParameterHoldsNoValueNotEvenNull()
    {
        Assert.Empty(Ids(new InStringRow(Name, new ListParamString("l")), Strings()));
        Assert.Equal(
            ["1", "2", "3", "4"],
            Ids(new NotRow(new InStringRow(Name, new ListParamString("l"))), Strings())
        );
    }

    [Fact]
    public void NullOperandIsNotInAListParameter()
    {
        Assert.Equal(
            ["1"],
            Ids(new InStringRow(Name, new ListParamString("l")), Strings("pen", "ink2"))
        );
        Assert.Equal(
            ["2", "3", "4"],
            Ids(
                new NotRow(new InStringRow(Name, new ListParamString("l"))),
                Strings("pen", "ink2")
            )
        );
    }

    [Fact]
    public void NullParameterOperandIsInNoListParameter()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items"),
                where: new EqualDecimalRow(Id, new LiteralInteger(1)),
                select:
                [
                    new SelectItemProjection(
                        "in",
                        new InStringProjection(
                            new ParamStringNullable("v"),
                            new ListParamString("l")
                        )
                    ),
                    new SelectItemProjection(
                        "notIn",
                        new NotProjection(
                            new InStringProjection(
                                new ParamStringNullable("v"),
                                new ListParamString("l")
                            )
                        )
                    ),
                ]
            ),
            new Dictionary<string, IParameter>
            {
                ["v"] = new NullableStringParameter(),
                ["l"] = new StringListParameter([new String("pen")]),
            }
        );

        Assert.Equal(["False"], ResultColumn.Texts(projection, "in"));
        Assert.Equal(["True"], ResultColumn.Texts(projection, "notIn"));
    }

    [Fact]
    public void IntegerListParameterIsComparedWithDecimalsByValue()
    {
        // Implicit conversions: "an integerList stand where a list of decimal
        // is expected"; 2 equals 2.0, 1.5 is no integer of the list.
        Assert.Equal(
            ["2", "3"],
            Ids(
                new InDecimalRow(
                    new FieldDecimalNullable("t.items", "price"),
                    new ListParamDecimal("l")
                ),
                new IntegerListParameter([new Long(2), new Long(3), new Long(1)])
            )
        );
    }

    [Fact]
    public void ListParameterIsReadInBothInputsOfAnExcept()
    {
        // The names in l except those in l that are of an id above 2.
        PureQLProjection projection = new(
            [Items],
            new MainCombinedQueryExcept([
                NamesWhere(new InStringRow(Name, new ListParamString("l"))),
                NamesWhere(
                    new AndRow([
                        new InStringRow(Name, new ListParamString("l")),
                        new GreaterThanDecimalRow(Id, new LiteralInteger(2)),
                    ])
                ),
            ]),
            Bound("l", Strings("pen", "pad", "ink"))
        );

        Assert.Equal(
            ["ink", "pen"],
            ResultColumn.Texts(projection, "name").Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void ListParameterIsReadInTheNextOfARecursiveSubquery()
    {
        // The steps 1, 2, ... while the step is in l: 1 to 3.
        Assert.Equal(
            ["1", "2", "3"],
            ResultColumn.Texts(
                Steps(
                    new InDecimalRow(
                        new FieldInteger("steps", "x"),
                        new ListParamInteger("l")
                    ),
                    new LiteralInteger(1),
                    Bound(
                        "l",
                        new IntegerListParameter([new Long(1), new Long(2), new Long(5)])
                    )
                ),
                "x"
            )
        );
    }

    [Fact]
    public void ListParameterIsReadInThePredicateOfAWindowAggregate()
    {
        // The running count of the rows whose name is in l, by id.
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items"),
                select:
                [
                    new SelectItemProjection(
                        "c",
                        new CountProjection(
                            predicate: new InStringRow(Name, new ListParamString("l")),
                            over: new WindowWithFrame(
                                [new OrderItemRow(Id)],
                                new Frame(preceding: new FrameRows(new All()))
                            )
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            ),
            Bound("l", Strings("ink", "pad", "zzz"))
        );

        Assert.Equal(["0", "1", "2", "2"], ResultColumn.Texts(projection, "c"));
    }

    [Fact]
    public void EmptyListParameterIsReadInThePredicateOfATotalAggregate()
    {
        PureQLProjection projection = new(
            [Items],
            new MainTotalQuery(
                from: new FromEntity("t.items"),
                aggregate:
                [
                    new SelectItemTotal(
                        "in",
                        new CountTotal(new InStringRow(Name, new ListParamString("l")))
                    ),
                    new SelectItemTotal(
                        "out",
                        new CountTotal(
                            new NotRow(new InStringRow(Name, new ListParamString("l")))
                        )
                    ),
                ]
            ),
            Bound("l", Strings())
        );

        Assert.Equal(["0"], ResultColumn.Texts(projection, "in"));
        Assert.Equal(["4"], ResultColumn.Texts(projection, "out"));
    }

    // ---- Typed nulls and scalars inside the new constructs.

    [Fact]
    public void TypedNullInAnInputOfAUnionIsTheSameValueAsANullName()
    {
        // Set operations compare with equal: two nulls are one value.
        PureQLProjection projection = new(
            [Items],
            new MainCombinedQueryUnion([
                NamesWhere(null),
                One("name", new ParamStringNullable("v")),
            ]),
            Bound("v", new NullableStringParameter())
        );

        Assert.Equal(
            ["", "ink", "pad", "pen"],
            ResultColumn.Texts(projection, "name").Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void IntegerAndDecimalParametersOfOneValueAreOneRowOfAUnion()
    {
        PureQLProjection projection = new(
            [Items],
            new MainCombinedQueryUnion([
                One("x", new ParamInteger("i")),
                One("x", new ParamDecimal("d")),
            ]),
            new Dictionary<string, IParameter>
            {
                ["i"] = new IntegerParameter(new Long(1)),
                ["d"] = new DecimalParameter(new Decimal(1.0m)),
            }
        );

        Assert.Equal(["1"], ResultColumn.Texts(projection, "x"));
        Assert.Equal(
            "decimal",
            Assert.Single(projection.TableSchema.Columns).Type.Name.TextValue
        );
    }

    [Fact]
    public void IntegerParameterBoundToADecimalCellIsADecimalColumn()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRows(
                        "cells",
                        [
                            [new RowItem("x", new ParamDecimal("d"))],
                            [new RowItem("x", new LiteralInteger(2))],
                        ]
                    ),
                ],
                from: new FromSubquery("cells"),
                select: [new SelectItemProjection("x", new FieldDecimal("cells", "x"))],
                orderBy: [new OrderItemProjectionColumn("x")]
            ),
            Bound("d", new IntegerParameter(new Long(3)))
        );

        Assert.Equal(["2", "3"], ResultColumn.Texts(projection, "x"));
    }

    [Theory]
    [InlineData(null, "1|2|3")]
    [InlineData(5L, "1|2|3|4|5")]
    public void NullableParameterStopsARecursionThroughCoalesce(long? limit, string steps)
    {
        IParameter value = limit is long number
            ? new IntegerParameter(new Long(number))
            : new NullableIntegerParameter();

        PureQLProjection projection = Steps(
            new LessThanDecimalRow(
                new FieldInteger("steps", "x"),
                new CoalesceIntegerRow([
                    new ParamIntegerNullable("limit"),
                    new LiteralInteger(3),
                ])
            ),
            new LiteralInteger(1),
            Bound("limit", value)
        );

        Assert.Equal(steps.Split('|'), ResultColumn.Texts(projection, "x"));
    }

    [Fact]
    public void ParameterIsTheAmountEachStepOfARecursionAdds()
    {
        PureQLProjection projection = Steps(
            new LessThanDecimalRow(
                new FieldInteger("steps", "x"),
                new LiteralInteger(30)
            ),
            new ParamInteger("step"),
            Bound("step", new IntegerParameter(new Long(10))),
            start: 0
        );

        Assert.Equal(["0", "10", "20", "30"], ResultColumn.Texts(projection, "x"));
    }

    [Fact]
    public void ParameterInAWindowsOrderReversesIt()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items"),
                select:
                [
                    new SelectItemProjection("id", Id),
                    new SelectItemProjection(
                        "n",
                        new RowNumberProjection(
                            new OrderedWindow([
                                new OrderItemRow(
                                    new MultiplyIntegerRow([Id, new ParamInteger("sign")])
                                ),
                            ])
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionColumn("id")]
            ),
            Bound("sign", new IntegerParameter(new Long(-1)))
        );

        Assert.Equal(["4", "3", "2", "1"], ResultColumn.Texts(projection, "n"));
    }

    [Fact]
    public void ParameterIsTheSeparatorOfConcatAllOverAWindowAndAGroup()
    {
        Separator separator = new(new ParamString("sep"));
        PureQLProjection windowed = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items"),
                where: new LessThanDecimalRow(Id, new LiteralInteger(4)),
                select:
                [
                    new SelectItemProjection(
                        "s",
                        new ConcatAllProjection(
                            Name,
                            separator,
                            [new OrderItemRow(Id)],
                            over: new WindowWithFrame(
                                [new OrderItemRow(Id)],
                                new Frame(preceding: new FrameRows(new All()))
                            )
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            ),
            Bound("sep", new StringParameter(new String(" + ")))
        );
        PureQLProjection grouped = new(
            [Items],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.items"),
                groupBy:
                [
                    new GroupKey(
                        "cheap",
                        new LessThanDecimalRow(
                            new FieldDecimalNullable("t.items", "price"),
                            new ParamDecimal("limit")
                        )
                    ),
                ],
                select:
                [
                    new SelectItemGroup("cheap", new KeyBoolean("cheap")),
                    new SelectItemGroup(
                        "s",
                        new ConcatAllGroup(Name, separator, [new OrderItemRow(Id)])
                    ),
                ],
                orderBy: [new OrderItemGroupColumn("cheap")]
            ),
            new Dictionary<string, IParameter>
            {
                ["sep"] = new StringParameter(new String(";")),
                ["limit"] = new DecimalParameter(new Decimal(2.5m)),
            }
        );

        Assert.Equal(
            ["pen", "pen + ink", "pen + ink + pad"],
            ResultColumn.Texts(windowed, "s")
        );
        // Item 4's null price is not below the limit, so it is with pad, and
        // concatAll skips its null name.
        Assert.Equal(["False", "True"], ResultColumn.Texts(grouped, "cheap"));
        Assert.Equal(["pad", "pen;ink"], ResultColumn.Texts(grouped, "s"));
    }

    [Fact]
    public void ParameterIsReadInAPredicateAndInHavingOfAGroupedQuery()
    {
        PureQLProjection projection = new(
            [Items],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.items"),
                groupBy:
                [
                    new GroupKey(
                        "named",
                        new NotEqualStringRow(Name, new LiteralStringNullable())
                    ),
                ],
                having: new GreaterThanDecimalGroup(
                    new CountGroup(),
                    new ParamInteger("min")
                ),
                select:
                [
                    new SelectItemGroup("named", new KeyBoolean("named")),
                    new SelectItemGroup(
                        "above",
                        new CountGroup(
                            new GreaterThanDecimalRow(Id, new ParamInteger("min"))
                        )
                    ),
                ]
            ),
            Bound("min", new IntegerParameter(new Long(1)))
        );

        Assert.Equal(["True"], ResultColumn.Texts(projection, "named"));
        Assert.Equal(["2"], ResultColumn.Texts(projection, "above"));
    }

    // ---- Date math (the current parameters of
    // Pure.RelationalSchema.Storage.PureQL.Projection.Parameters) has its value at
    // the instant each run starts, in every construct.

    [Fact]
    public void DateMathInAnInlineRowIsBoundAgainForEveryRun()
    {
        FixedClock clock = FixedClock.At(Thursday);
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRows(
                        "since",
                        [[new RowItem("day", new ParamDate("since"))]]
                    ),
                ],
                from: new FromEntity("t.items"),
                joins:
                [
                    new JoinSubquery(
                        JoinKind.Inner,
                        "since",
                        new GreaterThanOrEqualDateRow(
                            new FieldDate("t.items", "day"),
                            new FieldDate("since", "day")
                        )
                    ),
                ],
                select: [new SelectItemProjection("id", Id)],
                orderBy: [new OrderItemProjectionColumn("id")]
            ),
            Bound("since", new TextParameter("date", "now-2d")),
            clock
        );

        Assert.Equal(["2", "3", "4"], ResultColumn.Texts(projection, "id"));
        clock.Now = clock.Now.AddDays(-2);
        Assert.Equal(["1", "2", "3", "4"], ResultColumn.Texts(projection, "id"));
    }

    [Fact]
    public void DateMathBoundsACalendarThatMovesWithTheClock()
    {
        FixedClock clock = FixedClock.At(Thursday);
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRows(
                        "first",
                        [[new RowItem("day", new ParamDate("from"))]]
                    ),
                    new SubqueryRecursive(
                        "days",
                        new Recursion(
                            seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromSubquery("first"),
                                select:
                                [
                                    new SelectItemProjection(
                                        "day",
                                        new FieldDate("first", "day")
                                    ),
                                ]
                            ),
                            next: new PlainNextQuery(
                                from: new FromSubquery("days"),
                                where: new LessThanDateRow(
                                    new FieldDate("days", "day"),
                                    new ParamDate("to")
                                ),
                                select:
                                [
                                    new SelectItemRow(
                                        "day",
                                        new DateAddDaysDateRow(
                                            new FieldDate("days", "day"),
                                            new LiteralInteger(1)
                                        )
                                    ),
                                ]
                            ),
                            maxDepth: 10L
                        )
                    ),
                ],
                from: new FromSubquery("days"),
                select: [new SelectItemProjection("day", new FieldDate("days", "day"))],
                orderBy: [new OrderItemProjectionColumn("day")]
            ),
            new Dictionary<string, IParameter>
            {
                ["from"] = new TextParameter("date", "now-2d"),
                ["to"] = new TextParameter("date", "now"),
            },
            clock
        );

        Assert.Equal(
            ["2026-10-27", "2026-10-28", "2026-10-29"],
            ResultColumn.Texts(projection, "day")
        );
        int reads = clock.Reads;
        clock.Now = clock.Now.AddDays(3);
        Assert.Equal(
            ["2026-10-30", "2026-10-31", "2026-11-01"],
            ResultColumn.Texts(projection, "day")
        );
        Assert.Equal(reads + 1, clock.Reads);
    }

    [Fact]
    public void DateMathInAnInListOverASubqueryIsBoundAgainForEveryRun()
    {
        // The subquery is uncorrelated, so a run may compute it once; the
        // next run computes it at its own instant.
        FixedClock clock = FixedClock.At(Thursday);
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "recent",
                        new PlainQueryWithoutPaginationWithoutOrderBy(
                            from: new FromEntity("t.items"),
                            where: new GreaterThanOrEqualDateRow(
                                new FieldDate("t.items", "day"),
                                new ParamDate("since")
                            ),
                            select: [new SelectItemProjection("id", Id)]
                        )
                    ),
                ],
                from: new FromEntity("t.items", "i"),
                where: new InDecimalRow(
                    new FieldInteger("i", "id"),
                    new ListSubqueryColumnInteger("recent", "id")
                ),
                select: [new SelectItemProjection("id", new FieldInteger("i", "id"))],
                orderBy: [new OrderItemProjectionColumn("id")]
            ),
            Bound("since", new TextParameter("date", "now-1d")),
            clock
        );

        Assert.Equal(["3", "4"], ResultColumn.Texts(projection, "id"));
        clock.Now = clock.Now.AddDays(1);
        Assert.Equal(["4"], ResultColumn.Texts(projection, "id"));
        clock.Now = clock.Now.AddDays(1);
        Assert.Empty(ResultColumn.Texts(projection, "id"));
    }

    [Fact]
    public void DateMathInAWindowAndInBothInputsOfAUnionIsReadAtOneInstant()
    {
        FixedClock clock = FixedClock.At(Thursday);
        PureQLProjection projection = new(
            [Items],
            new MainCombinedQueryUnionAll([
                new PlainQueryWithoutPaginationWithoutOrderBy(
                    from: new FromEntity("t.items"),
                    where: new GreaterThanOrEqualDateRow(
                        new FieldDate("t.items", "day"),
                        new ParamDate("since")
                    ),
                    select:
                    [
                        new SelectItemProjection("kind", new LiteralString("recent")),
                        new SelectItemProjection(
                            "n",
                            new CountProjection(
                                over: new WindowWithoutFrameWithoutRange([
                                    new LessThanDateRow(
                                        new FieldDate("t.items", "day"),
                                        new ParamDate("since")
                                    ),
                                ])
                            )
                        ),
                    ]
                ),
                new TotalQuery(
                    from: new FromEntity("t.items"),
                    where: new LessThanDateRow(
                        new FieldDate("t.items", "day"),
                        new ParamDate("since")
                    ),
                    aggregate:
                    [
                        new SelectItemTotal("kind", new LiteralString("older")),
                        new SelectItemTotal("n", new CountTotal()),
                    ]
                ),
            ]),
            Bound("since", new TextParameter("date", "now-1d")),
            clock
        );

        Assert.Equal(
            ["older|2", "recent|2", "recent|2"],
            Lines(projection).Order(StringComparer.Ordinal)
        );
        clock.Now = clock.Now.AddDays(-1);
        Assert.Equal(
            ["older|1", "recent|3", "recent|3", "recent|3"],
            Lines(projection).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void ProviderIsGivenTheDateMathOfAUnionAtTheInstantEachRunStarts()
    {
        RecordingProvider provider = new();
        FixedClock clock = FixedClock.At(Thursday);
        PureQLProjection projection = new(
            [ProvidedTable.Schema(Items, provider)],
            new MainCombinedQueryUnion([
                new PlainQueryWithoutPaginationWithoutOrderBy(
                    from: new FromEntity("t.items"),
                    where: new GreaterThanOrEqualDateRow(
                        new FieldDate("t.items", "day"),
                        new ParamDate("since")
                    ),
                    select: [new SelectItemProjection("id", Id)]
                ),
                new PlainQueryWithoutPaginationWithoutOrderBy(
                    from: new FromEntity("t.items"),
                    where: new EqualDecimalRow(Id, new LiteralInteger(1)),
                    select: [new SelectItemProjection("id", Id)]
                ),
            ]),
            Bound("since", new TextParameter("date", "now")),
            clock
        );

        Assert.Equal(
            ["1", "4"],
            ResultColumn.Texts(projection, "id").Order(StringComparer.Ordinal)
        );
        Assert.Equal(QueryExecution.Translated, projection.Execution);
        clock.Now = clock.Now.AddDays(-2);
        Assert.Equal(
            ["1", "2", "3", "4"],
            ResultColumn.Texts(projection, "id").Order(StringComparer.Ordinal)
        );
        Assert.Equal(2, provider.Queries.Count);
    }

    [Theory]
    [InlineData(null, "")]
    [InlineData("ink", "ink")]
    public void TypedNullInAnInputOfAnIntersectMeetsANullName(string? value, string row)
    {
        IParameter bound = value is null
            ? new NullableStringParameter()
            : new StringParameter(new String(value));
        PureQLProjection projection = new(
            [Items],
            new MainCombinedQueryIntersect([
                NamesWhere(null),
                One("name", new ParamStringNullable("v")),
            ]),
            Bound("v", bound)
        );

        Assert.Equal([row], ResultColumn.Texts(projection, "name"));
    }

    [Fact]
    public void NullIsInTheColumnOfAUnionThatATypedNullParameterFills()
    {
        // Null semantics: "A null value is in a list exactly when the list
        // contains null, which only a nullable subquery column can".
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "u",
                        new CombinedQueryUnionAll([
                            One("x", new ParamIntegerNullable("p")),
                            One("x", new LiteralInteger(1)),
                        ])
                    ),
                ],
                from: new FromEntity("t.items"),
                where: new EqualDecimalRow(Id, new LiteralInteger(1)),
                select:
                [
                    new SelectItemProjection(
                        "in",
                        new InDecimalProjection(
                            new ParamIntegerNullable("q"),
                            new ListSubqueryColumnIntegerNullable("u", "x")
                        )
                    ),
                    new SelectItemProjection(
                        "notIn",
                        new NotProjection(
                            new InDecimalProjection(
                                new ParamIntegerNullable("q"),
                                new ListSubqueryColumnIntegerNullable("u", "x")
                            )
                        )
                    ),
                ]
            ),
            new Dictionary<string, IParameter>
            {
                ["p"] = new NullableIntegerParameter(),
                ["q"] = new NullableIntegerParameter(),
            }
        );

        Assert.Equal(["True"], ResultColumn.Texts(projection, "in"));
        Assert.Equal(["False"], ResultColumn.Texts(projection, "notIn"));
    }

    [Fact]
    public void FrameParameterIsReadOverTheRowsOfACombinedSubquery()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "u",
                        new CombinedQueryUnionAll([
                            new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.items"),
                                where: new LessThanDecimalRow(Id, new LiteralInteger(3)),
                                select: [new SelectItemProjection("x", Id)]
                            ),
                            new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.items"),
                                where: new GreaterThanDecimalRow(
                                    Id,
                                    new LiteralInteger(2)
                                ),
                                select:
                                [
                                    new SelectItemProjection(
                                        "x",
                                        new AddIntegerProjection([
                                            Id,
                                            new LiteralInteger(10),
                                        ])
                                    ),
                                ]
                            ),
                        ])
                    ),
                ],
                from: new FromSubquery("u"),
                select:
                [
                    new SelectItemProjection("x", new FieldInteger("u", "x")),
                    new SelectItemProjection(
                        "s",
                        new SumIntegerProjection(
                            new FieldInteger("u", "x"),
                            over: new WindowWithFrame(
                                [new OrderItemRow(new FieldInteger("u", "x"))],
                                new Frame(preceding: new ParamInteger("n"))
                            )
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionColumn("x")]
            ),
            Bound("n", new IntegerParameter(new Long(1)))
        );

        Assert.Equal(["1", "2", "13", "14"], ResultColumn.Texts(projection, "x"));
        Assert.Equal(["1", "3", "15", "27"], ResultColumn.Texts(projection, "s"));
    }

    [Fact]
    public void RecursionSeededFromAUnionOfParametersReturnsEveryRowOfEveryLevel()
    {
        // Level 0 is 1 and 2, level 1 is 2 and 3, level 2 is 3: duplicates
        // are returned, as unionAll would give them.
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRecursive(
                        "steps",
                        new Recursion(
                            seed: new CombinedQueryUnion([
                                One("x", new ParamInteger("a")),
                                One("x", new ParamInteger("b")),
                            ]),
                            next: new PlainNextQuery(
                                from: new FromSubquery("steps"),
                                where: new LessThanDecimalRow(
                                    new FieldInteger("steps", "x"),
                                    new LiteralInteger(3)
                                ),
                                select:
                                [
                                    new SelectItemRow(
                                        "x",
                                        new AddIntegerRow([
                                            new FieldInteger("steps", "x"),
                                            new LiteralInteger(1),
                                        ])
                                    ),
                                ]
                            ),
                            maxDepth: 5L
                        )
                    ),
                ],
                from: new FromSubquery("steps"),
                select: [new SelectItemProjection("x", new FieldInteger("steps", "x"))],
                orderBy: [new OrderItemProjectionColumn("x")]
            ),
            new Dictionary<string, IParameter>
            {
                ["a"] = new IntegerParameter(new Long(1)),
                ["b"] = new IntegerParameter(new Long(2)),
            }
        );

        Assert.Equal(["1", "2", "2", "3", "3"], ResultColumn.Texts(projection, "x"));
    }

    [Fact]
    public void InlineRowsOfParametersExceptTheNamesOfATable()
    {
        PureQLProjection projection = new(
            [Items],
            new MainCombinedQueryExcept(
                [
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromSubquery("wanted"),
                        select:
                        [
                            new SelectItemProjection(
                                "name",
                                new FieldString("wanted", "name")
                            ),
                        ]
                    ),
                    NamesWhere(null),
                ],
                [
                    new SubqueryRows(
                        "wanted",
                        [
                            [new RowItem("name", new ParamString("a"))],
                            [new RowItem("name", new ParamString("b"))],
                            [new RowItem("name", new ParamString("c"))],
                        ]
                    ),
                ]
            ),
            new Dictionary<string, IParameter>
            {
                ["a"] = new StringParameter(new String("pen")),
                ["b"] = new StringParameter(new String("zzz")),
                ["c"] = new StringParameter(new String("ink")),
            }
        );

        Assert.Equal(["zzz"], ResultColumn.Texts(projection, "name"));
    }

    [Theory]
    [InlineData(Refusal.Never)]
    [InlineData(Refusal.OnFirstValue)]
    public void ProviderRunsADateMathCalendarAtTheInstantEachRunStarts(Refusal refusal)
    {
        RecordingProvider provider = new(refusal);
        FixedClock clock = FixedClock.At(Thursday);
        PureQLProjection projection = new(
            [ProvidedTable.Schema(Items, provider)],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRecursive(
                        "days",
                        new Recursion(
                            seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.items"),
                                where: new EqualDecimalRow(Id, new LiteralInteger(1)),
                                select:
                                [
                                    new SelectItemProjection(
                                        "day",
                                        new ParamDate("from")
                                    ),
                                ]
                            ),
                            next: new PlainNextQuery(
                                from: new FromSubquery("days"),
                                where: new LessThanDateRow(
                                    new FieldDate("days", "day"),
                                    new ParamDate("to")
                                ),
                                select:
                                [
                                    new SelectItemRow(
                                        "day",
                                        new DateAddDaysDateRow(
                                            new FieldDate("days", "day"),
                                            new LiteralInteger(1)
                                        )
                                    ),
                                ]
                            ),
                            maxDepth: 10L
                        )
                    ),
                ],
                from: new FromSubquery("days"),
                select: [new SelectItemProjection("day", new FieldDate("days", "day"))],
                orderBy: [new OrderItemProjectionColumn("day")]
            ),
            new Dictionary<string, IParameter>
            {
                ["from"] = new TextParameter("date", "now-1d"),
                ["to"] = new TextParameter("date", "now+1d"),
            },
            clock
        );

        Assert.Equal(
            ["2026-10-28", "2026-10-29", "2026-10-30"],
            ResultColumn.Texts(projection, "day")
        );
        clock.Now = clock.Now.AddMonths(1);
        Assert.Equal(
            ["2026-11-28", "2026-11-29", "2026-11-30"],
            ResultColumn.Texts(projection, "day")
        );
        Assert.Equal(
            refusal == Refusal.Never
                ? QueryExecution.Translated
                : QueryExecution.InMemory,
            projection.Execution
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void WindowFunctionsAreComputedBeforeAPageOfParameters(bool provided)
    {
        // Query clauses: "Aggregates over all rows and window functions are
        // computed before distinctBy and pagination".
        IStoredSchemaDataSet items = provided
            ? ProvidedTable.Schema(Items, new RecordingProvider())
            : Items;
        PureQLProjection projection = new(
            [items],
            new MainPlainQueryWithPagination(
                from: new FromEntity("t.items"),
                select:
                [
                    new SelectItemProjection("id", Id),
                    new SelectItemProjection(
                        "n",
                        new RowNumberProjection(new OrderedWindow([new OrderItemRow(Id)]))
                    ),
                    new SelectItemProjection("count", new CountProjection()),
                ],
                orderBy:
                [
                    new OrderItemProjectionExpression(
                        new MultiplyIntegerProjection([Id, new ParamInteger("sign")])
                    ),
                ],
                pagination: new Pagination(
                    skip: new ParamInteger("skip"),
                    take: new ParamInteger("take")
                )
            ),
            new Dictionary<string, IParameter>
            {
                ["sign"] = new IntegerParameter(new Long(-1)),
                ["skip"] = new IntegerParameter(new Long(1)),
                ["take"] = new IntegerParameter(new Long(2)),
            }
        );

        Assert.Equal(["3", "2"], ResultColumn.Texts(projection, "id"));
        Assert.Equal(["3", "2"], ResultColumn.Texts(projection, "n"));
        Assert.Equal(["4", "4"], ResultColumn.Texts(projection, "count"));
        Assert.Equal(
            provided ? QueryExecution.Translated : QueryExecution.InMemory,
            projection.Execution
        );
    }

    // ---- Helpers.

    private static ExprProjection Param(string type)
    {
        return type switch
        {
            "integer" => new ParamInteger("p"),
            "decimal" => new ParamDecimal("p"),
            "date" => new ParamDate("p"),
            "time" => new ParamTime("p"),
            "datetime" => new ParamDatetime("p"),
            _ => new ParamUuid("p"),
        };
    }

    // The value of the parameter p, bound to text of a type, on one row.
    private static IReadOnlyList<string> Selected(
        ExprProjection parameter,
        string type,
        string text
    )
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items"),
                where: new EqualDecimalRow(Id, new LiteralInteger(1)),
                select: [new SelectItemProjection("p", parameter)]
            ),
            Bound("p", new TextParameter(type, text))
        );

        return ResultColumn.Texts(projection, "p");
    }

    private static Dictionary<string, IParameter> Bound(string name, IParameter value)
    {
        return new Dictionary<string, IParameter> { [name] = value };
    }

    private static StringListParameter Strings(params string[] values)
    {
        return new StringListParameter(
            [.. values.Select(value => (IString)new String(value))]
        );
    }

    // The ids of the items where the condition holds, l bound to a list.
    private static IReadOnlyList<string> Ids(BooleanRow where, IParameter list)
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items"),
                where: where,
                select: [new SelectItemProjection("id", Id)],
                orderBy: [new OrderItemProjectionColumn("id")]
            ),
            Bound("l", list)
        );

        return ResultColumn.Texts(projection, "id");
    }

    private static Query NamesWhere(BooleanRow? where)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.items"),
            where: where,
            select: [new SelectItemProjection("name", Name)]
        );
    }

    // One row of one column.
    private static Query One(string alias, ExprProjection value)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.items"),
            where: new EqualDecimalRow(Id, new LiteralInteger(1)),
            select: [new SelectItemProjection(alias, value)]
        );
    }

    // steps: start, then x + amount while the condition holds of x.
    private static PureQLProjection Steps(
        BooleanRow condition,
        IntegerRow amount,
        IReadOnlyDictionary<string, IParameter> values,
        long start = 1
    )
    {
        return new PureQLProjection(
            [Items],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRecursive(
                        "steps",
                        new Recursion(
                            seed: One("x", new LiteralInteger(start)),
                            next: new PlainNextQuery(
                                from: new FromSubquery("steps"),
                                where: condition,
                                select:
                                [
                                    new SelectItemRow(
                                        "x",
                                        new AddIntegerRow([
                                            new FieldInteger("steps", "x"),
                                            amount,
                                        ])
                                    ),
                                ]
                            ),
                            maxDepth: 10L
                        )
                    ),
                ],
                from: new FromSubquery("steps"),
                select: [new SelectItemProjection("x", new FieldInteger("steps", "x"))],
                orderBy: [new OrderItemProjectionColumn("x")]
            ),
            values
        );
    }

    private static IReadOnlyList<string> Lines(PureQLProjection projection)
    {
        IReadOnlyList<string> columns =
        [
            .. projection.TableSchema.Columns.Select(column => column.Name.TextValue),
        ];

        return
        [
            .. projection.AsEnumerable().Select(row =>
                string.Join(
                    "|",
                    columns.Select(column =>
                        row.Cells.Single(cell =>
                            cell.Key.Name.TextValue == column
                        ).Value.Value.TextValue
                    )
                )
            ),
        ];
    }
}
