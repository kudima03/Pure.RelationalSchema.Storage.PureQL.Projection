using Pure.Primitives.Number;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Parameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.GroupExpressions;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Where window functions stand in a query: in the select and the orderBy of
// a plain query, also as operands of other operators, each with its own
// window. They are computed over the rows left by from, joins and where, and
// before orderBy, distinctBy and pagination; a window never sorts the
// result. A condition on a window function belongs to a query that reads
// this one as a subquery, and windows over grouped rows read a grouped
// subquery.
public sealed class WindowPlacementTests
{
    // user a: 1 (30), 2 (10), 4 (40), 7 (30); user b: 3 (20), 5 (50), 6 (20);
    // user c: 8 (60).
    private static readonly IStoredSchemaDataSet Orders = new InlineTable(
        "t",
        "orders",
        ("id", new LongColumnType()),
        ("user", new StringColumnType()),
        ("total", new LongColumnType()),
        ("month", new DateColumnType())
    ).With(
        ["1", "a", "30", "2024-01-01"],
        ["2", "a", "10", "2024-01-01"],
        ["3", "b", "20", "2024-01-01"],
        ["4", "a", "40", "2024-02-01"],
        ["5", "b", "50", "2024-02-01"],
        ["6", "b", "20", "2024-03-01"],
        ["7", "a", "30", "2024-03-01"],
        ["8", "c", "60", "2024-03-01"]
    );

    private static readonly IStoredSchemaDataSet Users = new InlineTable(
        "t",
        "users",
        ("name", new StringColumnType()),
        ("region", new StringColumnType())
    ).With(["a", "north"], ["b", "south"], ["c", "north"]);

    private static readonly FieldInteger Id = new("t.orders", "id");

    private static readonly FieldString User = new("t.orders", "user");

    private static readonly FieldInteger Total = new("t.orders", "total");

    private static readonly FieldDate Month = new("t.orders", "month");

    private static readonly OrderedWindow PerUserById = new(
        [new OrderItemRow(Id)],
        [User]
    );

    private static readonly OrderedWindow PerUserByTotalDescending = new(
        [new OrderItemRow(Total, SortDirection.Desc), new OrderItemRow(Id)],
        [User]
    );

    private static readonly WindowWithoutFrameWithoutRange PerUser = new([User]);

    private static readonly RowNumberProjection Position = new(PerUserByTotalDescending);

    [Fact]
    public void WindowFunctionsOfOneSelectEachReadTheirOwnWindow()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                select:
                [
                    new SelectItemProjection("id", Id),
                    new SelectItemProjection("position", Position),
                    new SelectItemProjection(
                        "user_total",
                        new SumIntegerProjection(Total, over: PerUser)
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
                    new SelectItemProjection(
                        "before",
                        new PreviousIntegerNullableProjection(Total, PerUserById)
                    ),
                    new SelectItemProjection("all", new SumIntegerProjection(Total)),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        Assert.Equal(
            ["1", "2", "3", "4", "5", "6", "7", "8"],
            ResultColumn.Texts(projection, "id")
        );
        Assert.Equal(
            ["2", "4", "2", "1", "1", "3", "3", "1"],
            ResultColumn.Texts(projection, "position")
        );
        Assert.Equal(
            ["110", "110", "90", "110", "90", "90", "110", "60"],
            ResultColumn.Texts(projection, "user_total")
        );
        Assert.Equal(
            ["30", "40", "60", "100", "150", "170", "200", "260"],
            ResultColumn.Texts(projection, "running")
        );
        Assert.Equal(
            ["", "30", "", "10", "20", "50", "40", ""],
            ResultColumn.Texts(projection, "before")
        );
        Assert.Equal(
            Enumerable.Repeat("260", 8),
            ResultColumn.Texts(projection, "all")
        );
    }

    [Fact]
    public void RankingFunctionIsAnOperandOfAComparisonInsideIf()
    {
        Assert.Equal(
            ["first", "repeat", "first", "repeat", "repeat", "repeat", "repeat", "first"],
            Values(
                new IfStringProjection(
                    new LiteralString("first"),
                    new LiteralString("repeat"),
                    new EqualDecimalProjection(
                        new RowNumberProjection(PerUserById),
                        new LiteralInteger(1)
                    )
                )
            )
        );
    }

    [Fact]
    public void RowValueIsComparedWithAMovingAverage()
    {
        Assert.Equal(
            ["False", "False", "False", "True", "True", "False", "False", "False"],
            Values(
                new GreaterThanDecimalProjection(
                    Total,
                    new AverageDecimalProjection(
                        Total,
                        over: new WindowWithFrame(
                            [new OrderItemRow(Id)],
                            new Frame(preceding: 1L),
                            [User]
                        )
                    )
                )
            )
        );
    }

    [Fact]
    public void PreviousIsAnOperandOfCoalesce()
    {
        Assert.Equal(
            ["0", "30", "0", "10", "20", "50", "40", "0"],
            Values(
                new CoalesceIntegerProjection([
                    new PreviousIntegerNullableProjection(Total, PerUserById),
                    new LiteralInteger(0),
                ])
            )
        );
    }

    [Fact]
    public void TwoWindowFunctionsOfDifferentWindowsAreOperandsOfOneOperator()
    {
        Assert.Equal(
            ["5", "10", "9", "7", "7", "12", "11", "9"],
            Values(
                new AddIntegerProjection([
                    new RowNumberProjection(new OrderedWindow([new OrderItemRow(Id)])),
                    new RankProjection(
                        new OrderedWindow([new OrderItemRow(Total, SortDirection.Desc)])
                    ),
                ])
            )
        );
    }

    [Fact]
    public void RunningShareDividesAWindowAggregateByAnAggregateOverAllRows()
    {
        Assert.Equal(
            ["0.2222222222222222222222222222", "0.7777777777777777777777777778", "1"],
            Values(
                new DivideDecimalProjection([
                    new SumIntegerProjection(
                        Total,
                        over: new WindowWithFrame(
                            [new OrderItemRow(Id)],
                            new Frame(preceding: new All())
                        )
                    ),
                    new SumIntegerProjection(Total),
                ]),
                where: new EqualStringRow(User, new LiteralString("b"))
            )
        );
    }

    [Fact]
    public void QueryIsSortedByAWindowFunctionItDoesNotSelect()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                select: [new SelectItemProjection("id", Id)],
                orderBy:
                [
                    new OrderItemProjectionExpression(Position),
                    new OrderItemProjectionExpression(User),
                ]
            )
        );

        Assert.Equal(
            ["4", "5", "8", "1", "3", "7", "6", "2"],
            ResultColumn.Texts(projection, "id")
        );
    }

    [Fact]
    public void QueryIsSortedByAnAggregateOverAPartitionDescending()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                select: [new SelectItemProjection("id", Id)],
                orderBy:
                [
                    new OrderItemProjectionExpression(
                        new SumIntegerProjection(Total, over: PerUser),
                        SortDirection.Desc
                    ),
                    new OrderItemProjectionExpression(Id),
                ]
            )
        );

        Assert.Equal(
            ["1", "2", "4", "7", "3", "5", "6", "8"],
            ResultColumn.Texts(projection, "id")
        );
    }

    [Fact]
    public void QueryIsSortedByTheColumnOfAWindowFunction()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                select:
                [
                    new SelectItemProjection("id", Id),
                    new SelectItemProjection("position", Position),
                ],
                orderBy:
                [
                    new OrderItemProjectionColumn("position", SortDirection.Desc),
                    new OrderItemProjectionColumn("id"),
                ]
            )
        );

        Assert.Equal(
            ["2", "6", "7", "1", "3", "4", "5", "8"],
            ResultColumn.Texts(projection, "id")
        );
        Assert.Equal(
            ["4", "3", "3", "2", "2", "1", "1", "1"],
            ResultColumn.Texts(projection, "position")
        );
    }

    [Fact]
    public void WindowFunctionsSeeOnlyTheRowsLeftByWhere()
    {
        Assert.Equal(
            ["1", "1", "2", "2", "3", "3", "1"],
            Values(
                new RowNumberProjection(PerUserById),
                where: new GreaterThanDecimalRow(Total, new LiteralInteger(15))
            )
        );
    }

    [Fact]
    public void WindowFunctionsRunOverTheJoinedRows()
    {
        PureQLProjection projection = new(
            [Orders, Users],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Inner,
                        "t.users",
                        new EqualStringRow(User, new FieldString("t.users", "name"))
                    ),
                ],
                select:
                [
                    new SelectItemProjection(
                        "region_total",
                        new SumIntegerProjection(
                            Total,
                            over: new WindowWithoutFrameWithoutRange([
                                new FieldString("t.users", "region"),
                            ])
                        )
                    ),
                    new SelectItemProjection(
                        "region_rank",
                        new RankProjection(
                            new OrderedWindow(
                                [new OrderItemRow(Total, SortDirection.Desc)],
                                [new FieldString("t.users", "region")]
                            )
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        Assert.Equal(
            ["170", "170", "90", "170", "90", "90", "170", "170"],
            ResultColumn.Texts(projection, "region_total")
        );
        Assert.Equal(
            ["3", "5", "2", "2", "1", "2", "3", "1"],
            ResultColumn.Texts(projection, "region_rank")
        );
    }

    [Fact]
    public void QueryWithoutOrderByReturnsOneRowPerRowWithItsWindowValue()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                select:
                [
                    new SelectItemProjection("id", Id),
                    new SelectItemProjection("position", Position),
                ]
            )
        );

        Assert.Equal(
            ["1:2", "2:4", "3:2", "4:1", "5:1", "6:3", "7:3", "8:1"],
            ResultColumn
                .Texts(projection, "id")
                .Zip(
                    ResultColumn.Texts(projection, "position"),
                    (id, position) => $"{id}:{position}"
                )
                .Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void ConditionOnAWindowFunctionFiltersTheSubqueryThatComputesIt()
    {
        PureQLProjection projection = new(
            [Orders],
            TopPerUser(new LiteralInteger(2))
        );

        Assert.Equal(["4", "1", "5", "3", "8"], ResultColumn.Texts(projection, "id"));
        Assert.Equal(
            ["1", "2", "1", "2", "1"],
            ResultColumn.Texts(projection, "position")
        );
        Assert.Equal(
            ["4", "4", "3", "3", "1"],
            ResultColumn.Texts(projection, "user_orders")
        );
    }

    [Theory]
    [InlineData(1, "4,5,8")]
    [InlineData(3, "4,1,7,5,3,6,8")]
    [InlineData(10, "4,1,7,2,5,3,6,8")]
    public void TopOrdersOfEachUserAreCutByAParameter(long top, string expected)
    {
        PureQLProjection projection = new(
            [Orders],
            TopPerUser(new ParamInteger("top")),
            new Dictionary<string, IParameter>
            {
                ["top"] = new IntegerParameter(new Long(top)),
            }
        );

        Assert.Equal(expected.Split(','), ResultColumn.Texts(projection, "id"));
    }

    [Fact]
    public void WindowsOfAQueryThatReadsAGroupedSubqueryRunOverTheGroupedRows()
    {
        FieldString user = new("monthly", "user");
        FieldDate month = new("monthly", "month");
        FieldInteger revenue = new("monthly", "revenue");
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "monthly",
                        new GroupedQueryWithoutPaginationWithoutOrderBy(
                            from: new FromEntity("t.orders"),
                            groupBy:
                            [
                                new GroupKey("user", User),
                                new GroupKey("month", Month),
                            ],
                            select:
                            [
                                new SelectItemGroup("user", new KeyString("user")),
                                new SelectItemGroup("month", new KeyDate("month")),
                                new SelectItemGroup(
                                    "revenue",
                                    new SumIntegerGroup(Total)
                                ),
                            ]
                        )
                    ),
                ],
                from: new FromSubquery("monthly"),
                select:
                [
                    new SelectItemProjection("revenue", revenue),
                    new SelectItemProjection(
                        "running",
                        new SumIntegerProjection(
                            revenue,
                            over: new WindowWithFrame(
                                [new OrderItemRow(month)],
                                new Frame(preceding: new All()),
                                [user]
                            )
                        )
                    ),
                    new SelectItemProjection(
                        "change",
                        new SubtractIntegerNullableProjection([
                            revenue,
                            new PreviousIntegerNullableProjection(
                                revenue,
                                new OrderedWindow([new OrderItemRow(month)], [user])
                            ),
                        ])
                    ),
                    new SelectItemProjection(
                        "rank_in_month",
                        new RankProjection(
                            new OrderedWindow(
                                [new OrderItemRow(revenue, SortDirection.Desc)],
                                [month]
                            )
                        )
                    ),
                    new SelectItemProjection(
                        "two_months",
                        new SumIntegerProjection(
                            revenue,
                            over: new WindowWithoutFrameWithRangeDate(
                                [new OrderItemDateRow(month)],
                                new RangeDate(preceding: new DistanceDateMonths(1L)),
                                [user]
                            )
                        )
                    ),
                ],
                orderBy:
                [
                    new OrderItemProjectionExpression(user),
                    new OrderItemProjectionExpression(month),
                ]
            )
        );

        Assert.Equal(
            ["40", "40", "30", "20", "50", "20", "60"],
            ResultColumn.Texts(projection, "revenue")
        );
        Assert.Equal(
            ["40", "80", "110", "20", "70", "90", "60"],
            ResultColumn.Texts(projection, "running")
        );
        Assert.Equal(
            ["", "0", "-10", "", "30", "-30", ""],
            ResultColumn.Texts(projection, "change")
        );
        Assert.Equal(
            ["1", "2", "2", "2", "1", "3", "1"],
            ResultColumn.Texts(projection, "rank_in_month")
        );
        Assert.Equal(
            ["40", "80", "70", "20", "70", "70", "60"],
            ResultColumn.Texts(projection, "two_months")
        );
    }

    [Fact]
    public void WindowFunctionsAreComputedBeforeDistinctBy()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                select:
                [
                    new SelectItemProjection("user", User),
                    new SelectItemProjection("id", Id),
                    new SelectItemProjection("position", Position),
                    new SelectItemProjection(
                        "orders",
                        new CountProjection(over: PerUser)
                    ),
                ],
                distinctBy: ["user"],
                orderBy:
                [
                    new OrderItemProjectionColumn("user"),
                    new OrderItemProjectionColumn("position", SortDirection.Desc),
                ]
            )
        );

        Assert.Equal(["a", "b", "c"], ResultColumn.Texts(projection, "user"));
        Assert.Equal(["2", "6", "8"], ResultColumn.Texts(projection, "id"));
        Assert.Equal(["4", "3", "1"], ResultColumn.Texts(projection, "position"));
        Assert.Equal(["4", "3", "1"], ResultColumn.Texts(projection, "orders"));
    }

    [Fact]
    public void WindowFunctionsAreComputedBeforePagination()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithPagination(
                from: new FromEntity("t.orders"),
                pagination: new Pagination(skip: 2L, take: 3L),
                select:
                [
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
                    new SelectItemProjection(
                        "number",
                        new RowNumberProjection(
                            new OrderedWindow([new OrderItemRow(Id)])
                        )
                    ),
                    new SelectItemProjection(
                        "orders",
                        new CountProjection(over: PerUser)
                    ),
                    new SelectItemProjection(
                        "after",
                        new NextIntegerNullableProjection(
                            Id,
                            new OrderedWindow([new OrderItemRow(Id)]),
                            offset: new LiteralInteger(2)
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        Assert.Equal(["60", "100", "150"], ResultColumn.Texts(projection, "running"));
        Assert.Equal(["3", "4", "5"], ResultColumn.Texts(projection, "number"));
        Assert.Equal(["3", "4", "3"], ResultColumn.Texts(projection, "orders"));
        Assert.Equal(["5", "6", "7"], ResultColumn.Texts(projection, "after"));
    }

    [Fact]
    public void PageIsCutInTheOrderOfAWindowFunction()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithPagination(
                from: new FromEntity("t.orders"),
                pagination: new Pagination(take: 3L),
                select:
                [
                    new SelectItemProjection("id", Id),
                    new SelectItemProjection(
                        "position",
                        new RowNumberProjection(
                            new OrderedWindow([
                                new OrderItemRow(Total, SortDirection.Desc),
                                new OrderItemRow(Id),
                            ])
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionColumn("position")]
            )
        );

        Assert.Equal(["8", "5", "4"], ResultColumn.Texts(projection, "id"));
        Assert.Equal(["1", "2", "3"], ResultColumn.Texts(projection, "position"));
    }

    private static MainPlainQueryWithoutPagination TopPerUser(DecimalNullableRow top)
    {
        return new MainPlainQueryWithoutPagination(
            subqueries:
            [
                new SubqueryQuery(
                    "ranked",
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromEntity("t.orders"),
                        select:
                        [
                            new SelectItemProjection("id", Id),
                            new SelectItemProjection("user", User),
                            new SelectItemProjection("position", Position),
                            new SelectItemProjection(
                                "user_orders",
                                new CountProjection(over: PerUser)
                            ),
                        ]
                    )
                ),
            ],
            from: new FromSubquery("ranked"),
            where: new LessThanOrEqualDecimalRow(
                new FieldInteger("ranked", "position"),
                top
            ),
            select:
            [
                new SelectItemProjection("id", new FieldInteger("ranked", "id")),
                new SelectItemProjection(
                    "position",
                    new FieldInteger("ranked", "position")
                ),
                new SelectItemProjection(
                    "user_orders",
                    new FieldInteger("ranked", "user_orders")
                ),
            ],
            orderBy:
            [
                new OrderItemProjectionExpression(new FieldString("ranked", "user")),
                new OrderItemProjectionColumn("position"),
            ]
        );
    }

    private static IReadOnlyList<string> Values(
        ExprProjection expression,
        BooleanRow? where = null
    )
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                where: where,
                select: [new SelectItemProjection("v", expression)],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        return ResultColumn.Texts(projection, "v");
    }
}
