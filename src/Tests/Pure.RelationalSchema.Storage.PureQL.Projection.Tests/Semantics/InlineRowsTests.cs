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

// Inline rows: a subquery whose rows are written in the document, each a
// list of { alias, expression } cells that are literals or parameters. Every
// row has the same aliases, in any order; a column takes the least type that
// accepts its cell in every row, and a null cell or a nullable parameter
// makes it nullable. The rows are a subquery like any other: one row for
// each row written, duplicates included, read with from, join or in and by
// the subqueries declared after them.
public sealed class InlineRowsTests
{
    private static readonly IStoredSchemaDataSet Orders = new InlineTable(
        "t",
        "orders",
        ("id", new LongColumnType()),
        ("status", new StringColumnType()),
        ("total", new DoubleColumnType()),
        ("note", new StringColumnType())
    ).With(
        ["1", "new", "10", "a"],
        ["2", "paid", "20", ""],
        ["3", "paid", "30.5", "b"],
        ["4", "cancelled", "5", ""],
        ["5", "lost", "1", "c"]
    );

    [Fact]
    public void RowsAreReadWithFrom()
    {
        // The example of the specification.
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRows(
                        "statuses",
                        [
                            [
                                new RowItem("status", new LiteralString("new")),
                                new RowItem("rank", new LiteralInteger(1)),
                            ],
                            [
                                new RowItem("status", new LiteralString("paid")),
                                new RowItem("rank", new LiteralInteger(2)),
                            ],
                        ]
                    ),
                ],
                from: new FromSubquery("statuses"),
                select:
                [
                    new SelectItemProjection(
                        "status",
                        new FieldString("statuses", "status")
                    ),
                    new SelectItemProjection("rank", new FieldInteger("statuses", "rank")),
                ],
                orderBy: [new OrderItemProjectionColumn("rank")]
            ),
            "new|1",
            "paid|2"
        );
    }

    [Fact]
    public void RowsAreJoinedAsAReferenceList()
    {
        // Sample 110: the label and the rank of each order's status; an
        // order whose status is not listed has neither.
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries: [Statuses()],
                from: new FromEntity("t.orders"),
                joins:
                [
                    new JoinSubquery(
                        JoinKind.Left,
                        "statuses",
                        new EqualStringRow(
                            new FieldString("t.orders", "status"),
                            new FieldString("statuses", "status")
                        )
                    ),
                ],
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("t.orders", "id")),
                    new SelectItemProjection(
                        "status",
                        new CoalesceStringProjection([
                            new FieldStringNullable("statuses", "label"),
                            new FieldString("t.orders", "status"),
                        ])
                    ),
                    new SelectItemProjection(
                        "rank",
                        new FieldIntegerNullable("statuses", "rank")
                    ),
                ],
                orderBy:
                [
                    new OrderItemProjectionExpression(
                        new FieldIntegerNullable("statuses", "rank")
                    ),
                    new OrderItemProjectionExpression(new FieldInteger("t.orders", "id")),
                ]
            ),
            "5|lost|",
            "1|New|1",
            "2|Paid|2",
            "3|Paid|2",
            "4|cancelled|4"
        );
    }

    [Fact]
    public void CellsAreMatchedByAliasWhateverTheirOrderInARow()
    {
        // By position the second row would be (s, r).
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRows(
                        "pairs",
                        [
                            [
                                new RowItem("a", new LiteralString("p")),
                                new RowItem("b", new LiteralString("q")),
                            ],
                            [
                                new RowItem("b", new LiteralString("s")),
                                new RowItem("a", new LiteralString("r")),
                            ],
                        ]
                    ),
                ],
                from: new FromSubquery("pairs"),
                select:
                [
                    new SelectItemProjection("a", new FieldString("pairs", "a")),
                    new SelectItemProjection("b", new FieldString("pairs", "b")),
                ],
                orderBy: [new OrderItemProjectionColumn("a")]
            ),
            "p|q",
            "r|s"
        );
    }

    [Fact]
    public void CellsOfDifferentTypesAreMatchedByAliasInEveryRow()
    {
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRows(
                        "pairs",
                        [
                            [
                                new RowItem("k", new LiteralString("a")),
                                new RowItem("v", new LiteralInteger(1)),
                                new RowItem("on", new LiteralBoolean(true)),
                            ],
                            [
                                new RowItem("on", new LiteralBoolean(false)),
                                new RowItem("k", new LiteralString("b")),
                                new RowItem("v", new LiteralInteger(2)),
                            ],
                            [
                                new RowItem("v", new LiteralInteger(3)),
                                new RowItem("on", new LiteralBoolean(true)),
                                new RowItem("k", new LiteralString("c")),
                            ],
                        ]
                    ),
                ],
                from: new FromSubquery("pairs"),
                select:
                [
                    new SelectItemProjection("k", new FieldString("pairs", "k")),
                    new SelectItemProjection("v", new FieldInteger("pairs", "v")),
                    new SelectItemProjection("on", new FieldBoolean("pairs", "on")),
                ],
                orderBy: [new OrderItemProjectionColumn("v")]
            ),
            "a|1|True",
            "b|2|False",
            "c|3|True"
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AnIntegerAndADecimalCellGiveADecimalColumn(bool integerFirst)
    {
        InlineCell[] cells = integerFirst
            ? [new LiteralInteger(1), new LiteralDecimal(2.5m)]
            : [new LiteralDecimal(2.5m), new LiteralInteger(1)];

        AssertRows(Reading(Values(cells), new FieldDecimal("list", "v")), "1", "2.5");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void AColumnOfAnIntegerAndADecimalCellIsNotAnInteger(bool integerFirst)
    {
        InlineCell[] cells = integerFirst
            ? [new LiteralInteger(1), new LiteralDecimal(2.5m)]
            : [new LiteralDecimal(2.5m), new LiteralInteger(1)];

        _ = Assert.Throws<ArgumentException>(() =>
            Reading(Values(cells), new FieldInteger("list", "v"))
        );
    }

    [Fact]
    public void IntegerCellsGiveAnIntegerColumn()
    {
        Subquery rows = Values(new LiteralInteger(2), new LiteralInteger(1));

        AssertRows(Reading(rows, new FieldInteger("list", "v")), "1", "2");
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(rows, new FieldDecimal("list", "v"))
        );
    }

    [Fact]
    public void ADecimalColumnOfIntegerCellsIsComputedWithAsADecimal()
    {
        // 1 / 2 + 2.5 / 2 over the rows 1 and 2.5.
        AssertRows(
            new MainTotalQuery(
                subqueries: [Values(new LiteralInteger(1), new LiteralDecimal(2.5m))],
                from: new FromSubquery("list"),
                aggregate:
                [
                    new SelectItemTotal("rows", new CountTotal()),
                    new SelectItemTotal(
                        "sum",
                        new SumDecimalTotal(new FieldDecimal("list", "v"))
                    ),
                    new SelectItemTotal(
                        "halves",
                        new SumDecimalTotal(
                            new DivideDecimalRow([
                                new FieldDecimal("list", "v"),
                                new LiteralInteger(2),
                            ])
                        )
                    ),
                ]
            ),
            "2|3.5|1.75"
        );
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void ANullCellMakesTheColumnNullable(bool nullFirst)
    {
        InlineCell[] cells = nullFirst
            ? [new LiteralStringNullable(), new LiteralString("a")]
            : [new LiteralString("a"), new LiteralStringNullable()];

        AssertRows(Reading(Values(cells), new FieldStringNullable("list", "v")), "", "a");
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(Values(cells), new FieldString("list", "v"))
        );
    }

    [Fact]
    public void ANullCellAndADecimalCellMakeAnIntegerColumnANullableDecimal()
    {
        Subquery rows = Values(
            new LiteralInteger(1),
            new LiteralIntegerNullable(),
            new LiteralDecimal(0.5m)
        );

        AssertRows(
            Reading(rows, new FieldDecimalNullable("list", "v")),
            "",
            "0.5",
            "1"
        );
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(rows, new FieldDecimal("list", "v"))
        );
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(rows, new FieldIntegerNullable("list", "v"))
        );
    }

    [Fact]
    public void AColumnWithoutANullCellIsNotNullable()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(
                Values(new LiteralString("a"), new LiteralString("b")),
                new FieldStringNullable("list", "v")
            )
        );
    }

    [Fact]
    public void ANullableParameterMakesTheColumnNullableWhateverItIsBoundTo()
    {
        Subquery rows = Values(new LiteralInteger(1), new ParamIntegerNullable("p"));
        Dictionary<string, IParameter> bound = new()
        {
            ["p"] = new TextParameter("integer", "7"),
        };

        AssertRows(
            Reading(rows, new FieldIntegerNullable("list", "v"), bound),
            "1",
            "7"
        );
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(rows, new FieldInteger("list", "v"), bound)
        );
    }

    [Fact]
    public void ANullableParameterBoundToNullIsANullCell()
    {
        AssertRows(
            Reading(
                Values(new LiteralInteger(1), new ParamIntegerNullable("p")),
                new FieldIntegerNullable("list", "v"),
                new Dictionary<string, IParameter>
                {
                    ["p"] = new TextParameter(new TextParameterType("integer", true), ""),
                }
            ),
            "",
            "1"
        );
    }

    [Fact]
    public void AnIntegerParameterAndADecimalCellGiveADecimalColumn()
    {
        AssertRows(
            Reading(
                Values(new ParamInteger("p"), new LiteralDecimal(0.25m)),
                new FieldDecimal("list", "v"),
                new Dictionary<string, IParameter>
                {
                    ["p"] = new TextParameter("integer", "3"),
                }
            ),
            "0.25",
            "3"
        );
    }

    [Fact]
    public void RowsAreReturnedOnceForEachRowWrittenDuplicatesIncluded()
    {
        AssertRows(
            Reading(
                Values(
                    new LiteralString("a"),
                    new LiteralString("b"),
                    new LiteralString("a"),
                    new LiteralString("a")
                ),
                new FieldString("list", "v")
            ),
            "a",
            "a",
            "a",
            "b"
        );
    }

    [Fact]
    public void DuplicateRowsAreCounted()
    {
        AssertRows(
            new MainGroupedQueryWithoutPagination(
                subqueries:
                [
                    Values(
                        new LiteralString("a"),
                        new LiteralString("b"),
                        new LiteralString("a"),
                        new LiteralString("a")
                    ),
                ],
                from: new FromSubquery("list"),
                groupBy: [new GroupKey("v", new FieldString("list", "v"))],
                select:
                [
                    new SelectItemGroup("v", new KeyString("v")),
                    new SelectItemGroup("rows", new CountGroup()),
                ],
                orderBy: [new OrderItemGroupColumn("v")]
            ),
            "a|3",
            "b|1"
        );
    }

    [Fact]
    public void OneRowOfOneColumnIsASubquery()
    {
        AssertRows(
            Reading(Values(new LiteralInteger(42)), new FieldInteger("list", "v")),
            "42"
        );
    }

    [Fact]
    public void ACellOfEveryTypeCarriesItsType()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRows(
                        "one",
                        [
                            [
                                new RowItem("integer", new LiteralInteger(-7)),
                                new RowItem("decimal", new LiteralDecimal(1.50m)),
                                new RowItem("string", new LiteralString("a b")),
                                new RowItem("boolean", new LiteralBoolean(true)),
                                new RowItem(
                                    "date",
                                    new LiteralDate(new DateOnly(2024, 2, 29))
                                ),
                                new RowItem(
                                    "time",
                                    new LiteralTime(new TimeOnly(13, 5, 9))
                                ),
                                new RowItem(
                                    "datetime",
                                    new LiteralDatetime(
                                        new DateTimeOffset(
                                            2024,
                                            2,
                                            29,
                                            23,
                                            59,
                                            58,
                                            TimeSpan.Zero
                                        )
                                    )
                                ),
                                new RowItem(
                                    "uuid",
                                    new LiteralUuid(
                                        new Guid("0000000a-0000-0000-0000-00000000000b")
                                    )
                                ),
                            ],
                        ]
                    ),
                ],
                from: new FromSubquery("one"),
                select:
                [
                    new SelectItemProjection("integer", new FieldInteger("one", "integer")),
                    new SelectItemProjection("decimal", new FieldDecimal("one", "decimal")),
                    new SelectItemProjection("string", new FieldString("one", "string")),
                    new SelectItemProjection("boolean", new FieldBoolean("one", "boolean")),
                    new SelectItemProjection("date", new FieldDate("one", "date")),
                    new SelectItemProjection("time", new FieldTime("one", "time")),
                    new SelectItemProjection(
                        "datetime",
                        new FieldDatetime("one", "datetime")
                    ),
                    new SelectItemProjection("uuid", new FieldUuid("one", "uuid")),
                ]
            )
        );

        AssertRows(
            projection,
            "-7|1.5|a b|True|2024-02-29|13:05:09|2024-02-29T23:59:58Z"
                + "|0000000a-0000-0000-0000-00000000000b"
        );
    }

    [Fact]
    public void ANullCellOfEveryTypeIsNull()
    {
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRows(
                        "one",
                        [
                            [
                                new RowItem("integer", new LiteralIntegerNullable()),
                                new RowItem("decimal", new LiteralDecimalNullable()),
                                new RowItem("string", new LiteralStringNullable()),
                                new RowItem("boolean", new LiteralBooleanNullable()),
                                new RowItem("date", new LiteralDateNullable()),
                                new RowItem("time", new LiteralTimeNullable()),
                                new RowItem("datetime", new LiteralDatetimeNullable()),
                                new RowItem("uuid", new LiteralUuidNullable()),
                                new RowItem("mark", new LiteralString("end")),
                            ],
                        ]
                    ),
                ],
                from: new FromSubquery("one"),
                select:
                [
                    new SelectItemProjection(
                        "integer",
                        new FieldIntegerNullable("one", "integer")
                    ),
                    new SelectItemProjection(
                        "decimal",
                        new FieldDecimalNullable("one", "decimal")
                    ),
                    new SelectItemProjection(
                        "string",
                        new FieldStringNullable("one", "string")
                    ),
                    new SelectItemProjection(
                        "boolean",
                        new FieldBooleanNullable("one", "boolean")
                    ),
                    new SelectItemProjection("date", new FieldDateNullable("one", "date")),
                    new SelectItemProjection("time", new FieldTimeNullable("one", "time")),
                    new SelectItemProjection(
                        "datetime",
                        new FieldDatetimeNullable("one", "datetime")
                    ),
                    new SelectItemProjection("uuid", new FieldUuidNullable("one", "uuid")),
                    new SelectItemProjection("mark", new FieldString("one", "mark")),
                ]
            ),
            "||||||||end"
        );
    }

    [Fact]
    public void ParameterCellsHoldTheBoundValues()
    {
        AssertRows(
            new PureQLProjection(
                [Orders],
                new MainPlainQueryWithoutPagination(
                    subqueries:
                    [
                        new SubqueryRows(
                            "one",
                            [
                                [
                                    new RowItem("integer", new ParamInteger("integer")),
                                    new RowItem("decimal", new ParamDecimal("decimal")),
                                    new RowItem("string", new ParamString("string")),
                                    new RowItem("boolean", new ParamBoolean("boolean")),
                                    new RowItem("date", new ParamDate("date")),
                                    new RowItem("none", new ParamDateNullable("none")),
                                ],
                            ]
                        ),
                    ],
                    from: new FromSubquery("one"),
                    select:
                    [
                        new SelectItemProjection(
                            "integer",
                            new FieldInteger("one", "integer")
                        ),
                        new SelectItemProjection(
                            "decimal",
                            new FieldDecimal("one", "decimal")
                        ),
                        new SelectItemProjection(
                            "string",
                            new FieldString("one", "string")
                        ),
                        new SelectItemProjection(
                            "boolean",
                            new FieldBoolean("one", "boolean")
                        ),
                        new SelectItemProjection("date", new FieldDate("one", "date")),
                        new SelectItemProjection(
                            "none",
                            new FieldDateNullable("one", "none")
                        ),
                    ]
                ),
                new Dictionary<string, IParameter>
                {
                    ["integer"] = new TextParameter("integer", "-7"),
                    ["decimal"] = new TextParameter("decimal", "2.5"),
                    ["string"] = new TextParameter("string", "x"),
                    ["boolean"] = new TextParameter("boolean", "False"),
                    ["date"] = new TextParameter("date", "2024-02-29"),
                    ["none"] = new TextParameter(new TextParameterType("date", true), ""),
                }
            ),
            "-7|2.5|x|False|2024-02-29|"
        );
    }

    [Fact]
    public void OneRowOfParametersJoinedOnTruePutsThemNextToEveryRow()
    {
        // The orders of at least 20, each with its total times 1.5.
        AssertRows(
            new PureQLProjection(
                [Orders],
                new MainPlainQueryWithoutPagination(
                    subqueries:
                    [
                        new SubqueryRows(
                            "settings",
                            [
                                [
                                    new RowItem("factor", new ParamDecimal("factor")),
                                    new RowItem("least", new ParamInteger("least")),
                                ],
                            ]
                        ),
                    ],
                    from: new FromEntity("t.orders"),
                    joins:
                    [
                        new JoinSubquery(
                            JoinKind.Inner,
                            "settings",
                            new LiteralBoolean(true)
                        ),
                    ],
                    where: new GreaterThanOrEqualDecimalRow(
                        new FieldDecimal("t.orders", "total"),
                        new FieldInteger("settings", "least")
                    ),
                    select:
                    [
                        new SelectItemProjection(
                            "id",
                            new FieldInteger("t.orders", "id")
                        ),
                        new SelectItemProjection(
                            "scaled",
                            new MultiplyDecimalProjection([
                                new FieldDecimal("t.orders", "total"),
                                new FieldDecimal("settings", "factor"),
                            ])
                        ),
                    ],
                    orderBy: [new OrderItemProjectionColumn("id")]
                ),
                new Dictionary<string, IParameter>
                {
                    ["factor"] = new TextParameter("decimal", "1.5"),
                    ["least"] = new TextParameter("integer", "20"),
                }
            ),
            "2|30",
            "3|45.75"
        );
    }

    [Fact]
    public void RowsJoinedOnTrueAreNextToEveryRow()
    {
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries: [Values(new LiteralString("a"), new LiteralString("b"))],
                from: new FromEntity("t.orders"),
                joins:
                [
                    new JoinSubquery(JoinKind.Inner, "list", new LiteralBoolean(true)),
                ],
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("t.orders", "id")),
                    new SelectItemProjection("v", new FieldString("list", "v")),
                ],
                orderBy:
                [
                    new OrderItemProjectionColumn("id"),
                    new OrderItemProjectionColumn("v"),
                ]
            ),
            "1|a",
            "1|b",
            "2|a",
            "2|b",
            "3|a",
            "3|b",
            "4|a",
            "4|b",
            "5|a",
            "5|b"
        );
    }

    [Fact]
    public void RowsAreReadTwiceUnderTwoAliases()
    {
        // Each status with the one of the next rank.
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries: [Statuses()],
                from: new FromSubquery("statuses", "a"),
                joins:
                [
                    new JoinSubquery(
                        JoinKind.Inner,
                        "statuses",
                        new EqualDecimalRow(
                            new FieldInteger("b", "rank"),
                            new AddIntegerRow([
                                new FieldInteger("a", "rank"),
                                new LiteralInteger(1),
                            ])
                        ),
                        "b"
                    ),
                ],
                select:
                [
                    new SelectItemProjection("status", new FieldString("a", "status")),
                    new SelectItemProjection("next", new FieldString("b", "status")),
                ],
                orderBy:
                [
                    new OrderItemProjectionExpression(new FieldInteger("a", "rank")),
                ]
            ),
            "new|paid",
            "paid|shipped",
            "shipped|cancelled"
        );
    }

    [Fact]
    public void AColumnIsTheListOfIn()
    {
        AssertRows(OrdersWhere(StatusIsListed()), "1", "2", "3", "4");
    }

    [Fact]
    public void AColumnIsTheListOfNotIn()
    {
        AssertRows(OrdersWhere(new NotRow(StatusIsListed())), "5");
    }

    [Fact]
    public void AnIntegerColumnIsTheListOfADecimalOperand()
    {
        AssertRows(
            OrdersWhere(
                new InDecimalRow(
                    new FieldDecimal("t.orders", "total"),
                    new ListSubqueryColumnInteger("list", "v")
                ),
                Values(new LiteralInteger(10), new LiteralInteger(20), new LiteralInteger(7))
            ),
            "1",
            "2"
        );
    }

    [Fact]
    public void ANullOperandIsInAColumnWithANullCell()
    {
        // The notes of the orders 2 and 4 are null.
        AssertRows(
            OrdersWhere(
                new InStringRow(
                    new FieldStringNullable("t.orders", "note"),
                    new ListSubqueryColumnStringNullable("list", "v")
                ),
                Values(new LiteralString("a"), new LiteralStringNullable())
            ),
            "1",
            "2",
            "4"
        );
    }

    [Fact]
    public void ANullOperandIsNotInAColumnWithoutANullCell()
    {
        AssertRows(
            OrdersWhere(
                new InStringRow(
                    new FieldStringNullable("t.orders", "note"),
                    new ListSubqueryColumnString("list", "v")
                ),
                Values(new LiteralString("a"), new LiteralString("c"))
            ),
            "1",
            "5"
        );
    }

    [Fact]
    public void RowsAreReadByALaterSubquery()
    {
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries: [Statuses(), Ranked()],
                from: new FromSubquery("ranked"),
                where: new GreaterThanOrEqualDecimalRow(
                    new FieldInteger("ranked", "rank"),
                    new LiteralInteger(2)
                ),
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("ranked", "id")),
                    new SelectItemProjection("rank", new FieldInteger("ranked", "rank")),
                ],
                orderBy: [new OrderItemProjectionColumn("id")]
            ),
            "2|2",
            "3|2",
            "4|4"
        );
    }

    [Fact]
    public void RowsDeclaredAfterTheSubqueryThatReadsThemAreRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Orders],
                new MainPlainQueryWithoutPagination(
                    subqueries: [Ranked(), Statuses()],
                    from: new FromSubquery("ranked"),
                    select:
                    [
                        new SelectItemProjection("id", new FieldInteger("ranked", "id")),
                    ]
                )
            )
        );
    }

    [Fact]
    public void RowsAreGroupedByTheQueryThatReadsThem()
    {
        AssertRows(
            new MainGroupedQueryWithoutPagination(
                subqueries: [Statuses()],
                from: new FromEntity("t.orders"),
                joins:
                [
                    new JoinSubquery(
                        JoinKind.Inner,
                        "statuses",
                        new EqualStringRow(
                            new FieldString("t.orders", "status"),
                            new FieldString("statuses", "status")
                        )
                    ),
                ],
                groupBy: [new GroupKey("rank", new FieldInteger("statuses", "rank"))],
                select:
                [
                    new SelectItemGroup("rank", new KeyInteger("rank")),
                    new SelectItemGroup("orders", new CountGroup()),
                ],
                orderBy: [new OrderItemGroupColumn("rank", SortDirection.Desc)]
            ),
            "4|1",
            "2|2",
            "1|1"
        );
    }

    [Fact]
    public void RowsAreSortedAndPagedByTheQueryThatReadsThem()
    {
        AssertRows(
            new MainPlainQueryWithPagination(
                subqueries: [Statuses()],
                from: new FromSubquery("statuses"),
                select:
                [
                    new SelectItemProjection(
                        "status",
                        new FieldString("statuses", "status")
                    ),
                ],
                orderBy:
                [
                    new OrderItemProjectionExpression(
                        new FieldInteger("statuses", "rank"),
                        SortDirection.Desc
                    ),
                ],
                pagination: new Pagination(skip: 1L, take: 2L)
            ),
            "shipped",
            "paid"
        );
    }

    [Fact]
    public void ARowWithAnotherAliasIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(
                new SubqueryRows(
                    "list",
                    [
                        [new RowItem("v", new LiteralInteger(1))],
                        [new RowItem("w", new LiteralInteger(2))],
                    ]
                ),
                new FieldInteger("list", "v")
            )
        );
    }

    [Fact]
    public void ARowWithACellMoreIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(
                new SubqueryRows(
                    "list",
                    [
                        [new RowItem("v", new LiteralInteger(1))],
                        [
                            new RowItem("v", new LiteralInteger(2)),
                            new RowItem("w", new LiteralInteger(2)),
                        ],
                    ]
                ),
                new FieldInteger("list", "v")
            )
        );
    }

    [Fact]
    public void ARowWithACellLessIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(
                new SubqueryRows(
                    "list",
                    [
                        [
                            new RowItem("v", new LiteralInteger(1)),
                            new RowItem("w", new LiteralInteger(1)),
                        ],
                        [
                            new RowItem("v", new LiteralInteger(2)),
                            new RowItem("w", new LiteralInteger(2)),
                        ],
                        [new RowItem("v", new LiteralInteger(3))],
                    ]
                ),
                new FieldInteger("list", "v")
            )
        );
    }

    [Fact]
    public void AnAliasTwiceInEveryRowIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(
                new SubqueryRows(
                    "list",
                    [
                        [
                            new RowItem("v", new LiteralInteger(1)),
                            new RowItem("v", new LiteralInteger(1)),
                        ],
                        [
                            new RowItem("v", new LiteralInteger(2)),
                            new RowItem("v", new LiteralInteger(2)),
                        ],
                    ]
                ),
                new FieldInteger("list", "v")
            )
        );
    }

    [Fact]
    public void AnAliasTwiceInTheOnlyRowIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(
                new SubqueryRows(
                    "list",
                    [
                        [
                            new RowItem("v", new LiteralInteger(1)),
                            new RowItem("v", new LiteralInteger(1)),
                        ],
                    ]
                ),
                new FieldInteger("list", "v")
            )
        );
    }

    [Fact]
    public void AnIntegerAndAStringCellDoNotMeet()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(
                Values(new LiteralInteger(1), new LiteralString("2")),
                new FieldInteger("list", "v")
            )
        );
    }

    [Fact]
    public void ADateAndADatetimeCellDoNotMeet()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(
                Values(
                    new LiteralDate(new DateOnly(2024, 1, 1)),
                    new LiteralDatetime(
                        new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
                    )
                ),
                new FieldDate("list", "v")
            )
        );
    }

    [Fact]
    public void ANullCellOfAnotherTypeDoesNotMeet()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(
                Values(new LiteralInteger(1), new LiteralStringNullable()),
                new FieldIntegerNullable("list", "v")
            )
        );
    }

    [Fact]
    public void ACellInTheThirdRowThatDoesNotMeetIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(
                Values(
                    new LiteralInteger(1),
                    new LiteralDecimal(1.5m),
                    new LiteralBoolean(true)
                ),
                new FieldDecimal("list", "v")
            )
        );
    }

    [Fact]
    public void AParameterCellWithoutAValueIsRejected()
    {
        Subquery rows = Values(new LiteralInteger(1), new ParamInteger("p"));

        _ = Assert.Throws<ArgumentException>(() =>
            Reading(rows, new FieldInteger("list", "v"))
        );
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(
                rows,
                new FieldInteger("list", "v"),
                new Dictionary<string, IParameter>
                {
                    ["q"] = new TextParameter("integer", "2"),
                }
            )
        );
    }

    [Fact]
    public void AParameterCellBoundToAValueOfAnotherTypeIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(
                Values(new LiteralInteger(1), new ParamInteger("p")),
                new FieldInteger("list", "v"),
                new Dictionary<string, IParameter>
                {
                    ["p"] = new TextParameter("string", "2"),
                }
            )
        );
    }

    [Fact]
    public void ANonNullParameterCellBoundToNullIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(
                Values(new LiteralInteger(1), new ParamInteger("p")),
                new FieldInteger("list", "v"),
                new Dictionary<string, IParameter>
                {
                    ["p"] = new TextParameter(new TextParameterType("integer", true), ""),
                }
            )
        );
    }

    [Fact]
    public void AParameterOfACellHasOneTypeInTheWholeDocument()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Orders],
                new MainPlainQueryWithoutPagination(
                    subqueries: [Values(new ParamInteger("p"))],
                    from: new FromSubquery("list"),
                    select:
                    [
                        new SelectItemProjection("v", new FieldInteger("list", "v")),
                        new SelectItemProjection("p", new ParamString("p")),
                    ]
                ),
                new Dictionary<string, IParameter>
                {
                    ["p"] = new TextParameter("integer", "2"),
                }
            )
        );
    }

    [Fact]
    public void TwoSubqueriesOfOneNameAreRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Orders],
                new MainPlainQueryWithoutPagination(
                    subqueries:
                    [
                        Values(new LiteralInteger(1)),
                        Values(new LiteralInteger(2)),
                    ],
                    from: new FromSubquery("list"),
                    select:
                    [
                        new SelectItemProjection("v", new FieldInteger("list", "v")),
                    ]
                )
            )
        );
    }

    [Fact]
    public void AColumnTheRowsDoNotHaveIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(Values(new LiteralInteger(1)), new FieldInteger("list", "w"))
        );
    }

    // The reference list of sample 110: a status, its rank and its label,
    // which the last status does not have.
    private static Subquery Statuses()
    {
        return new SubqueryRows(
            "statuses",
            [
                Status("new", 1, new LiteralString("New")),
                Status("paid", 2, new LiteralString("Paid")),
                Status("shipped", 3, new LiteralString("Shipped")),
                Status("cancelled", 4, new LiteralStringNullable()),
            ]
        );
    }

    private static RowItem[] Status(string status, long rank, InlineCell label)
    {
        return
        [
            new RowItem("status", new LiteralString(status)),
            new RowItem("rank", new LiteralInteger(rank)),
            new RowItem("label", label),
        ];
    }

    // The id of every order with a listed status, and the rank of the status.
    private static Subquery Ranked()
    {
        return new SubqueryQuery(
            "ranked",
            new PlainQueryWithoutPaginationWithoutOrderBy(
                from: new FromEntity("t.orders"),
                joins:
                [
                    new JoinSubquery(
                        JoinKind.Inner,
                        "statuses",
                        new EqualStringRow(
                            new FieldString("t.orders", "status"),
                            new FieldString("statuses", "status")
                        )
                    ),
                ],
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("t.orders", "id")),
                    new SelectItemProjection("rank", new FieldInteger("statuses", "rank")),
                ]
            )
        );
    }

    private static BooleanRow StatusIsListed()
    {
        return new InStringRow(
            new FieldString("t.orders", "status"),
            new ListSubqueryColumnString("statuses", "status")
        );
    }

    // The ids of the orders a condition keeps, in order; the condition reads
    // the given inline rows, or the statuses.
    private static PureQLProjection OrdersWhere(
        BooleanRow condition,
        Subquery? rows = null
    )
    {
        return new PureQLProjection(
            [Orders],
            new MainPlainQueryWithoutPagination(
                subqueries: [rows ?? Statuses()],
                from: new FromEntity("t.orders"),
                where: condition,
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("t.orders", "id")),
                ],
                orderBy: [new OrderItemProjectionColumn("id")]
            )
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

    // The column v of inline rows, read through the given field and sorted.
    private static PureQLProjection Reading(
        Subquery rows,
        ExprProjection field,
        Dictionary<string, IParameter>? parameters = null
    )
    {
        MainPlainQueryWithoutPagination query = new(
            subqueries: [rows],
            from: new FromSubquery("list"),
            select: [new SelectItemProjection("v", field)],
            orderBy: [new OrderItemProjectionColumn("v")]
        );

        return parameters is null
            ? new PureQLProjection([Orders], query)
            : new PureQLProjection([Orders], query, parameters);
    }

    private static void AssertRows(PureQLQuery query, params string[] expected)
    {
        AssertRows(new PureQLProjection([Orders], query), expected);
    }

    private static void AssertRows(PureQLProjection projection, params string[] expected)
    {
        IReadOnlyList<string> columns =
        [
            .. projection.TableSchema.Columns.Select(column => column.Name.TextValue),
        ];

        Assert.Equal(
            expected.AsEnumerable(),
            projection
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
                )
        );
    }
}
