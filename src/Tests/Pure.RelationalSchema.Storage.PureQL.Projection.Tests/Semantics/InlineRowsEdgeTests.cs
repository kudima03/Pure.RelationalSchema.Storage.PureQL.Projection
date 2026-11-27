using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
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
using InlineCell = PureQL.CSharp.Model.Cell;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Edge cases of inline rows, by the specification's Subqueries (inline
// rows: cells are typed literals or parameters, a column takes the least
// type of its cells, one row for each row written) and Semantics (joins,
// equality and aggregates as over any other source).
public sealed class InlineRowsEdgeTests
{
    // n: 1, null, 3, null; d: 1.0, 2.5, 3, null.
    private static readonly IStoredSchemaDataSet Stock = new InlineTable(
        "t",
        "stock",
        ("id", new LongColumnType()),
        ("n", new LongColumnType()),
        ("d", new DoubleColumnType()),
        ("at", new DateTimeColumnType())
    ).With(
        ["1", "1", "1.0", "2024-01-01T00:00:00Z"],
        ["2", "", "2.5", "2024-01-02T00:00:00Z"],
        ["3", "3", "3", "2024-01-03T00:00:00Z"],
        ["4", "", "", ""]
    );

    [Fact]
    public void AnIntegerColumnJoinsAStoredDecimalColumnByValue()
    {
        // Integer and decimal arithmetic: 1 equals 1.0.
        AssertRows(
            Joined(
                Values(new LiteralInteger(1), new LiteralInteger(3)),
                JoinKind.Inner,
                new EqualDecimalRow(
                    new FieldDecimalNullable("t.stock", "d"),
                    new FieldInteger("list", "v")
                ),
                new FieldInteger("list", "v")
            ),
            "1|1",
            "3|3"
        );
    }

    [Fact]
    public void ADecimalColumnJoinsAStoredIntegerColumnByValue()
    {
        AssertRows(
            Joined(
                Values(
                    new LiteralDecimal(1m),
                    new LiteralDecimal(2.5m),
                    new LiteralInteger(3)
                ),
                JoinKind.Inner,
                new EqualDecimalRow(
                    new FieldIntegerNullable("t.stock", "n"),
                    new FieldDecimal("list", "v")
                ),
                new FieldDecimal("list", "v")
            ),
            "1|1",
            "3|3"
        );
    }

    [Fact]
    public void ANullCellJoinsTheNullsOfAStoredColumn()
    {
        // Null semantics: equal pairs two nulls, also in join.on.
        AssertRows(
            Joined(
                Values(new LiteralIntegerNullable(), new LiteralInteger(1)),
                JoinKind.Inner,
                new EqualDecimalRow(
                    new FieldIntegerNullable("t.stock", "n"),
                    new FieldIntegerNullable("list", "v")
                ),
                new FieldIntegerNullable("list", "v")
            ),
            "1|1",
            "2|",
            "4|"
        );
    }

    [Fact]
    public void ANotEqualNullGuardDropsTheNullPairs()
    {
        AssertRows(
            Joined(
                Values(new LiteralIntegerNullable(), new LiteralInteger(1)),
                JoinKind.Inner,
                new AndRow([
                    new EqualDecimalRow(
                        new FieldIntegerNullable("t.stock", "n"),
                        new FieldIntegerNullable("list", "v")
                    ),
                    new NotEqualDecimalRow(
                        new FieldIntegerNullable("list", "v"),
                        new LiteralIntegerNullable()
                    ),
                ]),
                new FieldIntegerNullable("list", "v")
            ),
            "1|1"
        );
    }

    [Fact]
    public void ALeftJoinOfInlineRowsWithoutAMatchGivesNull()
    {
        AssertRows(
            Joined(
                Values(new LiteralInteger(3), new LiteralInteger(9)),
                JoinKind.Left,
                new EqualDecimalRow(
                    new FieldIntegerNullable("t.stock", "n"),
                    new FieldInteger("list", "v")
                ),
                new FieldIntegerNullable("list", "v")
            ),
            "1|",
            "2|",
            "3|3",
            "4|"
        );
    }

    [Fact]
    public void AFullJoinWithInlineRowsKeepsBothUnmatchedSides()
    {
        AssertRows(
            new PureQLProjection(
                [Stock],
                new MainPlainQueryWithoutPagination(
                    subqueries: [Values(new LiteralInteger(3), new LiteralInteger(9))],
                    from: new FromEntity("t.stock"),
                    joins:
                    [
                        new JoinSubquery(
                            JoinKind.Full,
                            "list",
                            new EqualDecimalRow(
                                new FieldIntegerNullable("t.stock", "n"),
                                new FieldInteger("list", "v")
                            )
                        ),
                    ],
                    select:
                    [
                        new SelectItemProjection(
                            "id",
                            new FieldIntegerNullable("t.stock", "id")
                        ),
                        new SelectItemProjection(
                            "v",
                            new FieldIntegerNullable("list", "v")
                        ),
                    ],
                    orderBy:
                    [
                        new OrderItemProjectionColumn("id"),
                        new OrderItemProjectionColumn("v"),
                    ]
                )
            ),
            "|9",
            "1|",
            "2|",
            "3|3",
            "4|"
        );
    }

    [Fact]
    public void AggregatesRunOverInlineRowsAsOverAnyRows()
    {
        // 1, 2, 2 and null: the average is 5 / 3 by divide, rounded half
        // away from zero to 28 significant digits (Integer and decimal
        // arithmetic).
        AssertRows(
            new PureQLProjection(
                [Stock],
                new MainTotalQuery(
                    subqueries:
                    [
                        Values(
                            new LiteralInteger(1),
                            new LiteralInteger(2),
                            new LiteralInteger(2),
                            new LiteralIntegerNullable()
                        ),
                    ],
                    from: new FromSubquery("list"),
                    aggregate:
                    [
                        new SelectItemTotal("rows", new CountTotal()),
                        new SelectItemTotal(
                            "distinct",
                            new CountDistinctTotal(new FieldIntegerNullable("list", "v"))
                        ),
                        new SelectItemTotal(
                            "sum",
                            new SumIntegerTotal(new FieldIntegerNullable("list", "v"))
                        ),
                        new SelectItemTotal(
                            "min",
                            new MinIntegerNullableTotal(
                                new FieldIntegerNullable("list", "v")
                            )
                        ),
                        new SelectItemTotal(
                            "max",
                            new MaxIntegerNullableTotal(
                                new FieldIntegerNullable("list", "v")
                            )
                        ),
                        new SelectItemTotal(
                            "average",
                            new AverageDecimalNullableTotal(
                                new FieldIntegerNullable("list", "v")
                            )
                        ),
                        new SelectItemTotal(
                            "median",
                            new MedianDecimalNullableTotal(
                                new FieldIntegerNullable("list", "v")
                            )
                        ),
                    ]
                )
            ),
            "4|2|5|1|2|1." + new string('6', 26) + "7|2"
        );
    }

    [Fact]
    public void TheMinAndMaxOfStringCellsAreByCodePoint()
    {
        // B (66) < Z (90) < a (97) < é (233).
        AssertRows(
            new PureQLProjection(
                [Stock],
                new MainTotalQuery(
                    subqueries:
                    [
                        Values(
                            new LiteralString("a"),
                            new LiteralString("B"),
                            new LiteralString("é"),
                            new LiteralString("Z")
                        ),
                    ],
                    from: new FromSubquery("list"),
                    aggregate:
                    [
                        new SelectItemTotal(
                            "min",
                            new MinStringNullableTotal(new FieldString("list", "v"))
                        ),
                        new SelectItemTotal(
                            "max",
                            new MaxStringNullableTotal(new FieldString("list", "v"))
                        ),
                    ]
                )
            ),
            "B|é"
        );
    }

    [Fact]
    public void AnIntegerAndADecimalCellOfOneValueAreOneGroup()
    {
        // Evaluation: 1 and 1.0 are the same decimal key.
        AssertRows(
            new PureQLProjection(
                [Stock],
                new MainGroupedQueryWithoutPagination(
                    subqueries:
                    [
                        Values(
                            new LiteralInteger(2),
                            new LiteralDecimal(2m),
                            new LiteralDecimal(0.5m)
                        ),
                    ],
                    from: new FromSubquery("list"),
                    groupBy: [new GroupKey("v", new FieldDecimal("list", "v"))],
                    select:
                    [
                        new SelectItemGroup("v", new KeyDecimal("v")),
                        new SelectItemGroup("rows", new CountGroup()),
                    ],
                    orderBy: [new OrderItemGroupColumn("v")]
                )
            ),
            "0.5|1",
            "2|2"
        );
    }

    [Fact]
    public void AnIntegerAndADecimalCellOfOneValueAreCountedOnceAsDistinct()
    {
        AssertRows(
            new PureQLProjection(
                [Stock],
                new MainTotalQuery(
                    subqueries:
                    [
                        Values(
                            new LiteralInteger(2),
                            new LiteralDecimal(2m),
                            new LiteralDecimal(0.5m)
                        ),
                    ],
                    from: new FromSubquery("list"),
                    aggregate:
                    [
                        new SelectItemTotal(
                            "distinct",
                            new CountDistinctTotal(new FieldDecimal("list", "v"))
                        ),
                    ]
                )
            ),
            "2"
        );
    }

    [Fact]
    public void AnEmptyStringCellIsAValueAndNotNull()
    {
        Subquery rows = Values(
            new LiteralString(""),
            new LiteralStringNullable(),
            new LiteralString(""),
            new LiteralString("a")
        );

        AssertRows(
            new PureQLProjection(
                [Stock],
                new MainTotalQuery(
                    subqueries: [rows],
                    from: new FromSubquery("list"),
                    aggregate:
                    [
                        new SelectItemTotal("rows", new CountTotal()),
                        new SelectItemTotal(
                            "nulls",
                            new CountTotal(
                                new EqualStringRow(
                                    new FieldStringNullable("list", "v"),
                                    new LiteralStringNullable()
                                )
                            )
                        ),
                        new SelectItemTotal(
                            "empty",
                            new CountTotal(
                                new EqualStringRow(
                                    new FieldStringNullable("list", "v"),
                                    new LiteralString("")
                                )
                            )
                        ),
                        new SelectItemTotal(
                            "distinct",
                            new CountDistinctTotal(new FieldStringNullable("list", "v"))
                        ),
                    ]
                )
            ),
            "4|1|2|2"
        );
    }

    [Fact]
    public void AnEmptyStringCellAndANullCellAreTwoGroups()
    {
        AssertRows(
            new PureQLProjection(
                [Stock],
                new MainGroupedQueryWithoutPagination(
                    subqueries:
                    [
                        Values(
                            new LiteralString(""),
                            new LiteralStringNullable(),
                            new LiteralString(""),
                            new LiteralString("a")
                        ),
                    ],
                    from: new FromSubquery("list"),
                    groupBy: [new GroupKey("v", new FieldStringNullable("list", "v"))],
                    select:
                    [
                        new SelectItemGroup("v", new KeyStringNullable("v")),
                        new SelectItemGroup("rows", new CountGroup()),
                    ],
                    orderBy: [new OrderItemGroupColumn("v")]
                )
            ),
            "|1",
            "|2",
            "a|1"
        );
    }

    [Fact]
    public void ExtremeNumbersAreHeldExactly()
    {
        AssertRows(
            new PureQLProjection(
                [Stock],
                new MainPlainQueryWithoutPagination(
                    subqueries:
                    [
                        new SubqueryRows(
                            "one",
                            [
                                [
                                    new RowItem("min", new LiteralInteger(long.MinValue)),
                                    new RowItem("max", new LiteralInteger(long.MaxValue)),
                                    new RowItem(
                                        "tiny",
                                        new LiteralDecimal(0.0000000000000000000000000001m)
                                    ),
                                    new RowItem(
                                        "huge",
                                        new LiteralDecimal(9999999999999999999999999999m)
                                    ),
                                    new RowItem(
                                        "low",
                                        new LiteralDecimal(-9999999999999999999999999999m)
                                    ),
                                ],
                            ]
                        ),
                    ],
                    from: new FromSubquery("one"),
                    select:
                    [
                        new SelectItemProjection("min", new FieldInteger("one", "min")),
                        new SelectItemProjection("max", new FieldInteger("one", "max")),
                        new SelectItemProjection("tiny", new FieldDecimal("one", "tiny")),
                        new SelectItemProjection("huge", new FieldDecimal("one", "huge")),
                        new SelectItemProjection("low", new FieldDecimal("one", "low")),
                    ]
                )
            ),
            "-9223372036854775808|9223372036854775807|0.0000000000000000000000000001"
                + "|9999999999999999999999999999|-9999999999999999999999999999"
        );
    }

    [Fact]
    public void TheLargestIntegerCellOfADecimalColumnIsExact()
    {
        AssertRows(
            new PureQLProjection(
                [Stock],
                new MainTotalQuery(
                    subqueries:
                    [
                        Values(new LiteralInteger(long.MaxValue), new LiteralDecimal(0.5m)),
                    ],
                    from: new FromSubquery("list"),
                    aggregate:
                    [
                        new SelectItemTotal(
                            "sum",
                            new SumDecimalTotal(new FieldDecimal("list", "v"))
                        ),
                        new SelectItemTotal(
                            "max",
                            new MaxDecimalNullableTotal(new FieldDecimal("list", "v"))
                        ),
                    ]
                )
            ),
            "9223372036854775807.5|9223372036854775807"
        );
    }

    [Fact]
    public void TemporalCellsKeepTheirBoundsAndMicroseconds()
    {
        AssertRows(
            new PureQLProjection(
                [Stock],
                new MainPlainQueryWithoutPagination(
                    subqueries:
                    [
                        new SubqueryRows(
                            "one",
                            [
                                [
                                    new RowItem(
                                        "date",
                                        new LiteralDate(new DateOnly(1, 1, 1))
                                    ),
                                    new RowItem(
                                        "time",
                                        new LiteralTime(new TimeOnly(23, 59, 59, 999, 999))
                                    ),
                                    new RowItem(
                                        "datetime",
                                        new LiteralDatetime(
                                            new DateTimeOffset(
                                                9999,
                                                12,
                                                31,
                                                23,
                                                59,
                                                59,
                                                999,
                                                999,
                                                TimeSpan.Zero
                                            )
                                        )
                                    ),
                                ],
                            ]
                        ),
                    ],
                    from: new FromSubquery("one"),
                    select:
                    [
                        new SelectItemProjection("date", new FieldDate("one", "date")),
                        new SelectItemProjection("time", new FieldTime("one", "time")),
                        new SelectItemProjection(
                            "datetime",
                            new FieldDatetime("one", "datetime")
                        ),
                    ]
                )
            ),
            "0001-01-01|23:59:59.999999|9999-12-31T23:59:59.999999Z"
        );
    }

    [Fact]
    public void CellsInAnotherOrderTakeTheLeastTypeOfTheirAlias()
    {
        Subquery rows = new SubqueryRows(
            "pairs",
            [
                [
                    new RowItem("a", new LiteralInteger(1)),
                    new RowItem("b", new LiteralString("x")),
                ],
                [
                    new RowItem("b", new LiteralStringNullable()),
                    new RowItem("a", new LiteralDecimal(0.5m)),
                ],
            ]
        );

        AssertRows(
            ReadingPairs(
                rows,
                new FieldDecimal("pairs", "a"),
                new FieldStringNullable("pairs", "b")
            ),
            "0.5|",
            "1|x"
        );
        _ = Assert.Throws<ArgumentException>(() =>
            ReadingPairs(
                rows,
                new FieldInteger("pairs", "a"),
                new FieldStringNullable("pairs", "b")
            )
        );
        _ = Assert.Throws<ArgumentException>(() =>
            ReadingPairs(
                rows,
                new FieldDecimal("pairs", "a"),
                new FieldString("pairs", "b")
            )
        );
    }

    [Theory]
    [InlineData("union", "a", "b")]
    [InlineData("unionAll", "a", "a", "a", "a", "b", "b")]
    [InlineData("intersect", "a", "b")]
    [InlineData("except")]
    public void InlineRowsCombinedWithThemselves(
        string operation,
        params string[] expected
    )
    {
        // The rows a, b, a: each input reads them once, on its own.
        Query input = new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromSubquery("list"),
            select: [new SelectItemProjection("v", new FieldString("list", "v"))]
        );
        Subquery[] subqueries =
        [
            Values(new LiteralString("a"), new LiteralString("b"), new LiteralString("a")),
        ];
        PureQLQuery query = operation switch
        {
            "union" => new MainCombinedQueryUnion([input, input], subqueries),
            "unionAll" => new MainCombinedQueryUnionAll([input, input], subqueries),
            "intersect" => new MainCombinedQueryIntersect([input, input], subqueries),
            _ => new MainCombinedQueryExcept([input, input], subqueries),
        };

        Assert.Equal(
            expected.AsEnumerable(),
            Texts(new PureQLProjection([Stock], query)).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void ADatetimeColumnIsTheListOfIn()
    {
        // A null operand is not in a column without a null cell.
        AssertRows(
            new PureQLProjection(
                [Stock],
                new MainPlainQueryWithoutPagination(
                    subqueries:
                    [
                        Values(
                            new LiteralDatetime(
                                new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
                            ),
                            new LiteralDatetime(
                                new DateTimeOffset(2024, 1, 3, 0, 0, 0, TimeSpan.Zero)
                            )
                        ),
                    ],
                    from: new FromEntity("t.stock"),
                    where: new InDatetimeRow(
                        new FieldDatetimeNullable("t.stock", "at"),
                        new ListSubqueryColumnDatetime("list", "v")
                    ),
                    select:
                    [
                        new SelectItemProjection("id", new FieldInteger("t.stock", "id")),
                    ],
                    orderBy: [new OrderItemProjectionColumn("id")]
                )
            ),
            "1",
            "3"
        );
    }

    [Fact]
    public void AnIntegerParameterCellIsTheListOfADecimalOperand()
    {
        // d: 1.0, 2.5, 3, null; the cells 3 and a parameter bound to 1.
        AssertRows(
            new PureQLProjection(
                [Stock],
                new MainPlainQueryWithoutPagination(
                    subqueries:
                    [
                        Values(new LiteralInteger(3), new ParamInteger("p")),
                    ],
                    from: new FromEntity("t.stock"),
                    where: new InDecimalRow(
                        new FieldDecimalNullable("t.stock", "d"),
                        new ListSubqueryColumnInteger("list", "v")
                    ),
                    select:
                    [
                        new SelectItemProjection("id", new FieldInteger("t.stock", "id")),
                    ],
                    orderBy: [new OrderItemProjectionColumn("id")]
                ),
                new Dictionary<string, IParameter>
                {
                    ["p"] = new TextParameter("integer", "1"),
                }
            ),
            "1",
            "3"
        );
    }

    [Fact]
    public void ADateMathCellIsBoundAgainWhenEachRunStarts()
    {
        FixedClock clock = FixedClock.At("2026-10-29T08:30:00Z");
        PureQLProjection projection = new(
            [Stock],
            new MainPlainQueryWithoutPagination(
                subqueries: [Values(new ParamDate("d"), new LiteralDate(new DateOnly(2000, 1, 1)))],
                from: new FromSubquery("list"),
                select: [new SelectItemProjection("v", new FieldDate("list", "v"))],
                orderBy: [new OrderItemProjectionColumn("v")]
            ),
            new Dictionary<string, IParameter> { ["d"] = new TextParameter("date", "now-1d") },
            clock
        );

        Assert.Equal(["2000-01-01", "2026-10-28"], Texts(projection));
        clock.Now = new DateTimeOffset(2026, 11, 1, 12, 0, 0, TimeSpan.Zero);
        Assert.Equal(["2000-01-01", "2026-10-31"], Texts(projection));
    }

    [Fact]
    public void ATotalQueryOverInlineRowsThatWhereEmptiesReturnsItsRow()
    {
        // aggregate: with no rows count and sum are 0, min and max null.
        AssertRows(
            new PureQLProjection(
                [Stock],
                new MainTotalQuery(
                    subqueries: [Values(new LiteralInteger(1), new LiteralInteger(2))],
                    from: new FromSubquery("list"),
                    where: new GreaterThanDecimalRow(
                        new FieldInteger("list", "v"),
                        new LiteralInteger(5)
                    ),
                    aggregate:
                    [
                        new SelectItemTotal("rows", new CountTotal()),
                        new SelectItemTotal(
                            "sum",
                            new SumIntegerTotal(new FieldInteger("list", "v"))
                        ),
                        new SelectItemTotal(
                            "min",
                            new MinIntegerNullableTotal(new FieldInteger("list", "v"))
                        ),
                        new SelectItemTotal(
                            "max",
                            new MaxIntegerNullableTotal(new FieldInteger("list", "v"))
                        ),
                    ]
                )
            ),
            "0|0||"
        );
    }

    [Fact]
    public void AliasesThatDifferInCaseAreDifferentColumns()
    {
        // Names and sources: names compare exactly, code point by code
        // point, so the rows do not have the same aliases.
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Stock],
                new MainPlainQueryWithoutPagination(
                    subqueries:
                    [
                        new SubqueryRows(
                            "list",
                            [
                                [new RowItem("v", new LiteralInteger(1))],
                                [new RowItem("V", new LiteralInteger(2))],
                            ]
                        ),
                    ],
                    from: new FromSubquery("list"),
                    select: [new SelectItemProjection("v", new FieldInteger("list", "v"))]
                )
            )
        );
    }

    [Fact]
    public void AParameterOfACellHasOneNullabilityInTheWholeDocument()
    {
        // One parameter name has one type in the whole document; integer
        // and nullable integer are two types.
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Stock],
                new MainPlainQueryWithoutPagination(
                    subqueries: [Values(new ParamInteger("p"))],
                    from: new FromSubquery("list"),
                    where: new EqualDecimalRow(
                        new FieldInteger("list", "v"),
                        new ParamIntegerNullable("p")
                    ),
                    select: [new SelectItemProjection("v", new FieldInteger("list", "v"))]
                ),
                new Dictionary<string, IParameter>
                {
                    ["p"] = new TextParameter("integer", "2"),
                }
            )
        );
    }

    [Fact]
    public void ADatetimeParameterCellBoundToNowIsTruncatedToTheMicrosecond()
    {
        // Date math: the instant is truncated to the microsecond.
        PureQLProjection projection = new(
            [Stock],
            new MainPlainQueryWithoutPagination(
                subqueries: [Values(new ParamDatetime("at"))],
                from: new FromSubquery("list"),
                select: [new SelectItemProjection("v", new FieldDatetime("list", "v"))]
            ),
            new Dictionary<string, IParameter>
            {
                ["at"] = new TextParameter("datetime", "now-1h"),
            },
            FixedClock.At("2026-10-29T08:30:15.1234567Z")
        );

        Assert.Equal(["2026-10-29T07:30:15.123456Z"], Texts(projection));
    }

    [Fact]
    public void AStringParameterCellAndAnIntegerCellDoNotMeet()
    {
        AssertRejected(
            Values(new ParamString("p"), new LiteralInteger(1)),
            new Dictionary<string, IParameter> { ["p"] = new TextParameter("string", "1") }
        );
    }

    [Fact]
    public void ANullableDateParameterCellAndADatetimeCellDoNotMeet()
    {
        AssertRejected(
            Values(
                new ParamDateNullable("p"),
                new LiteralDatetime(new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero))
            ),
            new Dictionary<string, IParameter>
            {
                ["p"] = new TextParameter("date", "2024-01-01"),
            }
        );
    }

    [Fact]
    public void ADecimalCellAndANullBooleanCellDoNotMeet()
    {
        AssertRejected(Values(new LiteralDecimal(0.5m), new LiteralBooleanNullable()), null);
    }

    [Fact]
    public void ATimeCellAndAStringCellDoNotMeet()
    {
        AssertRejected(
            Values(new LiteralTime(new TimeOnly(1, 0)), new LiteralString("01:00:00")),
            null
        );
    }

    [Fact]
    public void AnIntegerParameterCellOutOfRangeIsRejected()
    {
        // Validation: a parameter value is written like a literal of its
        // type.
        _ = Assert.Throws<ArgumentException>(() =>
            SelectingParameter("integer", "9223372036854775808")
        );
    }

    [Theory(
        Skip = "Bug: spec (Parameters, Validation) requires a parameter value in"
            + " the literal's one spelling; 1.50, 01.5, +01:00 and upper-case uuids"
            + " are accepted"
    )]
    [InlineData("decimal", "1.50")]
    [InlineData("decimal", "01.5")]
    [InlineData("datetime", "2024-01-01T00:00:00+01:00")]
    [InlineData("uuid", "0000000A-0000-0000-0000-00000000000B")]
    public void AParameterCellBoundToTextNotWrittenAsALiteralIsRejected(
        string type,
        string value
    )
    {
        // Validation: a parameter value is written like a literal of its
        // type: one spelling, a datetime in UTC; Parameters: a uuid in lower
        // case.
        _ = Assert.Throws<ArgumentException>(() => SelectingParameter(type, value));
    }

    // The column v of one inline row whose cell is the parameter p of a
    // type, bound to a text.
    private static PureQLProjection SelectingParameter(string type, string value)
    {
        (InlineCell cell, ExprProjection field) = type switch
        {
            "decimal" => ((InlineCell)new ParamDecimal("p"), (ExprProjection)new FieldDecimal("list", "v")),
            "integer" => (new ParamInteger("p"), new FieldInteger("list", "v")),
            "datetime" => (new ParamDatetime("p"), new FieldDatetime("list", "v")),
            _ => (new ParamUuid("p"), new FieldUuid("list", "v")),
        };

        return new PureQLProjection(
            [Stock],
            new MainPlainQueryWithoutPagination(
                subqueries: [Values(cell)],
                from: new FromSubquery("list"),
                select: [new SelectItemProjection("v", field)]
            ),
            new Dictionary<string, IParameter> { ["p"] = new TextParameter(type, value) }
        );
    }

    // The inline rows list: one row for each cell, in the column v.
    private static Subquery Values(params InlineCell[] cells)
    {
        return new SubqueryRows(
            "list",
            cells.Select(cell => (IEnumerable<RowItem>)[new RowItem("v", cell)])
        );
    }

    // The id of t.stock and the column v of the inline rows list joined to
    // it, sorted by id.
    private static PureQLProjection Joined(
        Subquery rows,
        JoinKind kind,
        BooleanRow on,
        ExprProjection v
    )
    {
        return new PureQLProjection(
            [Stock],
            new MainPlainQueryWithoutPagination(
                subqueries: [rows],
                from: new FromEntity("t.stock"),
                joins: [new JoinSubquery(kind, "list", on)],
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("t.stock", "id")),
                    new SelectItemProjection("v", v),
                ],
                orderBy: [new OrderItemProjectionColumn("id")]
            )
        );
    }

    private static PureQLProjection ReadingPairs(
        Subquery rows,
        ExprProjection a,
        ExprProjection b
    )
    {
        return new PureQLProjection(
            [Stock],
            new MainPlainQueryWithoutPagination(
                subqueries: [rows],
                from: new FromSubquery("pairs"),
                select: [new SelectItemProjection("a", a), new SelectItemProjection("b", b)],
                orderBy: [new OrderItemProjectionColumn("a")]
            )
        );
    }

    // Inline rows that are rejected when the projection is built, read by
    // a query that selects nothing of them.
    private static void AssertRejected(
        Subquery rows,
        Dictionary<string, IParameter>? parameters
    )
    {
        MainTotalQuery query = new(
            subqueries: [rows],
            from: new FromSubquery("list"),
            aggregate: [new SelectItemTotal("rows", new CountTotal())]
        );

        _ = Assert.Throws<ArgumentException>(() =>
            parameters is null
                ? new PureQLProjection([Stock], query)
                : new PureQLProjection([Stock], query, parameters)
        );
    }

    private static void AssertRows(PureQLProjection projection, params string[] expected)
    {
        Assert.Equal(expected.AsEnumerable(), Texts(projection));
    }

    // The cell texts of every row, in the order of the result's columns.
    private static IEnumerable<string> Texts(PureQLProjection projection)
    {
        IReadOnlyList<string> columns =
        [
            .. projection.TableSchema.Columns.Select(column => column.Name.TextValue),
        ];

        return
        [
            .. projection
                .AsEnumerable()
                .Select(row =>
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
