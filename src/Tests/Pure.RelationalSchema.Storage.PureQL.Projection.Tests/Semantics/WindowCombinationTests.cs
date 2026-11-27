using Pure.Primitives.Number;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Parameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.TotalExpressions;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Window functions combined with the other clauses, every value derived
// from Evaluation and Window evaluation: they are computed with the select
// of a plain query over the rows left by from, joins and where (outer joins
// included, whose missing fields are null and so form one null partition),
// before orderBy, distinctBy and pagination; a subquery behaves as if it ran
// once, so windows in a subquery, in an input of a set operation, in the
// seed of a recursion or over inline rows, a total query's row, a recursion
// or a filtered table run over exactly those rows.
public sealed class WindowCombinationTests
{
    // user a: 1 (30), 2 (10), 4 (40); b: 3 (20), 5 (50); c: 6 (60);
    // d: 7 (15).
    private static readonly IStoredSchemaDataSet Orders = new InlineTable(
        "t",
        "orders",
        ("id", new LongColumnType()),
        ("user", new StringColumnType()),
        ("total", new LongColumnType())
    ).With(
        ["1", "a", "30"],
        ["2", "a", "10"],
        ["3", "b", "20"],
        ["4", "a", "40"],
        ["5", "b", "50"],
        ["6", "c", "60"],
        ["7", "d", "15"]
    );

    // c has no region, d no user row, e no order.
    private static readonly IStoredSchemaDataSet Users = new InlineTable(
        "t",
        "users",
        ("name", new StringColumnType()),
        ("region", new StringColumnType())
    ).With(["a", "north"], ["b", "south"], ["c", ""], ["e", "west"]);

    private static readonly IStoredSchemaDataSet Items = new InlineTable(
        "t",
        "items",
        ("id", new LongColumnType())
    ).With(["0"], ["1"], ["2"]);

    // ref 0 and 1 point at items; a null ref points nowhere.
    private static readonly IStoredSchemaDataSet Links = new InlineTable(
        "t",
        "links",
        ("id", new LongColumnType()),
        ("ref", new LongColumnType())
    ).With(["1", "0"], ["2", ""], ["3", "1"], ["4", "1"]);

    private static readonly FieldInteger Id = new("t.orders", "id");

    private static readonly FieldString User = new("t.orders", "user");

    private static readonly FieldInteger Total = new("t.orders", "total");

    private static readonly OrderedWindow ById = new([new OrderItemRow(Id)]);

    private static readonly OrderedWindow PerUserById = new(
        [new OrderItemRow(Id)],
        [User]
    );

    [Fact]
    public void WindowsOverALeftJoinPutTheMissingAndTheNullRegionInOnePartition()
    {
        FieldStringNullable region = new("t.users", "region");
        PureQLProjection projection = new(
            [Orders, Users],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                joins: [UsersOf(JoinKind.Left)],
                select:
                [
                    new SelectItemProjection(
                        "region_total",
                        new SumIntegerProjection(
                            Total,
                            over: new WindowWithoutFrameWithoutRange([region])
                        )
                    ),
                    new SelectItemProjection(
                        "position",
                        new RowNumberProjection(
                            new OrderedWindow(
                                [new OrderItemRow(Total, SortDirection.Desc)],
                                [region]
                            )
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        Assert.Equal(
            ["80", "80", "70", "80", "70", "75", "75"],
            ResultColumn.Texts(projection, "region_total")
        );
        Assert.Equal(
            ["2", "3", "2", "1", "1", "1", "2"],
            ResultColumn.Texts(projection, "position")
        );
    }

    // The full join adds e, with null for every field of orders: its total
    // sorts last descending, and its region west is a partition of its own.
    [Fact]
    public void WindowsOverAFullJoinSeeTheRowsOfBothSidesThatMatchedNothing()
    {
        FieldIntegerNullable id = new("t.orders", "id");
        PureQLProjection projection = new(
            [Orders, Users],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                joins: [UsersOf(JoinKind.Full)],
                select:
                [
                    new SelectItemProjection("id", id),
                    new SelectItemProjection(
                        "region_orders",
                        new CountProjection(
                            over: new WindowWithoutFrameWithoutRange([
                                new FieldStringNullable("t.users", "region"),
                            ])
                        )
                    ),
                    new SelectItemProjection(
                        "rank",
                        new RankProjection(
                            new OrderedWindow([
                                new OrderItemRow(
                                    new FieldIntegerNullable("t.orders", "total"),
                                    SortDirection.Desc
                                ),
                            ])
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(id)]
            )
        );

        Assert.Equal(
            ["", "1", "2", "3", "4", "5", "6", "7"],
            ResultColumn.Texts(projection, "id")
        );
        Assert.Equal(
            ["1", "3", "3", "2", "3", "2", "2", "2"],
            ResultColumn.Texts(projection, "region_orders")
        );
        Assert.Equal(
            ["8", "4", "7", "5", "3", "2", "1", "6"],
            ResultColumn.Texts(projection, "rank")
        );
    }

    // items i join links l on l.ref = i.id and l.ref <> null: (0, 1),
    // (1, 3), (1, 4); the link of null ref is dropped by its guard.
    [Fact]
    public void WindowsOverAJoinOnAGuardedNullableKeyReadTheKeyAsItIs()
    {
        FieldIntegerNullable reference = new("l", "ref");
        PureQLProjection projection = new(
            [Items, Links],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                joins: [LinksOf(JoinKind.Inner)],
                select:
                [
                    new SelectItemProjection("link", new FieldInteger("l", "id")),
                    new SelectItemProjection(
                        "same_ref",
                        new CountProjection(
                            over: new WindowWithoutFrameWithoutRange([reference])
                        )
                    ),
                    new SelectItemProjection(
                        "position",
                        new RowNumberProjection(
                            new OrderedWindow(
                                [new OrderItemRow(new FieldInteger("l", "id"))],
                                [new FieldInteger("i", "id")]
                            )
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionColumn("link")]
            )
        );

        Assert.Equal(["1", "3", "4"], ResultColumn.Texts(projection, "link"));
        Assert.Equal(["1", "2", "2"], ResultColumn.Texts(projection, "same_ref"));
        Assert.Equal(["1", "1", "2"], ResultColumn.Texts(projection, "position"));
    }

    // The item 2 matches no link, so its ref is null after the left join and
    // it is alone in the null partition.
    [Fact]
    public void WindowsOverALeftJoinOnAGuardedNullableKeyPartitionTheMissingRowsApart()
    {
        PureQLProjection projection = new(
            [Items, Links],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                joins: [LinksOf(JoinKind.Left)],
                select:
                [
                    new SelectItemProjection("item", new FieldInteger("i", "id")),
                    new SelectItemProjection("link", new FieldIntegerNullable("l", "id")),
                    new SelectItemProjection(
                        "same_ref",
                        new CountProjection(
                            over: new WindowWithoutFrameWithoutRange([
                                new FieldIntegerNullable("l", "ref"),
                            ])
                        )
                    ),
                ],
                orderBy:
                [
                    new OrderItemProjectionColumn("item"),
                    new OrderItemProjectionColumn("link"),
                ]
            )
        );

        Assert.Equal(
            ["0|1|1", "1|3|2", "1|4|2", "2||1"],
            Lines(projection, "item", "link", "same_ref")
        );
    }

    // top: the two largest orders, cut by a row number it does not select.
    [Fact]
    public void SubqueryReadByInIsCutInTheOrderOfAWindowFunction()
    {
        Assert.Equal(
            ["5", "6"],
            IdsIn(
                new PlainQueryWithPagination(
                    from: new FromEntity("t.orders"),
                    pagination: new Pagination(take: 2L),
                    select: [new SelectItemProjection("id", Id)],
                    orderBy:
                    [
                        new OrderItemProjectionExpression(
                            new RowNumberProjection(
                                new OrderedWindow([
                                    new OrderItemRow(Total, SortDirection.Desc),
                                ])
                            )
                        ),
                    ]
                )
            )
        );
    }

    // By total ascending: 2, 7, 3, 1, 4, 5, 6; skip one, take two.
    [Fact]
    public void SubqueryReadByInSkipsRowsInTheOrderOfAWindowFunction()
    {
        Assert.Equal(
            ["3", "7"],
            IdsIn(
                new PlainQueryWithPagination(
                    from: new FromEntity("t.orders"),
                    pagination: new Pagination(skip: 1L, take: 2L),
                    select:
                    [
                        new SelectItemProjection("id", Id),
                        new SelectItemProjection(
                            "position",
                            new RowNumberProjection(
                                new OrderedWindow([new OrderItemRow(Total)])
                            )
                        ),
                    ],
                    orderBy: [new OrderItemProjectionColumn("position")]
                )
            )
        );
    }

    // The ranks by user are 1 (a), 4 (b), 6 (c) and 7 (d).
    [Fact]
    public void SubqueryReadByInListsTheValuesOfAWindowFunction()
    {
        Assert.Equal(
            ["1", "4", "6", "7"],
            IdsIn(
                new PlainQueryWithoutPaginationWithoutOrderBy(
                    from: new FromEntity("t.orders"),
                    select:
                    [
                        new SelectItemProjection(
                            "id",
                            new RankProjection(
                                new OrderedWindow([new OrderItemRow(User)])
                            )
                        ),
                    ]
                )
            )
        );
    }

    // distinctBy keeps, of each user, the first row in the order of the
    // window function: the largest order, 4, 5, 6 and 7.
    [Fact(
        Skip = "Bug: a subquery read by in lists the column of the rows its distinctBy"
            + " keeps (4, 5, 6, 7); the library drops distinctBy for a subquery read "
            + "by in without pagination and lists every id (1 to 7)"
    )]
    public void SubqueryReadByInKeepsTheFirstRowOfEachUserInTheOrderOfAWindowFunction()
    {
        Assert.Equal(
            ["4", "5", "6", "7"],
            IdsIn(
                new PlainQueryWithoutPaginationWithOrderBy(
                    from: new FromEntity("t.orders"),
                    distinctBy: ["user"],
                    select:
                    [
                        new SelectItemProjection("user", User),
                        new SelectItemProjection("id", Id),
                    ],
                    orderBy:
                    [
                        new OrderItemProjectionExpression(
                            new RowNumberProjection(
                                new OrderedWindow(
                                    [new OrderItemRow(Total, SortDirection.Desc)],
                                    [User]
                                )
                            )
                        ),
                    ]
                )
            )
        );
    }

    [Fact(
        Skip = "Bug: a subquery read by in lists the column of the rows its distinctBy"
            + " keeps (4, 5, 6, 7); the library drops distinctBy for a subquery read "
            + "by in without pagination and lists every id (1 to 7); not specific to "
            + "windows"
    )]
    public void SubqueryReadByInKeepsTheFirstRowOfEachUserInItsOrder()
    {
        Assert.Equal(
            ["4", "5", "6", "7"],
            IdsIn(
                new PlainQueryWithoutPaginationWithOrderBy(
                    from: new FromEntity("t.orders"),
                    distinctBy: ["user"],
                    select:
                    [
                        new SelectItemProjection("user", User),
                        new SelectItemProjection("id", Id),
                    ],
                    orderBy:
                    [
                        new OrderItemProjectionExpression(Total, SortDirection.Desc),
                    ]
                )
            )
        );
    }

    [Fact]
    public void SubqueryReadAsRowsKeepsTheFirstRowOfEachUserInTheOrderOfAWindow()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "top",
                        new PlainQueryWithoutPaginationWithOrderBy(
                            from: new FromEntity("t.orders"),
                            distinctBy: ["user"],
                            select:
                            [
                                new SelectItemProjection("user", User),
                                new SelectItemProjection("id", Id),
                            ],
                            orderBy:
                            [
                                new OrderItemProjectionExpression(
                                    new RowNumberProjection(
                                        new OrderedWindow(
                                            [new OrderItemRow(Total, SortDirection.Desc)],
                                            [User]
                                        )
                                    )
                                ),
                            ]
                        )
                    ),
                ],
                from: new FromSubquery("top"),
                select: [new SelectItemProjection("id", new FieldInteger("top", "id"))],
                orderBy: [new OrderItemProjectionColumn("id")]
            )
        );

        Assert.Equal(["4", "5", "6", "7"], ResultColumn.Texts(projection, "id"));
    }

    // The largest order of each user, kept by distinctBy in the order of a
    // window function: 4, 5, 6 and 7.
    [Fact]
    public void TotalQueryAggregatesTheRowsADistinctBySubqueryKeepsInTheOrderOfAWindow()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainTotalQuery(
                subqueries:
                [
                    new SubqueryQuery(
                        "largest",
                        new PlainQueryWithoutPaginationWithOrderBy(
                            from: new FromEntity("t.orders"),
                            distinctBy: ["user"],
                            select:
                            [
                                new SelectItemProjection("user", User),
                                new SelectItemProjection("id", Id),
                            ],
                            orderBy:
                            [
                                new OrderItemProjectionExpression(
                                    new RowNumberProjection(
                                        new OrderedWindow(
                                            [new OrderItemRow(Total, SortDirection.Desc)],
                                            [User]
                                        )
                                    )
                                ),
                            ]
                        )
                    ),
                ],
                from: new FromSubquery("largest"),
                aggregate:
                [
                    new SelectItemTotal("orders", new CountTotal()),
                    new SelectItemTotal(
                        "ids",
                        new SumIntegerTotal(new FieldInteger("largest", "id"))
                    ),
                ]
            )
        );

        Assert.Equal(["4"], ResultColumn.Texts(projection, "orders"));
        Assert.Equal(["22"], ResultColumn.Texts(projection, "ids"));
    }

    // The three largest totals, 60, 50 and 40, cut in the order of a window
    // function and then aggregated.
    [Fact]
    public void TotalQueryAggregatesAPageCutInTheOrderOfAWindowFunction()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainTotalQuery(
                subqueries:
                [
                    new SubqueryQuery(
                        "top",
                        new PlainQueryWithPagination(
                            from: new FromEntity("t.orders"),
                            pagination: new Pagination(take: 3L),
                            select: [new SelectItemProjection("total", Total)],
                            orderBy:
                            [
                                new OrderItemProjectionExpression(
                                    new RankProjection(
                                        new OrderedWindow([
                                            new OrderItemRow(Total, SortDirection.Desc),
                                        ])
                                    )
                                ),
                            ]
                        )
                    ),
                ],
                from: new FromSubquery("top"),
                aggregate:
                [
                    new SelectItemTotal(
                        "revenue",
                        new SumIntegerTotal(new FieldInteger("top", "total"))
                    ),
                ]
            )
        );

        Assert.Equal(["150"], ResultColumn.Texts(projection, "revenue"));
    }

    // Every order joined on true with the one row of a total query: the
    // running total by id over the total of all orders, 225.
    [Fact]
    public void WindowsOverACrossJoinWithATotalSubquery()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "totals",
                        new TotalQuery(
                            from: new FromEntity("t.orders"),
                            aggregate:
                            [
                                new SelectItemTotal("s", new SumIntegerTotal(Total)),
                            ]
                        )
                    ),
                ],
                from: new FromEntity("t.orders"),
                joins:
                [
                    new JoinSubquery(JoinKind.Inner, "totals", new LiteralBoolean(true)),
                ],
                select:
                [
                    new SelectItemProjection(
                        "share",
                        new DivideDecimalProjection([
                            new SumIntegerProjection(
                                Total,
                                over: new WindowWithFrame(
                                    [new OrderItemRow(Id)],
                                    new Frame(preceding: new All())
                                )
                            ),
                            new FieldInteger("totals", "s"),
                        ])
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        Assert.Equal(
            [
                "0.1333333333333333333333333333",
                "0.1777777777777777777777777778",
                "0.2666666666666666666666666667",
                "0.4444444444444444444444444444",
                "0.6666666666666666666666666667",
                "0.9333333333333333333333333333",
                "1",
            ],
            ResultColumn.Texts(projection, "share")
        );
    }

    // The two largest orders of each user: 4, 1 (a), 5, 3 (b), 6 (c), 7 (d).
    [Fact]
    public void TotalQueryAggregatesTheRowsOfASubqueryFilteredOnAWindowFunction()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainTotalQuery(
                subqueries: [Ranked()],
                from: new FromSubquery("ranked"),
                where: new LessThanOrEqualDecimalRow(
                    new FieldInteger("ranked", "position"),
                    new LiteralInteger(2)
                ),
                aggregate:
                [
                    new SelectItemTotal("orders", new CountTotal()),
                    new SelectItemTotal(
                        "revenue",
                        new SumIntegerTotal(new FieldInteger("ranked", "total"))
                    ),
                ]
            )
        );

        Assert.Equal(["6"], ResultColumn.Texts(projection, "orders"));
        Assert.Equal(["215"], ResultColumn.Texts(projection, "revenue"));
    }

    // Each input computes its windows over its own rows: a numbers 1, 2, 4
    // by id; b ranks 5 and 3 by total descending.
    [Fact]
    public void EachInputOfASetOperationComputesItsWindowsOverItsOwnRows()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "u",
                        new CombinedQueryUnionAll([
                            OfUser(
                                "a",
                                new SelectItemProjection(
                                    "n",
                                    new RowNumberProjection(ById)
                                )
                            ),
                            OfUser(
                                "b",
                                new SelectItemProjection(
                                    "n",
                                    new RankProjection(
                                        new OrderedWindow([
                                            new OrderItemRow(Total, SortDirection.Desc),
                                        ])
                                    )
                                )
                            ),
                        ])
                    ),
                ],
                from: new FromSubquery("u"),
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("u", "id")),
                    new SelectItemProjection("n", new FieldInteger("u", "n")),
                ],
                orderBy: [new OrderItemProjectionColumn("id")]
            )
        );

        Assert.Equal(["1|1", "2|2", "3|2", "4|3", "5|1"], Lines(projection, "id", "n"));
    }

    // unionAll returns the row (4, 40) twice: two peers, both in id 4's
    // partition.
    [Fact]
    public void WindowsOverACombinedSubqueryCountItsRepeatedRows()
    {
        FieldInteger id = new("u", "id");
        FieldInteger total = new("u", "total");
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "u",
                        new CombinedQueryUnionAll([
                            IdAndTotal(new EqualStringRow(User, new LiteralString("a"))),
                            IdAndTotal(
                                new GreaterThanDecimalRow(Total, new LiteralInteger(35))
                            ),
                        ])
                    ),
                ],
                from: new FromSubquery("u"),
                select:
                [
                    new SelectItemProjection("id", id),
                    new SelectItemProjection(
                        "rank",
                        new RankProjection(
                            new OrderedWindow([
                                new OrderItemRow(total, SortDirection.Desc),
                            ])
                        )
                    ),
                    new SelectItemProjection(
                        "copies",
                        new CountProjection(
                            over: new WindowWithoutFrameWithoutRange([id])
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionColumn("id")]
            )
        );

        Assert.Equal(
            ["1|5|1", "2|6|1", "4|3|2", "4|3|2", "5|2|1", "6|1|1"],
            Lines(projection, "id", "rank", "copies")
        );
    }

    // Inline rows 3, 1, 2, 2. Descending: 3, 2, 2, 1, so the two rows of 2
    // read 3 and 2 before them, in either order.
    [Fact]
    public void WindowsOverInlineRowsCountTheirDuplicates()
    {
        FieldInteger x = new("vals", "x");
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRows(
                        "vals",
                        [
                            [new RowItem("x", new LiteralInteger(3))],
                            [new RowItem("x", new LiteralInteger(1))],
                            [new RowItem("x", new LiteralInteger(2))],
                            [new RowItem("x", new LiteralInteger(2))],
                        ]
                    ),
                ],
                from: new FromSubquery("vals"),
                select:
                [
                    new SelectItemProjection("x", x),
                    new SelectItemProjection(
                        "dense",
                        new DenseRankProjection(new OrderedWindow([new OrderItemRow(x)]))
                    ),
                    new SelectItemProjection(
                        "before",
                        new PreviousIntegerNullableProjection(
                            x,
                            new OrderedWindow([new OrderItemRow(x, SortDirection.Desc)])
                        )
                    ),
                ],
                orderBy:
                [
                    new OrderItemProjectionColumn("x"),
                    new OrderItemProjectionColumn("before"),
                ]
            )
        );

        Assert.Equal(
            ["1|1|2", "2|2|2", "2|2|3", "3|3|"],
            Lines(projection, "x", "dense", "before")
        );
    }

    // A total query returns one row, also over no rows: (7, 225), or (0, 0).
    [Theory]
    [InlineData(true, "7|225|1|0|1|1|-1|225")]
    [InlineData(false, "0|0|1|0|1|1|-1|0")]
    public void WindowsOverTheOneRowOfATotalSubquery(bool rows, string expected)
    {
        FieldInteger n = new("totals", "n");
        FieldInteger s = new("totals", "s");
        OrderedWindow byN = new([new OrderItemRow(n)]);
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "totals",
                        new TotalQuery(
                            from: new FromEntity("t.orders"),
                            where: new LiteralBoolean(rows),
                            aggregate:
                            [
                                new SelectItemTotal("n", new CountTotal()),
                                new SelectItemTotal("s", new SumIntegerTotal(Total)),
                            ]
                        )
                    ),
                ],
                from: new FromSubquery("totals"),
                select:
                [
                    new SelectItemProjection("n", n),
                    new SelectItemProjection("s", s),
                    new SelectItemProjection("number", new RowNumberProjection(byN)),
                    new SelectItemProjection(
                        "percent",
                        new PercentRankProjection(
                            new OrderedWindow([new OrderItemRow(s)])
                        )
                    ),
                    new SelectItemProjection(
                        "spread",
                        new CumulativeDistributionProjection(byN)
                    ),
                    new SelectItemProjection(
                        "bucket",
                        new NtileProjection(new LiteralInteger(3), byN)
                    ),
                    new SelectItemProjection(
                        "before",
                        new PreviousIntegerProjection(n, new LiteralInteger(-1), byN)
                    ),
                    new SelectItemProjection(
                        "running",
                        new SumIntegerProjection(
                            s,
                            over: new WindowWithFrame(
                                [new OrderItemRow(n)],
                                new Frame(preceding: new All())
                            )
                        )
                    ),
                ]
            )
        );

        Assert.Equal(
            [expected],
            Lines(
                projection,
                "n",
                "s",
                "number",
                "percent",
                "spread",
                "bucket",
                "before",
                "running"
            )
        );
    }

    // nums: 1, then n + 1 while n < 4.
    [Fact]
    public void WindowsOverARecursiveSubqueryRunOverEveryLevel()
    {
        FieldInteger n = new("nums", "n");
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRecursive(
                        "nums",
                        new Recursion(
                            seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.orders"),
                                where: new EqualDecimalRow(Id, new LiteralInteger(1)),
                                select:
                                [
                                    new SelectItemProjection("n", new LiteralInteger(1)),
                                ]
                            ),
                            next: new PlainNextQuery(
                                from: new FromSubquery("nums"),
                                where: new LessThanDecimalRow(n, new LiteralInteger(4)),
                                select:
                                [
                                    new SelectItemRow(
                                        "n",
                                        new AddIntegerRow([n, new LiteralInteger(1)])
                                    ),
                                ]
                            ),
                            maxDepth: 10L
                        )
                    ),
                ],
                from: new FromSubquery("nums"),
                select:
                [
                    new SelectItemProjection("n", n),
                    new SelectItemProjection(
                        "running",
                        new SumIntegerProjection(
                            n,
                            over: new WindowWithFrame(
                                [new OrderItemRow(n)],
                                new Frame(preceding: new All())
                            )
                        )
                    ),
                    new SelectItemProjection(
                        "half",
                        new NtileProjection(
                            new LiteralInteger(2),
                            new OrderedWindow([new OrderItemRow(n)])
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionColumn("n")]
            )
        );

        Assert.Equal(
            ["1|1|1", "2|3|1", "3|6|2", "4|10|2"],
            Lines(projection, "n", "running", "half")
        );
    }

    // The seed numbers the orders of a by id: (1, 1), (2, 2), (4, 3); next
    // adds 1 to n while n < 3.
    [Fact]
    public void WindowsInTheSeedOfARecursionAreComputedOnceOverTheSeedRows()
    {
        FieldInteger id = new("rec", "id");
        FieldInteger n = new("rec", "n");
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRecursive(
                        "rec",
                        new Recursion(
                            seed: OfUser(
                                "a",
                                new SelectItemProjection(
                                    "n",
                                    new RowNumberProjection(ById)
                                )
                            ),
                            next: new PlainNextQuery(
                                from: new FromSubquery("rec"),
                                where: new LessThanDecimalRow(n, new LiteralInteger(3)),
                                select:
                                [
                                    new SelectItemRow("id", id),
                                    new SelectItemRow(
                                        "n",
                                        new AddIntegerRow([n, new LiteralInteger(1)])
                                    ),
                                ]
                            ),
                            maxDepth: 10L
                        )
                    ),
                ],
                from: new FromSubquery("rec"),
                select:
                [
                    new SelectItemProjection("id", id),
                    new SelectItemProjection("n", n),
                ],
                orderBy:
                [
                    new OrderItemProjectionColumn("id"),
                    new OrderItemProjectionColumn("n"),
                ]
            )
        );

        Assert.Equal(
            ["1|1", "1|2", "1|3", "2|2", "2|3", "4|3"],
            Lines(projection, "id", "n")
        );
    }

    // The orders above 4 are 10 (5), 11 (7) and 12 (9): the rows the
    // condition drops are rows the table does not have.
    [Fact]
    public void WindowsOverAFilteredTableSeeOnlyTheRowsItsConditionKeeps()
    {
        FieldInteger id = new("t.orders", "id");
        FieldInteger total = new("t.orders", "total");
        OrderedWindow byId = new([new OrderItemRow(id)]);
        PureQLProjection projection = new(
            [Staff.Filtered(Staff.Schema(), ("orders", Staff.BigOrders))],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                select:
                [
                    new SelectItemProjection("id", id),
                    new SelectItemProjection("number", new RowNumberProjection(byId)),
                    new SelectItemProjection(
                        "before",
                        new PreviousIntegerNullableProjection(total, byId)
                    ),
                    new SelectItemProjection(
                        "running",
                        new SumIntegerProjection(
                            total,
                            over: new WindowWithFrame(
                                [new OrderItemRow(id)],
                                new Frame(preceding: new All())
                            )
                        )
                    ),
                    new SelectItemProjection(
                        "rank",
                        new RankProjection(
                            new OrderedWindow([
                                new OrderItemRow(total, SortDirection.Desc),
                            ])
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionColumn("id")]
            )
        );

        Assert.Equal(
            ["10|1||5|3", "11|2|5|12|2", "12|3|7|21|1"],
            Lines(projection, "id", "number", "before", "running", "rank")
        );
    }

    // People of the red team: Ann (1) and Dee (4); their orders are 10 (5)
    // and 13 (1) of Ann, 12 (9) of Dee.
    [Fact]
    public void WindowsOverAJoinOfAFilteredTablePartitionTheKeptRows()
    {
        PureQLProjection projection = new(
            [Staff.Filtered(Staff.Schema(), ("people", Staff.RedTeam))],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders", "o"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Inner,
                        "t.people",
                        new EqualDecimalRow(
                            new FieldInteger("o", "person"),
                            new FieldInteger("p", "id")
                        ),
                        "p"
                    ),
                ],
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("o", "id")),
                    new SelectItemProjection(
                        "rank",
                        new RankProjection(
                            new OrderedWindow(
                                [
                                    new OrderItemRow(
                                        new FieldInteger("o", "total"),
                                        SortDirection.Desc
                                    ),
                                ],
                                [new FieldString("p", "name")]
                            )
                        )
                    ),
                    new SelectItemProjection(
                        "team_orders",
                        new CountProjection(
                            over: new WindowWithoutFrameWithoutRange([
                                new FieldStringNullable("p", "team"),
                            ])
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionColumn("id")]
            )
        );

        Assert.Equal(
            ["10|1|3", "12|1|3", "13|2|3"],
            Lines(projection, "id", "rank", "team_orders")
        );
    }

    // The ranks by user are 1 (a), 4 (b), 6 (c), 7 (d); distinct, descending,
    // after the first: 6 and 4.
    [Fact]
    public void DistinctByAWindowColumnThenAPageInItsDescendingOrder()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithPagination(
                from: new FromEntity("t.orders"),
                pagination: new Pagination(skip: 1L, take: 2L),
                select:
                [
                    new SelectItemProjection(
                        "r",
                        new RankProjection(new OrderedWindow([new OrderItemRow(User)]))
                    ),
                    new SelectItemProjection("user", User),
                ],
                orderBy: [new OrderItemProjectionColumn("r", SortDirection.Desc)],
                distinctBy: ["r"]
            )
        );

        Assert.Equal(["6|c", "4|b"], Lines(projection, "r", "user"));
    }

    // ntile of two buckets over seven rows: 1 to 4 in the first, 5 to 7 in
    // the second, whatever page is returned.
    [Theory]
    [InlineData(0, "1|1,2|1,3|1")]
    [InlineData(2, "3|1,4|1,5|2")]
    [InlineData(6, "7|2")]
    public void PageSkippedByAParameterKeepsTheWindowValuesOfEveryRow(
        long skip,
        string expected
    )
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithPagination(
                from: new FromEntity("t.orders"),
                pagination: new Pagination(skip: new ParamInteger("skip"), take: 3L),
                select:
                [
                    new SelectItemProjection("id", Id),
                    new SelectItemProjection(
                        "bucket",
                        new NtileProjection(new LiteralInteger(2), ById)
                    ),
                ],
                orderBy: [new OrderItemProjectionColumn("id")]
            ),
            new Dictionary<string, IParameter>
            {
                ["skip"] = new IntegerParameter(new Long(skip)),
            }
        );

        Assert.Equal(expected.Split(','), Lines(projection, "id", "bucket"));
    }

    // Users by their number of orders, descending (a 3, b 2, c and d 1),
    // then by the position of the total among all orders.
    [Fact]
    public void QueryIsSortedByTwoWindowFunctionsItDoesNotSelect()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                select: [new SelectItemProjection("id", Id)],
                orderBy:
                [
                    new OrderItemProjectionExpression(
                        new CountProjection(
                            over: new WindowWithoutFrameWithoutRange([User])
                        ),
                        SortDirection.Desc
                    ),
                    new OrderItemProjectionExpression(
                        new RowNumberProjection(
                            new OrderedWindow([new OrderItemRow(Total)])
                        )
                    ),
                ]
            )
        );

        Assert.Equal(
            ["2", "1", "4", "3", "5", "7", "6"],
            ResultColumn.Texts(projection, "id")
        );
    }

    // A null before is not below the total: only 4 (10 before) and 5 (20
    // before) are above the order before them.
    [Fact]
    public void OuterQueryFiltersOnPreviousComputedInItsSubquery()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "sub",
                        new PlainQueryWithoutPaginationWithoutOrderBy(
                            from: new FromEntity("t.orders"),
                            select:
                            [
                                new SelectItemProjection("id", Id),
                                new SelectItemProjection("total", Total),
                                new SelectItemProjection(
                                    "before",
                                    new PreviousIntegerNullableProjection(
                                        Total,
                                        PerUserById
                                    )
                                ),
                            ]
                        )
                    ),
                ],
                from: new FromSubquery("sub"),
                where: new GreaterThanDecimalRow(
                    new FieldInteger("sub", "total"),
                    new FieldIntegerNullable("sub", "before")
                ),
                select: [new SelectItemProjection("id", new FieldInteger("sub", "id"))],
                orderBy: [new OrderItemProjectionColumn("id")]
            )
        );

        Assert.Equal(["4", "5"], ResultColumn.Texts(projection, "id"));
    }

    [Fact]
    public void WindowsThatDifferOnlyInDirectionAreComputedApart()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                select:
                [
                    new SelectItemProjection(
                        "v",
                        new AddIntegerProjection([
                            new RowNumberProjection(ById),
                            new RowNumberProjection(
                                new OrderedWindow([
                                    new OrderItemRow(Id, SortDirection.Desc),
                                ])
                            ),
                        ])
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        Assert.Equal(Enumerable.Repeat("8", 7), ResultColumn.Texts(projection, "v"));
    }

    // ranked numbers each user's orders by id: a 1, 2, 4; b 3, 5; c 6; d 7.
    // Read twice, it pairs every order with the next one of its user.
    [Fact]
    public void SubqueryWithAWindowJoinedToItselfGivesTheSameValuesOnBothSides()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                subqueries: [Ranked()],
                from: new FromSubquery("ranked", "r1"),
                joins:
                [
                    new JoinSubquery(
                        JoinKind.Inner,
                        "ranked",
                        new AndRow([
                            new EqualStringRow(
                                new FieldString("r1", "user"),
                                new FieldString("r2", "user")
                            ),
                            new EqualDecimalRow(
                                new FieldInteger("r2", "number"),
                                new AddIntegerRow([
                                    new FieldInteger("r1", "number"),
                                    new LiteralInteger(1),
                                ])
                            ),
                        ]),
                        "r2"
                    ),
                ],
                select:
                [
                    new SelectItemProjection("first", new FieldInteger("r1", "id")),
                    new SelectItemProjection("second", new FieldInteger("r2", "id")),
                ],
                orderBy: [new OrderItemProjectionColumn("first")]
            )
        );

        Assert.Equal(["1|2", "2|4", "3|5"], Lines(projection, "first", "second"));
    }

    // latest keeps the highest id of each user, after its windows: the count
    // of the user's orders and the running total over every order by id.
    [Fact]
    public void WindowsOfASubqueryAreComputedBeforeItsDistinctBy()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "latest",
                        new PlainQueryWithoutPaginationWithOrderBy(
                            from: new FromEntity("t.orders"),
                            distinctBy: ["user"],
                            select:
                            [
                                new SelectItemProjection("user", User),
                                new SelectItemProjection("id", Id),
                                new SelectItemProjection(
                                    "n",
                                    new CountProjection(
                                        over: new WindowWithoutFrameWithoutRange([User])
                                    )
                                ),
                                new SelectItemProjection(
                                    "running",
                                    new SumIntegerProjection(
                                        Total,
                                        over: new WindowWithFrame(
                                            [new OrderItemRow(Id)],
                                            new Frame(preceding: new All())
                                        )
                                    )
                                ),
                            ],
                            orderBy:
                            [
                                new OrderItemProjectionColumn("id", SortDirection.Desc),
                            ]
                        )
                    ),
                ],
                from: new FromSubquery("latest"),
                select:
                [
                    new SelectItemProjection("user", new FieldString("latest", "user")),
                    new SelectItemProjection("id", new FieldInteger("latest", "id")),
                    new SelectItemProjection("n", new FieldInteger("latest", "n")),
                    new SelectItemProjection(
                        "running",
                        new FieldInteger("latest", "running")
                    ),
                ],
                orderBy: [new OrderItemProjectionColumn("user")]
            )
        );

        Assert.Equal(
            ["a|4|3|100", "b|5|2|150", "c|6|1|210", "d|7|1|225"],
            Lines(projection, "user", "id", "n", "running")
        );
    }

    // After the left join: regions north, north, south, north, south, null
    // (c has none), null (d has no user); names a, a, b, a, b, c, null. The
    // nulls read from the other row are returned as they are; the default
    // stands only for the missing row.
    [Fact]
    public void PreviousAndNextOfAnOuterJoinedFieldReturnTheNullsOfTheOtherRow()
    {
        PureQLProjection projection = new(
            [Orders, Users],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                joins: [UsersOf(JoinKind.Left)],
                select:
                [
                    new SelectItemProjection("id", Id),
                    new SelectItemProjection(
                        "before",
                        new PreviousStringNullableProjection(
                            new FieldStringNullable("t.users", "region"),
                            ById,
                            @default: new LiteralString("none")
                        )
                    ),
                    new SelectItemProjection(
                        "after",
                        new NextStringNullableProjection(
                            new FieldStringNullable("t.users", "name"),
                            ById,
                            @default: new LiteralString("-")
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionColumn("id")]
            )
        );

        Assert.Equal(
            [
                "1|none|a",
                "2|north|b",
                "3|north|a",
                "4|south|b",
                "5|north|c",
                "6|south|",
                "7||-",
            ],
            Lines(projection, "id", "before", "after")
        );
    }

    // The orders of a and c: 1, 2, 4 and 6.
    [Fact]
    public void WindowAggregateWithAPredicateReadingASubqueryColumn()
    {
        InStringRow picked = new(User, new ListSubqueryColumnString("picked", "user"));
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRows(
                        "picked",
                        [
                            [new RowItem("user", new LiteralString("a"))],
                            [new RowItem("user", new LiteralString("c"))],
                        ]
                    ),
                ],
                from: new FromEntity("t.orders"),
                select:
                [
                    new SelectItemProjection(
                        "running",
                        new CountProjection(
                            predicate: picked,
                            over: new WindowWithFrame(
                                [new OrderItemRow(Id)],
                                new Frame(preceding: new All())
                            )
                        )
                    ),
                    new SelectItemProjection(
                        "pair",
                        new SumIntegerProjection(
                            Total,
                            predicate: picked,
                            over: new WindowWithFrame(
                                [new OrderItemRow(Id)],
                                new Frame(preceding: 1L)
                            )
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        Assert.Equal(
            ["1|30", "2|40", "2|10", "3|40", "3|40", "4|60", "4|60"],
            Lines(projection, "running", "pair")
        );
    }

    // Every order paired with every order of its user, numbered in each
    // order's partition; a has 9 pairs, b 4, c and d 1.
    [Fact]
    public void WindowsOverASelfJoinOfAStoredTableNumberThePairs()
    {
        FieldInteger first = new("o1", "id");
        FieldInteger second = new("o2", "id");
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders", "o1"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Inner,
                        "t.orders",
                        new EqualStringRow(
                            new FieldString("o1", "user"),
                            new FieldString("o2", "user")
                        ),
                        "o2"
                    ),
                ],
                select:
                [
                    new SelectItemProjection("first", first),
                    new SelectItemProjection("second", second),
                    new SelectItemProjection(
                        "n",
                        new RowNumberProjection(
                            new OrderedWindow([new OrderItemRow(second)], [first])
                        )
                    ),
                    new SelectItemProjection(
                        "pairs",
                        new CountProjection(
                            over: new WindowWithoutFrameWithoutRange([
                                new FieldString("o1", "user"),
                            ])
                        )
                    ),
                ],
                orderBy:
                [
                    new OrderItemProjectionColumn("first"),
                    new OrderItemProjectionColumn("second"),
                ]
            )
        );

        Assert.Equal(
            [
                "1|1|1|9",
                "1|2|2|9",
                "1|4|3|9",
                "2|1|1|9",
                "2|2|2|9",
                "2|4|3|9",
                "3|3|1|4",
                "3|5|2|4",
                "4|1|1|9",
                "4|2|2|9",
                "4|4|3|9",
                "5|3|1|4",
                "5|5|2|4",
                "6|6|1|1",
                "7|7|1|1",
            ],
            Lines(projection, "first", "second", "n", "pairs")
        );
    }

    // The main query's previous reads a number computed by the window of
    // the subquery it joins.
    [Fact]
    public void WindowsOfTheMainQueryReadTheWindowValuesOfAJoinedSubquery()
    {
        FieldInteger number = new("ranked", "number");
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                subqueries: [Ranked()],
                from: new FromEntity("t.orders"),
                joins:
                [
                    new JoinSubquery(
                        JoinKind.Inner,
                        "ranked",
                        new EqualDecimalRow(new FieldInteger("ranked", "id"), Id)
                    ),
                ],
                select:
                [
                    new SelectItemProjection("id", Id),
                    new SelectItemProjection("n", number),
                    new SelectItemProjection(
                        "before",
                        new PreviousIntegerNullableProjection(number, PerUserById)
                    ),
                ],
                orderBy: [new OrderItemProjectionColumn("id")]
            )
        );

        Assert.Equal(
            ["1|1|", "2|2|1", "3|1|", "4|3|2", "5|2|1", "6|1|", "7|1|"],
            Lines(projection, "id", "n", "before")
        );
    }

    // By user: a 1, b 4, c 6, d 7 over every order; a 1, b 3, c 5 over the
    // orders above 15. Each input runs on its own.
    [Fact]
    public void SetOperationsCompareTheWindowValuesOfEachInput()
    {
        PureQLProjection intersect = new(
            [Orders],
            new MainCombinedQueryIntersect([UserRanks(null), UserRanks(Above15)])
        );
        PureQLProjection except = new(
            [Orders],
            new MainCombinedQueryExcept([UserRanks(null), UserRanks(Above15)])
        );

        Assert.Equal(["a|1"], Lines(intersect, "user", "r"));
        Assert.Equal(
            ["b|4", "c|6", "d|7"],
            Lines(except, "user", "r").Order(StringComparer.Ordinal)
        );
    }

    // distinctBy listing every alias removes duplicate rows; no order is
    // needed, so the rows are compared as a set.
    [Fact]
    public void DistinctByEveryAliasRemovesTheRowsEqualOnTheirWindowValues()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                select:
                [
                    new SelectItemProjection("user", User),
                    new SelectItemProjection(
                        "n",
                        new CountProjection(
                            over: new WindowWithoutFrameWithoutRange([User])
                        )
                    ),
                ],
                distinctBy: ["user", "n"]
            )
        );

        Assert.Equal(
            ["a|3", "b|2", "c|1", "d|1"],
            Lines(projection, "user", "n").Order(StringComparer.Ordinal)
        );
    }

    // where drops the order of 10, the only row whose key 100 / (total - 10)
    // divides by zero: windows run over the rows where keeps, so the key is
    // never evaluated there (Evaluation). Keys: 5, 10, 3, 2, 2, 20.
    [Fact]
    public void PartitionKeyIsNotEvaluatedForTheRowsWhereDrops()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                where: new NotEqualDecimalRow(Total, new LiteralInteger(10)),
                select:
                [
                    new SelectItemProjection("id", Id),
                    new SelectItemProjection(
                        "same_key",
                        new CountProjection(
                            over: new WindowWithoutFrameWithoutRange([
                                new IntegerDivideIntegerRow(
                                    new LiteralInteger(100),
                                    new SubtractIntegerRow([
                                        Total,
                                        new LiteralInteger(10),
                                    ])
                                ),
                            ])
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionColumn("id")]
            )
        );

        Assert.Equal(
            ["1|1", "3|1", "4|1", "5|2", "6|2", "7|1"],
            Lines(projection, "id", "same_key")
        );
    }

    // The running sum of n is 2^63 - 1, then beyond 64 bits (an error), then
    // 2^63 - 2. Laziness: if evaluates only the branch it takes, so the sum
    // of the second row is never evaluated and nothing fails.
    [Fact(
        Skip = "Bug: Laziness says if evaluates only the branch it takes, so the sum "
            + "of row 2 (beyond 64 bits) is never evaluated; the library computes "
            + "every window value of every row before the select and throws "
            + "OverflowException"
    )]
    public void WindowSumInABranchThatIfDoesNotTakeIsNotEvaluated()
    {
        Assert.Equal(
            ["9223372036854775807", "0", "9223372036854775806"],
            NumberValues(
                new IfIntegerProjection(
                    new LiteralInteger(0),
                    RunningSumOfN,
                    new EqualDecimalProjection(NumberId, new LiteralInteger(2))
                )
            )
        );
    }

    // Laziness: and stops at the first false condition.
    [Fact(
        Skip = "Bug: Laziness says and stops at the first false condition, so the sum "
            + "of row 2 (beyond 64 bits) is never evaluated; the library computes "
            + "every window value of every row before the select and throws "
            + "OverflowException"
    )]
    public void WindowSumAfterAFalseConditionOfAndIsNotEvaluated()
    {
        Assert.Equal(
            ["True", "False", "True"],
            NumberValues(
                new AndProjection([
                    new NotEqualDecimalProjection(NumberId, new LiteralInteger(2)),
                    new GreaterThanDecimalProjection(
                        RunningSumOfN,
                        new LiteralInteger(0)
                    ),
                ])
            )
        );
    }

    // The default of previous is evaluated only in the first row, where if
    // takes the other branch, so its division by zero never happens.
    [Fact(
        Skip = "Bug: the default of previous is evaluated only where there is no row "
            + "before (row 1) and if does not take that branch there (Laziness); the "
            + "library evaluates previous with its default in every row and throws "
            + "DivideByZeroException"
    )]
    public void PreviousDefaultInABranchThatIfDoesNotTakeIsNotEvaluated()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                select:
                [
                    new SelectItemProjection(
                        "v",
                        new IfIntegerProjection(
                            new LiteralInteger(0),
                            new PreviousIntegerProjection(
                                Total,
                                new IntegerDivideIntegerRow(Total, new LiteralInteger(0)),
                                ById
                            ),
                            new EqualDecimalProjection(Id, new LiteralInteger(1))
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        Assert.Equal(
            ["0", "30", "10", "20", "40", "50", "60"],
            ResultColumn.Texts(projection, "v")
        );
    }

    private static readonly IStoredSchemaDataSet Numbers = new InlineTable(
        "t",
        "numbers",
        ("id", new LongColumnType()),
        ("n", new LongColumnType())
    ).With(["1", "9223372036854775807"], ["2", "1"], ["3", "-2"]);

    private static readonly FieldInteger NumberId = new("t.numbers", "id");

    private static readonly SumIntegerProjection RunningSumOfN = new(
        new FieldInteger("t.numbers", "n"),
        over: new WindowWithFrame(
            [new OrderItemRow(new FieldInteger("t.numbers", "id"))],
            new Frame(preceding: new All())
        )
    );

    private static readonly GreaterThanDecimalRow Above15 = new(
        Total,
        new LiteralInteger(15)
    );

    private static IReadOnlyList<string> NumberValues(ExprProjection expression)
    {
        PureQLProjection projection = new(
            [Numbers],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.numbers"),
                select: [new SelectItemProjection("v", expression)],
                orderBy: [new OrderItemProjectionExpression(NumberId)]
            )
        );

        return ResultColumn.Texts(projection, "v");
    }

    private static PlainQueryWithoutPaginationWithoutOrderBy UserRanks(BooleanRow? where)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.orders"),
            where: where,
            select:
            [
                new SelectItemProjection("user", User),
                new SelectItemProjection(
                    "r",
                    new RankProjection(new OrderedWindow([new OrderItemRow(User)]))
                ),
            ]
        );
    }

    private static JoinEntity UsersOf(JoinKind kind)
    {
        return new JoinEntity(
            kind,
            "t.users",
            new EqualStringRow(User, new FieldString("t.users", "name"))
        );
    }

    private static JoinEntity LinksOf(JoinKind kind)
    {
        FieldIntegerNullable reference = new("l", "ref");

        return new JoinEntity(
            kind,
            "t.links",
            new AndRow([
                new EqualDecimalRow(reference, new FieldInteger("i", "id")),
                new NotEqualDecimalRow(reference, new LiteralIntegerNullable()),
            ]),
            "l"
        );
    }

    private static SubqueryQuery Ranked()
    {
        return new SubqueryQuery(
            "ranked",
            new PlainQueryWithoutPaginationWithoutOrderBy(
                from: new FromEntity("t.orders"),
                select:
                [
                    new SelectItemProjection("id", Id),
                    new SelectItemProjection("user", User),
                    new SelectItemProjection("total", Total),
                    new SelectItemProjection(
                        "number",
                        new RowNumberProjection(PerUserById)
                    ),
                    new SelectItemProjection(
                        "position",
                        new RowNumberProjection(
                            new OrderedWindow(
                                [new OrderItemRow(Total, SortDirection.Desc)],
                                [User]
                            )
                        )
                    ),
                ]
            )
        );
    }

    private static PlainQueryWithoutPaginationWithoutOrderBy OfUser(
        string user,
        SelectItemProjection column
    )
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.orders"),
            where: new EqualStringRow(User, new LiteralString(user)),
            select: [new SelectItemProjection("id", Id), column]
        );
    }

    private static PlainQueryWithoutPaginationWithoutOrderBy IdAndTotal(BooleanRow where)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.orders"),
            where: where,
            select:
            [
                new SelectItemProjection("id", Id),
                new SelectItemProjection("total", Total),
            ]
        );
    }

    // The ids of the orders whose id is in the column id of the subquery.
    private static IReadOnlyList<string> IdsIn(Query subquery)
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                subqueries: [new SubqueryQuery("top", subquery)],
                from: new FromEntity("t.orders"),
                where: new InDecimalRow(Id, new ListSubqueryColumnInteger("top", "id")),
                select: [new SelectItemProjection("id", Id)],
                orderBy: [new OrderItemProjectionColumn("id")]
            )
        );

        return ResultColumn.Texts(projection, "id");
    }

    // Each row, read in one run, as the texts of the given columns joined by
    // '|'.
    private static IReadOnlyList<string> Lines(
        PureQLProjection projection,
        params string[] columns
    )
    {
        List<IRow> rows = [.. projection];
        IReadOnlyList<IReadOnlyList<string>> texts =
        [
            .. columns.Select(column => ResultColumn.Texts(rows, column)),
        ];

        return
        [
            .. Enumerable
                .Range(0, texts[0].Count)
                .Select(row => string.Join('|', texts.Select(column => column[row]))),
        ];
    }
}
