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
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Recursive subqueries: level 0 is the result of seed and level n + 1 the
// result of next run with the subquery's name standing for the rows of
// level n. The result holds the rows of every level up to the first empty
// one, duplicates included; a row beyond maxDepth fails the whole query. The
// columns are those of seed, and next gives values its columns accept. With
// groupBy, next gives one row for each group of the rows it reads, which
// merges the rows of a level or iterates a numerical method.
public sealed class RecursiveSubqueryTests
{
    // ann refers bob and cid, bob refers dan, dan refers eve; fay refers
    // gus.
    private static readonly IStoredSchemaDataSet Users = new InlineTable(
        "t",
        "users",
        ("id", new LongColumnType()),
        ("name", new StringColumnType()),
        ("referrer", new LongColumnType())
    ).With(
        ["1", "ann", ""],
        ["2", "bob", "1"],
        ["3", "cid", "1"],
        ["4", "dan", "2"],
        ["5", "eve", "4"],
        ["6", "fay", ""],
        ["7", "gus", "6"]
    );

    // 4 is reached from 1 along two paths, through 2 and through 3.
    private static readonly IStoredSchemaDataSet Edges = new InlineTable(
        "t",
        "edges",
        ("source", new LongColumnType()),
        ("target", new LongColumnType())
    ).With(["1", "2"], ["1", "3"], ["2", "4"], ["3", "4"], ["4", "5"]);

    // A cycle: 1, 2, 3 and 1 again.
    private static readonly IStoredSchemaDataSet Ring = new InlineTable(
        "t",
        "ring",
        ("source", new LongColumnType()),
        ("target", new LongColumnType())
    ).With(["1", "2"], ["2", "3"], ["3", "1"]);

    // The parts of two sum to 2 and those of nine to 9.
    private static readonly IStoredSchemaDataSet Parts = new InlineTable(
        "t",
        "parts",
        ("k", new StringColumnType()),
        ("v", new DoubleColumnType())
    ).With(["two", "1.5"], ["two", "0.5"], ["nine", "4"], ["nine", "5"]);

    private static readonly IStoredSchemaDataSet Sales = new InlineTable(
        "t",
        "sales",
        ("day", new DateColumnType()),
        ("amount", new DoubleColumnType())
    ).With(["2024-02-28", "10"], ["2024-03-01", "2.5"], ["2024-02-28", "5"]);

    private static readonly IStoredSchemaDataSet Quiet = new InlineTable(
        "t",
        "quiet",
        ("day", new DateColumnType()),
        ("amount", new DoubleColumnType())
    ).With();

    private static readonly IStoredSchemaDataSet[] Tables =
    [
        Users,
        Edges,
        Ring,
        Parts,
        Sales,
        Quiet,
    ];

    [Fact]
    public void DescendantsAreFollowedToAnyDepthWithADepthColumn()
    {
        // The example of the specification: everyone the user referred,
        // directly or through others.
        AssertRows(
            new PureQLProjection(
                Tables,
                ReadingTree(
                    Tree(
                        new EqualDecimalRow(
                            new FieldInteger("t.users", "id"),
                            new ParamInteger("user_id")
                        ),
                        50L
                    )
                ),
                new Dictionary<string, IParameter>
                {
                    ["user_id"] = new TextParameter("integer", "1"),
                }
            ),
            "1|0",
            "2|1",
            "3|1",
            "4|2",
            "5|3"
        );
    }

    [Fact]
    public void DescendantsOfAUserInTheMiddleStartThere()
    {
        AssertRows(ReadingTree(Tree(IsUser(2), 50L)), "2|0", "4|1", "5|2");
    }

    [Fact]
    public void ASeedRowThatNextFollowsNowhereIsTheWholeResult()
    {
        AssertRows(ReadingTree(Tree(IsUser(3), 50L)), "3|0");
    }

    [Fact]
    public void ASeedWithoutRowsGivesNoRows()
    {
        AssertRows(ReadingTree(Tree(IsUser(99), 50L)));
    }

    [Fact]
    public void EverySeedRowIsFollowed()
    {
        // Both roots: the users without a referrer.
        AssertRows(
            ReadingTree(
                Tree(
                    new EqualDecimalRow(
                        new FieldIntegerNullable("t.users", "referrer"),
                        new LiteralIntegerNullable()
                    ),
                    50L
                )
            ),
            "1|0",
            "6|0",
            "2|1",
            "3|1",
            "7|1",
            "4|2",
            "5|3"
        );
    }

    [Fact]
    public void AncestorsAreFollowedUpwards()
    {
        // Sample 121: the chain of referrers above a user.
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRecursive(
                        "chain",
                        new Recursion(
                            seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.users"),
                                where: IsUser(5),
                                select:
                                [
                                    new SelectItemProjection(
                                        "id",
                                        new FieldInteger("t.users", "id")
                                    ),
                                    new SelectItemProjection(
                                        "referrer",
                                        new FieldIntegerNullable("t.users", "referrer")
                                    ),
                                    new SelectItemProjection(
                                        "depth",
                                        new LiteralInteger(0)
                                    ),
                                ]
                            ),
                            next: new PlainNextQuery(
                                from: new FromSubquery("chain"),
                                joins:
                                [
                                    new JoinEntity(
                                        JoinKind.Inner,
                                        "t.users",
                                        new EqualDecimalRow(
                                            new FieldInteger("t.users", "id"),
                                            new FieldIntegerNullable("chain", "referrer")
                                        )
                                    ),
                                ],
                                select:
                                [
                                    new SelectItemRow(
                                        "id",
                                        new FieldInteger("t.users", "id")
                                    ),
                                    new SelectItemRow(
                                        "referrer",
                                        new FieldIntegerNullable("t.users", "referrer")
                                    ),
                                    new SelectItemRow(
                                        "depth",
                                        new AddIntegerRow([
                                            new FieldInteger("chain", "depth"),
                                            new LiteralInteger(1),
                                        ])
                                    ),
                                ]
                            ),
                            maxDepth: 50L
                        )
                    ),
                ],
                from: new FromSubquery("chain"),
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("chain", "id")),
                    new SelectItemProjection(
                        "referrer",
                        new FieldIntegerNullable("chain", "referrer")
                    ),
                    new SelectItemProjection("depth", new FieldInteger("chain", "depth")),
                ],
                orderBy: [new OrderItemProjectionColumn("depth")]
            ),
            "5|4|0",
            "4|2|1",
            "2|1|2",
            "1||3"
        );
    }

    [Fact]
    public void APathColumnSortsATreeInReadingOrder()
    {
        // Sample 122: next extends the path with concat.
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRecursive(
                        "tree",
                        new Recursion(
                            seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.users"),
                                where: new EqualDecimalRow(
                                    new FieldIntegerNullable("t.users", "referrer"),
                                    new LiteralIntegerNullable()
                                ),
                                select:
                                [
                                    new SelectItemProjection(
                                        "id",
                                        new FieldInteger("t.users", "id")
                                    ),
                                    new SelectItemProjection(
                                        "path",
                                        new FieldString("t.users", "name")
                                    ),
                                ]
                            ),
                            next: new PlainNextQuery(
                                from: new FromEntity("t.users"),
                                joins:
                                [
                                    new JoinSubquery(
                                        JoinKind.Inner,
                                        "tree",
                                        new EqualDecimalRow(
                                            new FieldIntegerNullable(
                                                "t.users",
                                                "referrer"
                                            ),
                                            new FieldInteger("tree", "id")
                                        )
                                    ),
                                ],
                                select:
                                [
                                    new SelectItemRow(
                                        "id",
                                        new FieldInteger("t.users", "id")
                                    ),
                                    new SelectItemRow(
                                        "path",
                                        new ConcatStringRow([
                                            new FieldString("tree", "path"),
                                            new LiteralString("/"),
                                            new FieldString("t.users", "name"),
                                        ])
                                    ),
                                ]
                            ),
                            maxDepth: 50L
                        )
                    ),
                ],
                from: new FromSubquery("tree"),
                select:
                [
                    new SelectItemProjection("path", new FieldString("tree", "path")),
                ],
                orderBy: [new OrderItemProjectionColumn("path")]
            ),
            "ann",
            "ann/bob",
            "ann/bob/dan",
            "ann/bob/dan/eve",
            "ann/cid",
            "fay",
            "fay/gus"
        );
    }

    [Fact]
    public void ARowReachedAlongTwoPathsIsReturnedTwice()
    {
        // 4 is under 2 and under 3, so it is a row of level 2 twice, and
        // next sees each of them once: 5 is a row of level 3 twice.
        AssertRows(
            ReadingReach(Reach("t.edges", 50L)),
            "1|0",
            "2|1",
            "3|1",
            "4|2",
            "4|2",
            "5|3",
            "5|3"
        );
    }

    [Theory]
    [InlineData(1, "1|0", "2|1", "3|1")]
    [InlineData(2, "1|0", "2|1", "3|1", "4|2", "4|2")]
    public void AWhereOnTheDepthStopsAtALevel(long levels, params string[] expected)
    {
        AssertRows(
            ReadingReach(
                Reach(
                    "t.edges",
                    50L,
                    new LessThanDecimalRow(
                        new FieldInteger("reach", "depth"),
                        new LiteralInteger(levels)
                    )
                )
            ),
            expected
        );
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(50)]
    [InlineData(long.MaxValue)]
    public void RowsAtMaxDepthAreReturned(long maxDepth)
    {
        // dan's eve is three levels below ann.
        AssertRows(
            ReadingTree(Tree(IsUser(1), maxDepth)),
            "1|0",
            "2|1",
            "3|1",
            "4|2",
            "5|3"
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    public void ARowBeyondMaxDepthFailsTheQuery(long maxDepth)
    {
        PureQLProjection projection = new(
            Tables,
            ReadingTree(Tree(IsUser(1), maxDepth))
        );

        _ = Assert.Throws<InvalidOperationException>(() =>
            projection.AsEnumerable().ToList()
        );
    }

    [Fact]
    public void AQueryThatFailsReturnsNoRow()
    {
        // The seed's row exists, but an error fails the whole query.
        PureQLProjection projection = new(Tables, ReadingTree(Tree(IsUser(1), 2L)));

        _ = Assert.Throws<InvalidOperationException>(() =>
            projection.AsEnumerable().First()
        );
    }

    [Fact]
    public void AWhereThatStopsAtMaxDepthDoesNotFail()
    {
        AssertRows(
            ReadingTree(
                Tree(
                    IsUser(1),
                    1L,
                    new LessThanDecimalRow(
                        new FieldInteger("tree", "depth"),
                        new LiteralInteger(1)
                    )
                )
            ),
            "1|0",
            "2|1",
            "3|1"
        );
    }

    [Theory]
    [InlineData("3")]
    [InlineData("9223372036854775807")]
    public void MaxDepthIsAParameter(string maxDepth)
    {
        AssertRows(
            new PureQLProjection(
                Tables,
                ReadingTree(Tree(IsUser(1), new ParamInteger("deepest"))),
                new Dictionary<string, IParameter>
                {
                    ["deepest"] = new TextParameter("integer", maxDepth),
                }
            ),
            "1|0",
            "2|1",
            "3|1",
            "4|2",
            "5|3"
        );
    }

    [Theory]
    [InlineData("1")]
    [InlineData("2")]
    public void ARowBeyondAMaxDepthParameterFailsTheQuery(string maxDepth)
    {
        PureQLProjection projection = new(
            Tables,
            ReadingTree(Tree(IsUser(1), new ParamInteger("deepest"))),
            new Dictionary<string, IParameter>
            {
                ["deepest"] = new TextParameter("integer", maxDepth),
            }
        );

        _ = Assert.Throws<InvalidOperationException>(() =>
            projection.AsEnumerable().ToList()
        );
    }

    [Theory]
    [InlineData(1)]
    [InlineData(10)]
    [InlineData(1000)]
    public void DataWithACycleFailsTheQuery(long maxDepth)
    {
        PureQLProjection projection = new(
            Tables,
            ReadingReach(Reach("t.ring", maxDepth))
        );

        _ = Assert.Throws<InvalidOperationException>(() =>
            projection.AsEnumerable().ToList()
        );
    }

    [Fact]
    public void ACycleIsWalkedAsFarAsAWhereOnTheDepthAllows()
    {
        AssertRows(
            ReadingReach(
                Reach(
                    "t.ring",
                    4L,
                    new LessThanDecimalRow(
                        new FieldInteger("reach", "depth"),
                        new LiteralInteger(4)
                    )
                )
            ),
            "1|0",
            "2|1",
            "3|2",
            "1|3",
            "2|4"
        );
    }

    [Fact]
    public void ACycleIsLeftWhenNextSkipsTheRowItStartedFrom()
    {
        // The start is carried in a column, and next does not go back to it.
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRecursive(
                        "walk",
                        new Recursion(
                            seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.ring"),
                                select:
                                [
                                    new SelectItemProjection(
                                        "start",
                                        new FieldInteger("t.ring", "source")
                                    ),
                                    new SelectItemProjection(
                                        "id",
                                        new FieldInteger("t.ring", "source")
                                    ),
                                ]
                            ),
                            next: new PlainNextQuery(
                                from: new FromSubquery("walk"),
                                joins:
                                [
                                    new JoinEntity(
                                        JoinKind.Inner,
                                        "t.ring",
                                        new EqualDecimalRow(
                                            new FieldInteger("t.ring", "source"),
                                            new FieldInteger("walk", "id")
                                        )
                                    ),
                                ],
                                where: new NotEqualDecimalRow(
                                    new FieldInteger("t.ring", "target"),
                                    new FieldInteger("walk", "start")
                                ),
                                select:
                                [
                                    new SelectItemRow(
                                        "start",
                                        new FieldInteger("walk", "start")
                                    ),
                                    new SelectItemRow(
                                        "id",
                                        new FieldInteger("t.ring", "target")
                                    ),
                                ]
                            ),
                            maxDepth: 2L
                        )
                    ),
                ],
                from: new FromSubquery("walk"),
                select:
                [
                    new SelectItemProjection("start", new FieldInteger("walk", "start")),
                    new SelectItemProjection("id", new FieldInteger("walk", "id")),
                ],
                orderBy:
                [
                    new OrderItemProjectionColumn("start"),
                    new OrderItemProjectionColumn("id"),
                ]
            ),
            "1|1",
            "1|2",
            "1|3",
            "2|1",
            "2|2",
            "2|3",
            "3|1",
            "3|2",
            "3|3"
        );
    }

    [Theory]
    [InlineData("2024-02-27", "2024-03-02", 4)]
    [InlineData("2024-02-27", "2024-03-02", 366)]
    public void ACalendarOfDaysIsSeededFromInlineRows(string from, string to, long maxDepth)
    {
        // 2024 is a leap year.
        AssertRows(
            Days(from, to, maxDepth),
            "2024-02-27",
            "2024-02-28",
            "2024-02-29",
            "2024-03-01",
            "2024-03-02"
        );
    }

    [Theory]
    [InlineData("2024-12-31", "2024-12-31")]
    [InlineData("2024-12-31", "2024-01-01")]
    public void ACalendarThatEndsOnOrBeforeItsFirstDayIsThatDay(string from, string to)
    {
        AssertRows(Days(from, to, 1L), "2024-12-31");
    }

    [Fact]
    public void ACalendarLongerThanMaxDepthFailsTheQuery()
    {
        PureQLProjection projection = Days("2024-02-27", "2024-03-02", 3L);

        _ = Assert.Throws<InvalidOperationException>(() =>
            projection.AsEnumerable().ToList()
        );
    }

    [Fact]
    public void ACalendarGivesARowToEveryDayWithoutData()
    {
        // Sample 124: the days of the range, each with its sales.
        AssertRows(
            new PureQLProjection(
                Tables,
                new MainPlainQueryWithoutPagination(
                    subqueries:
                    [
                        .. Calendar(366L),
                        new SubqueryQuery(
                            "daily",
                            new GroupedQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.sales"),
                                groupBy:
                                [
                                    new GroupKey("day", new FieldDate("t.sales", "day")),
                                ],
                                select:
                                [
                                    new SelectItemGroup("day", new KeyDate("day")),
                                    new SelectItemGroup("sales", new CountGroup()),
                                    new SelectItemGroup(
                                        "revenue",
                                        new SumDecimalGroup(
                                            new FieldDecimal("t.sales", "amount")
                                        )
                                    ),
                                ]
                            )
                        ),
                    ],
                    from: new FromSubquery("days"),
                    joins:
                    [
                        new JoinSubquery(
                            JoinKind.Left,
                            "daily",
                            new EqualDateRow(
                                new FieldDate("days", "day"),
                                new FieldDate("daily", "day")
                            )
                        ),
                    ],
                    select:
                    [
                        new SelectItemProjection("day", new FieldDate("days", "day")),
                        new SelectItemProjection(
                            "sales",
                            new CoalesceIntegerProjection([
                                new FieldIntegerNullable("daily", "sales"),
                                new LiteralInteger(0),
                            ])
                        ),
                        new SelectItemProjection(
                            "revenue",
                            new CoalesceDecimalProjection([
                                new FieldDecimalNullable("daily", "revenue"),
                                new LiteralInteger(0),
                            ])
                        ),
                    ],
                    orderBy:
                    [
                        new OrderItemProjectionExpression(new FieldDate("days", "day")),
                    ]
                ),
                Range("2024-02-27", "2024-03-02")
            ),
            "2024-02-27|0|0",
            "2024-02-28|2|15",
            "2024-02-29|0|0",
            "2024-03-01|1|2.5",
            "2024-03-02|0|0"
        );
    }

    [Fact]
    public void ACalendarIsSeededFromATotalQuery()
    {
        // From the first day of sales to the first of March.
        AssertRows(DaysFromTheFirst("t.sales"), "2024-02-28", "2024-02-29", "2024-03-01");
    }

    [Fact]
    public void ATotalSeedGivesItsOneRowEvenOverNoRows()
    {
        // The first day of no sales is null, and null is before no date.
        AssertRows(DaysFromTheFirst("t.quiet"), "");
    }

    [Fact]
    public void ASeedIsACombinedQuery()
    {
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRecursive(
                        "tree",
                        new Recursion(
                            seed: new CombinedQueryUnionAll([Root(1), Root(6), Root(6)]),
                            next: Children(),
                            maxDepth: 50L
                        )
                    ),
                ],
                from: new FromSubquery("tree"),
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("tree", "id")),
                    new SelectItemProjection("depth", new FieldInteger("tree", "depth")),
                ],
                orderBy:
                [
                    new OrderItemProjectionColumn("depth"),
                    new OrderItemProjectionColumn("id"),
                ]
            ),
            "1|0",
            "6|0",
            "6|0",
            "2|1",
            "3|1",
            "7|1",
            "7|1",
            "4|2",
            "5|3"
        );
    }

    [Fact]
    public void NextReadsASubqueryDeclaredBeforeIt()
    {
        // The links are inline rows.
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRows(
                        "links",
                        [
                            [
                                new RowItem("source", new LiteralInteger(1)),
                                new RowItem("target", new LiteralInteger(2)),
                            ],
                            [
                                new RowItem("source", new LiteralInteger(2)),
                                new RowItem("target", new LiteralInteger(3)),
                            ],
                            [
                                new RowItem("source", new LiteralInteger(7)),
                                new RowItem("target", new LiteralInteger(8)),
                            ],
                        ]
                    ),
                    Reach("links", 50L),
                ],
                from: new FromSubquery("reach"),
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("reach", "id")),
                    new SelectItemProjection("depth", new FieldInteger("reach", "depth")),
                ],
                orderBy: [new OrderItemProjectionColumn("depth")]
            ),
            "1|0",
            "2|1",
            "3|2"
        );
    }

    [Fact]
    public void TheSubqueryIsReadInNextUnderAnAlias()
    {
        AssertRows(
            ReadingTree(
                new SubqueryRecursive(
                    "tree",
                    new Recursion(
                        seed: Root(1),
                        next: new PlainNextQuery(
                            from: new FromEntity("t.users", "child"),
                            joins:
                            [
                                new JoinSubquery(
                                    JoinKind.Inner,
                                    "tree",
                                    new EqualDecimalRow(
                                        new FieldIntegerNullable("child", "referrer"),
                                        new FieldInteger("parent", "id")
                                    ),
                                    "parent"
                                ),
                            ],
                            select:
                            [
                                new SelectItemRow("id", new FieldInteger("child", "id")),
                                new SelectItemRow(
                                    "depth",
                                    new AddIntegerRow([
                                        new FieldInteger("parent", "depth"),
                                        new LiteralInteger(1),
                                    ])
                                ),
                            ]
                        ),
                        maxDepth: 50L
                    )
                )
            ),
            "1|0",
            "2|1",
            "3|1",
            "4|2",
            "5|3"
        );
    }

    [Fact]
    public void TheColumnsOfNextAreMatchedByAliasWhateverTheirOrder()
    {
        // By position the depth of a level would be an id.
        AssertRows(
            ReadingTree(
                new SubqueryRecursive(
                    "tree",
                    new Recursion(
                        seed: Root(2),
                        next: new PlainNextQuery(
                            from: new FromEntity("t.users"),
                            joins: [JoinTree()],
                            select:
                            [
                                new SelectItemRow(
                                    "depth",
                                    new AddIntegerRow([
                                        new FieldInteger("tree", "depth"),
                                        new LiteralInteger(1),
                                    ])
                                ),
                                new SelectItemRow(
                                    "id",
                                    new FieldInteger("t.users", "id")
                                ),
                            ]
                        ),
                        maxDepth: 50L
                    )
                )
            ),
            "2|0",
            "4|1",
            "5|2"
        );
    }

    [Fact]
    public void NextKeepsEveryRowOfALeftJoinFromTheSubquery()
    {
        // The subquery is not on the optional side: the edges are.
        AssertRows(
            ReadingReach(
                new SubqueryRecursive(
                    "reach",
                    new Recursion(
                        seed: Start(),
                        next: new PlainNextQuery(
                            from: new FromSubquery("reach"),
                            joins:
                            [
                                new JoinEntity(
                                    JoinKind.Left,
                                    "t.edges",
                                    new EqualDecimalRow(
                                        new FieldInteger("t.edges", "source"),
                                        new FieldInteger("reach", "id")
                                    )
                                ),
                            ],
                            where: new NotEqualDecimalRow(
                                new FieldIntegerNullable("t.edges", "target"),
                                new LiteralIntegerNullable()
                            ),
                            select: OptionalStep()
                        ),
                        maxDepth: 3L
                    )
                )
            ),
            "1|0",
            "2|1",
            "3|1",
            "4|2",
            "4|2",
            "5|3",
            "5|3"
        );
    }

    [Fact]
    public void NextReadsTheSubqueryAsTheJoinedSourceOfARightJoin()
    {
        // The optional side of a right join is the sources before it.
        AssertRows(
            ReadingReach(
                new SubqueryRecursive(
                    "reach",
                    new Recursion(
                        seed: Start(),
                        next: new PlainNextQuery(
                            from: new FromEntity("t.edges"),
                            joins:
                            [
                                new JoinSubquery(
                                    JoinKind.Right,
                                    "reach",
                                    new EqualDecimalRow(
                                        new FieldInteger("t.edges", "source"),
                                        new FieldInteger("reach", "id")
                                    )
                                ),
                            ],
                            where: new NotEqualDecimalRow(
                                new FieldIntegerNullable("t.edges", "target"),
                                new LiteralIntegerNullable()
                            ),
                            select: OptionalStep()
                        ),
                        maxDepth: 3L
                    )
                )
            ),
            "1|0",
            "2|1",
            "3|1",
            "4|2",
            "4|2",
            "5|3",
            "5|3"
        );
    }

    [Fact]
    public void TheSeedDeclaresADecimalColumnWithAsDecimal()
    {
        // total is the id of the root plus a half for every level.
        AssertRows(
            ReadingColumn(
                new PlainQueryWithoutPaginationWithoutOrderBy(
                    from: new FromEntity("t.users"),
                    where: IsUser(1),
                    select:
                    [
                        new SelectItemProjection("id", new FieldInteger("t.users", "id")),
                        new SelectItemProjection(
                            "x",
                            new AsDecimalIntegerDecimalProjection(
                                new FieldInteger("t.users", "id")
                            )
                        ),
                    ]
                ),
                new AddDecimalRow([
                    new FieldDecimal("tree", "x"),
                    new LiteralDecimal(0.5m),
                ]),
                new FieldDecimal("tree", "x")
            ),
            "1|1",
            "2|1.5",
            "3|1.5",
            "4|2",
            "5|2.5"
        );
    }

    [Fact]
    public void NextGivesAnIntegerForADecimalColumn()
    {
        AssertRows(
            ReadingColumn(
                SeedWith(new LiteralDecimal(0.5m)),
                new FieldInteger("t.users", "id"),
                new FieldDecimal("tree", "x")
            ),
            "1|0.5",
            "2|2",
            "3|3",
            "4|4",
            "5|5"
        );
    }

    [Fact]
    public void TheSeedDeclaresANullableColumnWithAsNullable()
    {
        AssertRows(
            ReadingColumn(
                SeedWith(
                    new AsNullableIntegerNullableProjection(
                        new FieldInteger("t.users", "id")
                    )
                ),
                new FieldIntegerNullable("t.users", "referrer"),
                new FieldIntegerNullable("tree", "x")
            ),
            "1|1",
            "2|1",
            "3|1",
            "4|2",
            "5|4"
        );
    }

    [Fact]
    public void TheSeedDeclaresANullableColumnWithATypedNull()
    {
        AssertRows(
            ReadingColumn(
                SeedWith(new LiteralIntegerNullable()),
                new FieldIntegerNullable("t.users", "referrer"),
                new FieldIntegerNullable("tree", "x")
            ),
            "1|",
            "2|1",
            "3|1",
            "4|2",
            "5|4"
        );
    }

    [Fact]
    public void NextGivesANonNullValueForANullableColumn()
    {
        AssertRows(
            ReadingColumn(
                SeedWith(new LiteralIntegerNullable()),
                new FieldInteger("tree", "id"),
                new FieldIntegerNullable("tree", "x")
            ),
            "1|",
            "2|1",
            "3|1",
            "4|2",
            "5|4"
        );
    }

    [Fact]
    public void NextGivesANonNullIntegerForANullableDecimalColumn()
    {
        AssertRows(
            ReadingColumn(
                SeedWith(new LiteralDecimalNullable()),
                new FieldInteger("tree", "id"),
                new FieldDecimalNullable("tree", "x")
            ),
            "1|",
            "2|1",
            "3|1",
            "4|2",
            "5|4"
        );
    }

    [Fact]
    public void TheSubqueryIsReadByALaterSubquery()
    {
        // The number of users on each level under ann.
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    Tree(IsUser(1), 50L),
                    new SubqueryQuery(
                        "levels",
                        new GroupedQueryWithoutPaginationWithoutOrderBy(
                            from: new FromSubquery("tree"),
                            groupBy:
                            [
                                new GroupKey("depth", new FieldInteger("tree", "depth")),
                            ],
                            select:
                            [
                                new SelectItemGroup("depth", new KeyInteger("depth")),
                                new SelectItemGroup("users", new CountGroup()),
                            ]
                        )
                    ),
                ],
                from: new FromSubquery("levels"),
                select:
                [
                    new SelectItemProjection(
                        "depth",
                        new FieldInteger("levels", "depth")
                    ),
                    new SelectItemProjection(
                        "users",
                        new FieldInteger("levels", "users")
                    ),
                ],
                orderBy: [new OrderItemProjectionColumn("depth")]
            ),
            "0|1",
            "1|2",
            "2|1",
            "3|1"
        );
    }

    [Fact]
    public void TheSubqueryIsJoinedTwice()
    {
        // Each user under ann with every user one level deeper.
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries: [Tree(IsUser(1), 50L)],
                from: new FromSubquery("tree", "upper"),
                joins:
                [
                    new JoinSubquery(
                        JoinKind.Inner,
                        "tree",
                        new EqualDecimalRow(
                            new FieldInteger("lower", "depth"),
                            new AddIntegerRow([
                                new FieldInteger("upper", "depth"),
                                new LiteralInteger(1),
                            ])
                        ),
                        "lower"
                    ),
                ],
                select:
                [
                    new SelectItemProjection("upper", new FieldInteger("upper", "id")),
                    new SelectItemProjection("lower", new FieldInteger("lower", "id")),
                ],
                orderBy:
                [
                    new OrderItemProjectionColumn("upper"),
                    new OrderItemProjectionColumn("lower"),
                ]
            ),
            "1|2",
            "1|3",
            "2|4",
            "3|4",
            "4|5"
        );
    }

    [Theory]
    [InlineData(true, "bob", "dan", "eve")]
    [InlineData(false, "ann", "cid", "fay", "gus")]
    public void TheSubqueryIsTheListOfIn(bool under, params string[] expected)
    {
        // The users under bob, bob included, and everyone else.
        BooleanRow isUnder = new InDecimalRow(
            new FieldInteger("t.users", "id"),
            new ListSubqueryColumnInteger("tree", "id")
        );

        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries: [Tree(IsUser(2), 50L)],
                from: new FromEntity("t.users"),
                where: under ? isUnder : new NotRow(isUnder),
                select:
                [
                    new SelectItemProjection("name", new FieldString("t.users", "name")),
                ],
                orderBy: [new OrderItemProjectionColumn("name")]
            ),
            expected
        );
    }

    [Fact]
    public void TheSubqueryIsSortedAndPagedByTheQueryThatReadsIt()
    {
        AssertRows(
            new MainPlainQueryWithPagination(
                subqueries: [Tree(IsUser(1), 50L)],
                from: new FromSubquery("tree"),
                select: [new SelectItemProjection("id", new FieldInteger("tree", "id"))],
                orderBy:
                [
                    new OrderItemProjectionExpression(
                        new FieldInteger("tree", "depth"),
                        SortDirection.Desc
                    ),
                    new OrderItemProjectionColumn("id"),
                ],
                pagination: new Pagination(skip: 1L, take: 3L)
            ),
            "4",
            "2",
            "3"
        );
    }

    [Fact]
    public void ARecursiveSubqueryIsSeededFromAnEarlierOne()
    {
        // The first level under ann, then everyone below it.
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    Tree(IsUser(1), 50L),
                    new SubqueryRecursive(
                        "below",
                        new Recursion(
                            seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromSubquery("tree"),
                                where: new EqualDecimalRow(
                                    new FieldInteger("tree", "depth"),
                                    new LiteralInteger(1)
                                ),
                                select:
                                [
                                    new SelectItemProjection(
                                        "id",
                                        new FieldInteger("tree", "id")
                                    ),
                                ]
                            ),
                            next: new PlainNextQuery(
                                from: new FromEntity("t.users"),
                                joins:
                                [
                                    new JoinSubquery(
                                        JoinKind.Inner,
                                        "below",
                                        new EqualDecimalRow(
                                            new FieldIntegerNullable(
                                                "t.users",
                                                "referrer"
                                            ),
                                            new FieldInteger("below", "id")
                                        )
                                    ),
                                ],
                                select:
                                [
                                    new SelectItemRow(
                                        "id",
                                        new FieldInteger("t.users", "id")
                                    ),
                                ]
                            ),
                            maxDepth: 2L
                        )
                    ),
                ],
                from: new FromSubquery("below"),
                select: [new SelectItemProjection("id", new FieldInteger("below", "id"))],
                orderBy: [new OrderItemProjectionColumn("id")]
            ),
            "2",
            "3",
            "4",
            "5"
        );
    }

    [Fact]
    public void EveryRowAsItsOwnAncestorIsRolledUpByAGroupedQuery()
    {
        // Sample 123: each user with everyone below, counted.
        AssertRows(
            new MainGroupedQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRecursive(
                        "lineage",
                        new Recursion(
                            seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.users"),
                                select:
                                [
                                    new SelectItemProjection(
                                        "ancestor",
                                        new FieldInteger("t.users", "id")
                                    ),
                                    new SelectItemProjection(
                                        "id",
                                        new FieldInteger("t.users", "id")
                                    ),
                                    new SelectItemProjection(
                                        "distance",
                                        new LiteralInteger(0)
                                    ),
                                ]
                            ),
                            next: new PlainNextQuery(
                                from: new FromEntity("t.users"),
                                joins:
                                [
                                    new JoinSubquery(
                                        JoinKind.Inner,
                                        "lineage",
                                        new EqualDecimalRow(
                                            new FieldIntegerNullable(
                                                "t.users",
                                                "referrer"
                                            ),
                                            new FieldInteger("lineage", "id")
                                        )
                                    ),
                                ],
                                select:
                                [
                                    new SelectItemRow(
                                        "ancestor",
                                        new FieldInteger("lineage", "ancestor")
                                    ),
                                    new SelectItemRow(
                                        "id",
                                        new FieldInteger("t.users", "id")
                                    ),
                                    new SelectItemRow(
                                        "distance",
                                        new AddIntegerRow([
                                            new FieldInteger("lineage", "distance"),
                                            new LiteralInteger(1),
                                        ])
                                    ),
                                ]
                            ),
                            maxDepth: 50L
                        )
                    ),
                ],
                from: new FromSubquery("lineage"),
                groupBy:
                [
                    new GroupKey("ancestor", new FieldInteger("lineage", "ancestor")),
                ],
                select:
                [
                    new SelectItemGroup("ancestor", new KeyInteger("ancestor")),
                    new SelectItemGroup("network", new CountGroup()),
                    new SelectItemGroup(
                        "deepest",
                        new MaxIntegerGroup(new FieldInteger("lineage", "distance"))
                    ),
                ],
                orderBy: [new OrderItemGroupColumn("ancestor")]
            ),
            "1|5|3",
            "2|3|2",
            "3|1|0",
            "4|2|1",
            "5|1|0",
            "6|2|1",
            "7|1|0"
        );
    }

    [Fact]
    public void AGroupedNextMergesTheRowsOfALevel()
    {
        // 4 is reached along two paths and is one row with both; next then
        // reads that one row, so 5 is one row as well.
        AssertRows(
            ReadingMerged(Merged(), "paths"),
            "1|0|1",
            "2|1|1",
            "3|1|1",
            "4|2|2",
            "5|3|2"
        );
    }

    [Fact]
    public void OverAllInAGroupedNextIsEveryRowOfTheStep()
    {
        // The steps read two, two and one joined rows.
        AssertRows(
            ReadingMerged(Merged(), "read"),
            "1|0|1",
            "2|1|2",
            "3|1|2",
            "4|2|2",
            "5|3|1"
        );
    }

    [Fact]
    public void AHavingThatDropsEveryGroupStopsAGroupedNext()
    {
        // The only group of the second step has two paths.
        AssertRows(
            ReadingMerged(
                Merged(
                    new LessThanDecimalGroup(
                        new SumIntegerGroup(new FieldInteger("reach", "paths")),
                        new LiteralInteger(2)
                    )
                ),
                "paths"
            ),
            "1|0|1",
            "2|1|1",
            "3|1|1"
        );
    }

    [Fact]
    public void AGroupedNextOverACycleFailsTheQuery()
    {
        PureQLProjection projection = new(
            Tables,
            ReadingMerged(Merged(entity: "t.ring"), "paths")
        );

        _ = Assert.Throws<InvalidOperationException>(() =>
            projection.AsEnumerable().ToList()
        );
    }

    [Fact]
    public void AnIterationStoppedByAToleranceInHavingReturnsEveryLevel()
    {
        // Newton's method for the square root of a = sum(v), from x = 1:
        // x' = (x + a / x) / 2 while |x * x - a| > 0.0000001. The digits are
        // those of Python's decimal module with 28 significant digits and
        // ROUND_HALF_UP, each of a / x, x + a / x, the division by 2 and
        // x * x rounded as a result of its own.
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries: [Roots(having: OutsideTolerance(), maxDepth: 5L)],
                from: new FromSubquery("root"),
                select: State(),
                orderBy:
                [
                    new OrderItemProjectionColumn("k"),
                    new OrderItemProjectionColumn("step"),
                ]
            ),
            "nine|0|1",
            "nine|1|5",
            "nine|2|3.4",
            "nine|3|3.023529411764705882352941177",
            "nine|4|3.000091554131380178530556192",
            "nine|5|3.000000001396983862248478426",
            "two|0|1",
            "two|1|1.5",
            "two|2|1.416666666666666666666666667",
            "two|3|1.414215686274509803921568628",
            "two|4|1.414213562374689910626295579"
        );
    }

    [Fact]
    public void TheFinalStateOfAnIterationIsTheRowOfItsLastStep()
    {
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries: [Roots(having: OutsideTolerance(), maxDepth: 50L)],
                from: new FromSubquery("root"),
                select: State(),
                orderBy:
                [
                    new OrderItemProjectionColumn("k"),
                    new OrderItemProjectionColumn("step", SortDirection.Desc),
                ],
                distinctBy: ["k"]
            ),
            "nine|5|3.000000001396983862248478426",
            "two|4|1.414213562374689910626295579"
        );
    }

    [Fact]
    public void AnIterationIsStoppedByANumberOfStepsInWhere()
    {
        // Eight steps of the same iteration, by which both roots no longer
        // change: 3 and the 28 digits of the square root of 2.
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    Roots(
                        where: new LessThanDecimalRow(
                            new FieldInteger("root", "step"),
                            new LiteralInteger(8)
                        ),
                        maxDepth: 8L
                    ),
                ],
                from: new FromSubquery("root"),
                where: new GreaterThanOrEqualDecimalRow(
                    new FieldInteger("root", "step"),
                    new LiteralInteger(5)
                ),
                select: State(),
                orderBy:
                [
                    new OrderItemProjectionColumn("k"),
                    new OrderItemProjectionColumn("step"),
                ]
            ),
            "nine|5|3.000000001396983862248478426",
            "nine|6|3.000000000000000000325260652",
            "nine|7|3",
            "nine|8|3",
            "two|5|1.414213562373095048801689624",
            "two|6|1.414213562373095048801688724",
            "two|7|1.414213562373095048801688724",
            "two|8|1.414213562373095048801688724"
        );
    }

    [Fact]
    public void AnIterationThatHasNotStoppedByMaxDepthFailsTheQuery()
    {
        // nine needs five steps.
        PureQLProjection projection = new(
            Tables,
            new MainPlainQueryWithoutPagination(
                subqueries: [Roots(having: OutsideTolerance(), maxDepth: 4L)],
                from: new FromSubquery("root"),
                select: State()
            )
        );

        _ = Assert.Throws<InvalidOperationException>(() =>
            projection.AsEnumerable().ToList()
        );
    }

    [Fact]
    public void AnIterationThatNeverStopsFailsTheQuery()
    {
        PureQLProjection projection = new(
            Tables,
            new MainPlainQueryWithoutPagination(
                subqueries: [Roots(maxDepth: 30L)],
                from: new FromSubquery("root"),
                select: State()
            )
        );

        _ = Assert.Throws<InvalidOperationException>(() =>
            projection.AsEnumerable().ToList()
        );
    }

    private static BooleanRow IsUser(long id)
    {
        return new EqualDecimalRow(
            new FieldInteger("t.users", "id"),
            new LiteralInteger(id)
        );
    }

    // The user of an id, at depth 0.
    private static Query Root(long id)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.users"),
            where: IsUser(id),
            select:
            [
                new SelectItemProjection("id", new FieldInteger("t.users", "id")),
                new SelectItemProjection("depth", new LiteralInteger(0)),
            ]
        );
    }

    private static Join JoinTree()
    {
        return new JoinSubquery(
            JoinKind.Inner,
            "tree",
            new EqualDecimalRow(
                new FieldIntegerNullable("t.users", "referrer"),
                new FieldInteger("tree", "id")
            )
        );
    }

    // The users the rows of tree referred, one level deeper.
    private static NextQuery Children(BooleanRow? where = null)
    {
        return new PlainNextQuery(
            from: new FromEntity("t.users"),
            joins: [JoinTree()],
            where: where,
            select:
            [
                new SelectItemRow("id", new FieldInteger("t.users", "id")),
                new SelectItemRow(
                    "depth",
                    new AddIntegerRow([
                        new FieldInteger("tree", "depth"),
                        new LiteralInteger(1),
                    ])
                ),
            ]
        );
    }

    // The subquery tree: the users the seed keeps and everyone below them,
    // each with its depth.
    private static Subquery Tree(
        BooleanRow seed,
        RecursionMaxDepth maxDepth,
        BooleanRow? step = null
    )
    {
        return new SubqueryRecursive(
            "tree",
            new Recursion(
                seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                    from: new FromEntity("t.users"),
                    where: seed,
                    select:
                    [
                        new SelectItemProjection("id", new FieldInteger("t.users", "id")),
                        new SelectItemProjection("depth", new LiteralInteger(0)),
                    ]
                ),
                next: Children(step),
                maxDepth: maxDepth
            )
        );
    }

    // The rows of tree, level by level.
    private static PureQLQuery ReadingTree(Subquery tree)
    {
        return new MainPlainQueryWithoutPagination(
            subqueries: [tree],
            from: new FromSubquery("tree"),
            select:
            [
                new SelectItemProjection("id", new FieldInteger("tree", "id")),
                new SelectItemProjection("depth", new FieldInteger("tree", "depth")),
            ],
            orderBy:
            [
                new OrderItemProjectionColumn("depth"),
                new OrderItemProjectionColumn("id"),
            ]
        );
    }

    // The row (1, 0) the walks over the edges start from.
    private static Query Start()
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.users"),
            where: IsUser(1),
            select:
            [
                new SelectItemProjection("id", new FieldInteger("t.users", "id")),
                new SelectItemProjection("depth", new LiteralInteger(0)),
            ]
        );
    }

    // The subquery reach: 1 and everything reached from it along the edges
    // of an entity or a subquery, each with its depth.
    private static Subquery Reach(
        string edges,
        RecursionMaxDepth maxDepth,
        BooleanRow? step = null
    )
    {
        BooleanRow on = new EqualDecimalRow(
            new FieldInteger(edges, "source"),
            new FieldInteger("reach", "id")
        );

        return new SubqueryRecursive(
            "reach",
            new Recursion(
                seed: Start(),
                next: new PlainNextQuery(
                    from: new FromSubquery("reach"),
                    joins:
                    [
                        edges.StartsWith("t.", StringComparison.Ordinal)
                            ? new JoinEntity(JoinKind.Inner, edges, on)
                            : new JoinSubquery(JoinKind.Inner, edges, on),
                    ],
                    where: step,
                    select:
                    [
                        new SelectItemRow("id", new FieldInteger(edges, "target")),
                        new SelectItemRow(
                            "depth",
                            new AddIntegerRow([
                                new FieldInteger("reach", "depth"),
                                new LiteralInteger(1),
                            ])
                        ),
                    ]
                ),
                maxDepth: maxDepth
            )
        );
    }

    // The columns of a step whose edges are on the optional side of an
    // outer join.
    private static SelectItemRow[] OptionalStep()
    {
        return
        [
            new SelectItemRow(
                "id",
                new CoalesceIntegerRow([
                    new FieldIntegerNullable("t.edges", "target"),
                    new LiteralInteger(0),
                ])
            ),
            new SelectItemRow(
                "depth",
                new AddIntegerRow([
                    new FieldInteger("reach", "depth"),
                    new LiteralInteger(1),
                ])
            ),
        ];
    }

    private static PureQLQuery ReadingReach(Subquery reach)
    {
        return new MainPlainQueryWithoutPagination(
            subqueries: [reach],
            from: new FromSubquery("reach"),
            select:
            [
                new SelectItemProjection("id", new FieldInteger("reach", "id")),
                new SelectItemProjection("depth", new FieldInteger("reach", "depth")),
            ],
            orderBy:
            [
                new OrderItemProjectionColumn("depth"),
                new OrderItemProjectionColumn("id"),
            ]
        );
    }

    // The seed (1, x) of ann.
    private static Query SeedWith(ExprProjection x)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.users"),
            where: IsUser(1),
            select:
            [
                new SelectItemProjection("id", new FieldInteger("t.users", "id")),
                new SelectItemProjection("x", x),
            ]
        );
    }

    // The users under the seed, each with the x that next gives it, read
    // through the given field of the subquery.
    private static PureQLQuery ReadingColumn(Query seed, ExprRow x, ExprProjection field)
    {
        return new MainPlainQueryWithoutPagination(
            subqueries:
            [
                new SubqueryRecursive(
                    "tree",
                    new Recursion(
                        seed: seed,
                        next: new PlainNextQuery(
                            from: new FromEntity("t.users"),
                            joins: [JoinTree()],
                            select:
                            [
                                new SelectItemRow(
                                    "id",
                                    new FieldInteger("t.users", "id")
                                ),
                                new SelectItemRow("x", x),
                            ]
                        ),
                        maxDepth: 50L
                    )
                ),
            ],
            from: new FromSubquery("tree"),
            select:
            [
                new SelectItemProjection("id", new FieldInteger("tree", "id")),
                new SelectItemProjection("x", field),
            ],
            orderBy: [new OrderItemProjectionColumn("id")]
        );
    }

    // The first day, a parameter, and every day after it up to another.
    private static Subquery[] Calendar(RecursionMaxDepth maxDepth)
    {
        return
        [
            new SubqueryRows("first_day", [[new RowItem("day", new ParamDate("from"))]]),
            new SubqueryRecursive(
                "days",
                new Recursion(
                    seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromSubquery("first_day"),
                        select:
                        [
                            new SelectItemProjection(
                                "day",
                                new FieldDate("first_day", "day")
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
                    maxDepth: maxDepth
                )
            ),
        ];
    }

    private static Dictionary<string, IParameter> Range(string from, string to)
    {
        return new Dictionary<string, IParameter>
        {
            ["from"] = new TextParameter("date", from),
            ["to"] = new TextParameter("date", to),
        };
    }

    private static PureQLProjection Days(string from, string to, long maxDepth)
    {
        return new PureQLProjection(
            Tables,
            new MainPlainQueryWithoutPagination(
                subqueries: Calendar(maxDepth),
                from: new FromSubquery("days"),
                select: [new SelectItemProjection("day", new FieldDate("days", "day"))],
                orderBy: [new OrderItemProjectionColumn("day")]
            ),
            Range(from, to)
        );
    }

    // The days from the first day of an entity to the first of March 2024;
    // the seed is a total query, so its day is nullable.
    private static PureQLQuery DaysFromTheFirst(string entity)
    {
        return new MainPlainQueryWithoutPagination(
            subqueries:
            [
                new SubqueryRecursive(
                    "days",
                    new Recursion(
                        seed: new TotalQuery(
                            from: new FromEntity(entity),
                            aggregate:
                            [
                                new SelectItemTotal(
                                    "day",
                                    new MinDateNullableTotal(new FieldDate(entity, "day"))
                                ),
                            ]
                        ),
                        next: new PlainNextQuery(
                            from: new FromSubquery("days"),
                            where: new LessThanDateRow(
                                new FieldDateNullable("days", "day"),
                                new LiteralDate(new DateOnly(2024, 3, 1))
                            ),
                            select:
                            [
                                new SelectItemRow(
                                    "day",
                                    new DateAddDaysDateNullableRow(
                                        new FieldDateNullable("days", "day"),
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
            select:
            [
                new SelectItemProjection("day", new FieldDateNullable("days", "day")),
            ],
            orderBy: [new OrderItemProjectionColumn("day")]
        );
    }

    // The subquery reach with a grouped next: one row for each target the
    // rows of a level reach, with the paths that lead to it and the rows the
    // step read.
    private static Subquery Merged(BooleanGroup? having = null, string entity = "t.edges")
    {
        return new SubqueryRecursive(
            "reach",
            new Recursion(
                seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                    from: new FromEntity("t.users"),
                    where: IsUser(1),
                    select:
                    [
                        new SelectItemProjection("id", new FieldInteger("t.users", "id")),
                        new SelectItemProjection("depth", new LiteralInteger(0)),
                        new SelectItemProjection("paths", new LiteralInteger(1)),
                        new SelectItemProjection("read", new LiteralInteger(1)),
                    ]
                ),
                next: new GroupedNextQuery(
                    from: new FromSubquery("reach"),
                    joins:
                    [
                        new JoinEntity(
                            JoinKind.Inner,
                            entity,
                            new EqualDecimalRow(
                                new FieldInteger(entity, "source"),
                                new FieldInteger("reach", "id")
                            )
                        ),
                    ],
                    groupBy:
                    [
                        new GroupKey("id", new FieldInteger(entity, "target")),
                        new GroupKey("depth", new FieldInteger("reach", "depth")),
                    ],
                    having: having,
                    select:
                    [
                        new SelectItemGroup("id", new KeyInteger("id")),
                        new SelectItemGroup(
                            "depth",
                            new AddIntegerGroup([
                                new KeyInteger("depth"),
                                new LiteralInteger(1),
                            ])
                        ),
                        new SelectItemGroup(
                            "paths",
                            new SumIntegerGroup(new FieldInteger("reach", "paths"))
                        ),
                        new SelectItemGroup(
                            "read",
                            new CountGroup(over: AggregateOver.All)
                        ),
                    ]
                ),
                maxDepth: 10L
            )
        );
    }

    private static PureQLQuery ReadingMerged(Subquery reach, string column)
    {
        return new MainPlainQueryWithoutPagination(
            subqueries: [reach],
            from: new FromSubquery("reach"),
            select:
            [
                new SelectItemProjection("id", new FieldInteger("reach", "id")),
                new SelectItemProjection("depth", new FieldInteger("reach", "depth")),
                new SelectItemProjection(column, new FieldInteger("reach", column)),
            ],
            orderBy:
            [
                new OrderItemProjectionColumn("depth"),
                new OrderItemProjectionColumn("id"),
            ]
        );
    }

    // Newton's method for the square root of the sum of each key's parts.
    // The state is (k, step, x), one row for each key; a step joins it with
    // the parts, groups by the state and gives x' = (x + sum(v) / x) / 2.
    private static Subquery Roots(
        RecursionMaxDepth maxDepth,
        BooleanRow? where = null,
        BooleanGroup? having = null
    )
    {
        return new SubqueryRecursive(
            "root",
            new Recursion(
                seed: new GroupedQueryWithoutPaginationWithoutOrderBy(
                    from: new FromEntity("t.parts"),
                    groupBy: [new GroupKey("k", new FieldString("t.parts", "k"))],
                    select:
                    [
                        new SelectItemGroup("k", new KeyString("k")),
                        new SelectItemGroup("step", new LiteralInteger(0)),
                        new SelectItemGroup("x", new LiteralDecimal(1m)),
                    ]
                ),
                next: new GroupedNextQuery(
                    from: new FromSubquery("root"),
                    joins:
                    [
                        new JoinEntity(
                            JoinKind.Inner,
                            "t.parts",
                            new EqualStringRow(
                                new FieldString("t.parts", "k"),
                                new FieldString("root", "k")
                            )
                        ),
                    ],
                    where: where,
                    groupBy:
                    [
                        new GroupKey("k", new FieldString("root", "k")),
                        new GroupKey("step", new FieldInteger("root", "step")),
                        new GroupKey("x", new FieldDecimal("root", "x")),
                    ],
                    having: having,
                    select:
                    [
                        new SelectItemGroup("k", new KeyString("k")),
                        new SelectItemGroup(
                            "step",
                            new AddIntegerGroup([
                                new KeyInteger("step"),
                                new LiteralInteger(1),
                            ])
                        ),
                        new SelectItemGroup(
                            "x",
                            new DivideDecimalGroup([
                                new AddDecimalGroup([
                                    new KeyDecimal("x"),
                                    new DivideDecimalGroup([
                                        new SumDecimalGroup(
                                            new FieldDecimal("t.parts", "v")
                                        ),
                                        new KeyDecimal("x"),
                                    ]),
                                ]),
                                new LiteralInteger(2),
                            ])
                        ),
                    ]
                ),
                maxDepth: maxDepth
            )
        );
    }

    // |x * x - sum(v)| > 0.0000001.
    private static BooleanGroup OutsideTolerance()
    {
        return new GreaterThanDecimalGroup(
            new AbsDecimalGroup(
                new SubtractDecimalGroup([
                    new MultiplyDecimalGroup([new KeyDecimal("x"), new KeyDecimal("x")]),
                    new SumDecimalGroup(new FieldDecimal("t.parts", "v")),
                ])
            ),
            new LiteralDecimal(0.0000001m)
        );
    }

    private static SelectItemProjection[] State()
    {
        return
        [
            new SelectItemProjection("k", new FieldString("root", "k")),
            new SelectItemProjection("step", new FieldInteger("root", "step")),
            new SelectItemProjection("x", new FieldDecimal("root", "x")),
        ];
    }

    private static void AssertRows(PureQLQuery query, params string[] expected)
    {
        AssertRows(new PureQLProjection(Tables, query), expected);
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
