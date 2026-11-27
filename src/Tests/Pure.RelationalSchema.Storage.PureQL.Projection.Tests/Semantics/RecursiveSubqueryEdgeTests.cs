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

// Edge cases of recursive subqueries, by the specification's Subqueries
// (recursive: seed, next over the level before, maxDepth), Semantics
// (Recursive subqueries: level n + 1 is next over level n; if level
// maxDepth + 1 would hold a row, the query fails) and Validation (the
// interpreter rows on next, seed and maxDepth).
public sealed class RecursiveSubqueryEdgeTests
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

    private static readonly IStoredSchemaDataSet[] Tables = [Users, Edges];

    // Validation ------------------------------------------------------------

    [Fact]
    public void NextThatDoesNotReadTheSubqueryIsRejected()
    {
        // next reads the subquery exactly once.
        AssertRejected(
            Recursive(
                Root(1),
                new PlainNextQuery(
                    from: new FromEntity("t.users"),
                    select: Step(new FieldInteger("t.users", "id"), new LiteralInteger(1))
                )
            )
        );
    }

    [Fact]
    public void NextThatReadsTheSubqueryTwiceIsRejected()
    {
        AssertRejected(
            Recursive(
                Root(1),
                new PlainNextQuery(
                    from: new FromSubquery("tree"),
                    joins:
                    [
                        new JoinSubquery(
                            JoinKind.Inner,
                            "tree",
                            new EqualDecimalRow(
                                new FieldInteger("again", "id"),
                                new FieldInteger("tree", "id")
                            ),
                            "again"
                        ),
                    ],
                    where: new LessThanDecimalRow(
                        new FieldInteger("tree", "depth"),
                        new LiteralInteger(1)
                    ),
                    select: Step(
                        new FieldInteger("tree", "id"),
                        new AddIntegerRow([
                            new FieldInteger("tree", "depth"),
                            new LiteralInteger(1),
                        ])
                    )
                )
            )
        );
    }

    [Fact]
    public void ASeedThatReadsTheSubqueryIsRejected()
    {
        AssertRejected(
            Recursive(
                new PlainQueryWithoutPaginationWithoutOrderBy(
                    from: new FromSubquery("tree"),
                    select:
                    [
                        new SelectItemProjection("id", new FieldInteger("tree", "id")),
                        new SelectItemProjection("depth", new LiteralInteger(0)),
                    ]
                ),
                Children()
            )
        );
    }

    [Theory]
    [InlineData(JoinKind.Left)]
    [InlineData(JoinKind.Full)]
    public void NextThatJoinsTheSubqueryOnTheOptionalSideIsRejected(JoinKind kind)
    {
        // The joined source is optional after left and full.
        AssertRejected(
            Recursive(
                Root(1),
                new PlainNextQuery(
                    from: new FromEntity("t.users"),
                    joins:
                    [
                        new JoinSubquery(
                            kind,
                            "tree",
                            new EqualDecimalRow(
                                new FieldIntegerNullable("t.users", "referrer"),
                                new FieldInteger("tree", "id")
                            )
                        ),
                    ],
                    select: Step(
                        kind == JoinKind.Full
                            ? new CoalesceIntegerRow([
                                new FieldIntegerNullable("t.users", "id"),
                                new LiteralInteger(0),
                            ])
                            : new FieldInteger("t.users", "id"),
                        new LiteralInteger(1)
                    )
                )
            )
        );
    }

    [Theory]
    [InlineData(JoinKind.Right)]
    [InlineData(JoinKind.Full)]
    public void NextThatReadsTheSubqueryInFromBeforeAnOuterJoinIsRejected(
        JoinKind kind
    )
    {
        // Every earlier source is optional after right and full.
        AssertRejected(
            Recursive(
                Root(1),
                new PlainNextQuery(
                    from: new FromSubquery("tree"),
                    joins:
                    [
                        new JoinEntity(
                            kind,
                            "t.users",
                            new EqualDecimalRow(
                                new FieldIntegerNullable("t.users", "referrer"),
                                new FieldInteger("tree", "id")
                            )
                        ),
                    ],
                    select: Step(
                        kind == JoinKind.Full
                            ? new CoalesceIntegerRow([
                                new FieldIntegerNullable("t.users", "id"),
                                new LiteralInteger(0),
                            ])
                            : new FieldInteger("t.users", "id"),
                        new LiteralInteger(1)
                    )
                )
            )
        );
    }

    [Fact]
    public void NextWithAnotherAliasIsRejected()
    {
        AssertRejected(
            Recursive(
                Root(1),
                Children(
                    [
                        new SelectItemRow("id", new FieldInteger("t.users", "id")),
                        new SelectItemRow("level", DepthPlusOne()),
                    ]
                )
            )
        );
    }

    [Fact]
    public void NextWithAColumnMoreIsRejected()
    {
        AssertRejected(
            Recursive(
                Root(1),
                Children(
                    [
                        new SelectItemRow("id", new FieldInteger("t.users", "id")),
                        new SelectItemRow("depth", DepthPlusOne()),
                        new SelectItemRow("name", new FieldString("t.users", "name")),
                    ]
                )
            )
        );
    }

    [Fact]
    public void NextWithAColumnLessIsRejected()
    {
        AssertRejected(
            Recursive(
                Root(1),
                Children([new SelectItemRow("id", new FieldInteger("t.users", "id"))])
            )
        );
    }

    [Fact]
    public void NextWithAnAliasTwiceIsRejected()
    {
        AssertRejected(
            Recursive(
                Root(1),
                Children(
                    [
                        new SelectItemRow("id", new FieldInteger("t.users", "id")),
                        new SelectItemRow("depth", DepthPlusOne()),
                        new SelectItemRow("depth", DepthPlusOne()),
                    ]
                )
            )
        );
    }

    [Fact]
    public void NextWithADecimalForAnIntegerColumnIsRejected()
    {
        // next gives a type the seed's column accepts: an integer for a
        // decimal column, never the other way round.
        AssertRejected(
            Recursive(
                Root(1),
                Children(
                    [
                        new SelectItemRow("id", new FieldInteger("t.users", "id")),
                        new SelectItemRow(
                            "depth",
                            new AsDecimalIntegerDecimalRow(DepthPlusOne())
                        ),
                    ]
                )
            )
        );
    }

    [Fact]
    public void NextWithANullableValueForANonNullColumnIsRejected()
    {
        AssertRejected(
            Recursive(
                Root(1),
                Children(
                    [
                        new SelectItemRow(
                            "id",
                            new FieldIntegerNullable("t.users", "referrer")
                        ),
                        new SelectItemRow("depth", DepthPlusOne()),
                    ]
                )
            )
        );
    }

    [Fact]
    public void NextWithAStringForAnIntegerColumnIsRejected()
    {
        AssertRejected(
            Recursive(
                Root(1),
                Children(
                    [
                        new SelectItemRow("id", new FieldInteger("t.users", "id")),
                        new SelectItemRow("depth", new FieldString("t.users", "name")),
                    ]
                )
            )
        );
    }

    [Fact]
    public void AReferenceInNextWithAnotherTypeThanTheSeedsIsRejected()
    {
        // Every reference to a column of the subquery, also inside next,
        // declares the seed's type.
        AssertRejected(
            Recursive(
                Root(1),
                Children(
                    [
                        new SelectItemRow("id", new FieldInteger("t.users", "id")),
                        new SelectItemRow(
                            "depth",
                            new AddIntegerNullableRow([
                                new FieldIntegerNullable("tree", "depth"),
                                new LiteralInteger(1),
                            ])
                        ),
                    ]
                )
            )
        );
    }

    [Fact]
    public void AReferenceInNextAsADecimalOfAnIntegerColumnIsRejected()
    {
        AssertRejected(
            Recursive(
                Root(1),
                new PlainNextQuery(
                    from: new FromEntity("t.users"),
                    joins: [JoinTree()],
                    where: new LessThanDecimalRow(
                        new FieldDecimal("tree", "depth"),
                        new LiteralInteger(5)
                    ),
                    select: Step(new FieldInteger("t.users", "id"), DepthPlusOne())
                )
            )
        );
    }

    [Theory]
    [InlineData("0")]
    [InlineData("-1")]
    [InlineData("-9223372036854775808")]
    public void AMaxDepthParameterBelowOneIsRejected(string maxDepth)
    {
        // Validation: a maxDepth parameter is bound to 1 or more.
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                ReadingTree(Recursive(Root(1), Children(), new ParamInteger("deepest"))),
                new Dictionary<string, IParameter>
                {
                    ["deepest"] = new TextParameter("integer", maxDepth),
                }
            )
        );
    }

    [Fact]
    public void AMaxDepthParameterBoundToNullIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                ReadingTree(Recursive(Root(1), Children(), new ParamInteger("deepest"))),
                new Dictionary<string, IParameter>
                {
                    ["deepest"] = new TextParameter(
                        new TextParameterType("integer", true),
                        ""
                    ),
                }
            )
        );
    }

    [Fact]
    public void AMaxDepthParameterOfAnotherTypeIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                ReadingTree(Recursive(Root(1), Children(), new ParamInteger("deepest"))),
                new Dictionary<string, IParameter>
                {
                    ["deepest"] = new TextParameter("decimal", "3.5"),
                }
            )
        );
    }

    [Fact]
    public void NextThatReadsALaterSubqueryIsRejected()
    {
        // A subquery reads only from earlier ones, a recursive one also from
        // itself.
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                new MainPlainQueryWithoutPagination(
                    subqueries:
                    [
                        Recursive(
                            Root(1),
                            new PlainNextQuery(
                                from: new FromSubquery("tree"),
                                joins:
                                [
                                    new JoinSubquery(
                                        JoinKind.Inner,
                                        "later",
                                        new EqualDecimalRow(
                                            new FieldIntegerNullable("later", "referrer"),
                                            new FieldInteger("tree", "id")
                                        )
                                    ),
                                ],
                                select: Step(
                                    new FieldInteger("later", "id"),
                                    new AddIntegerRow([
                                        new FieldInteger("tree", "depth"),
                                        new LiteralInteger(1),
                                    ])
                                )
                            )
                        ),
                        new SubqueryQuery(
                            "later",
                            new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.users"),
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
                                ]
                            )
                        ),
                    ],
                    from: new FromSubquery("tree"),
                    select:
                    [
                        new SelectItemProjection("id", new FieldInteger("tree", "id")),
                    ]
                )
            )
        );
    }

    [Fact]
    public void AnEarlierSubqueryThatReadsTheRecursiveOneIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                new MainPlainQueryWithoutPagination(
                    subqueries:
                    [
                        new SubqueryQuery(
                            "earlier",
                            new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromSubquery("tree"),
                                select:
                                [
                                    new SelectItemProjection(
                                        "id",
                                        new FieldInteger("tree", "id")
                                    ),
                                ]
                            )
                        ),
                        Recursive(Root(1), Children()),
                    ],
                    from: new FromSubquery("earlier"),
                    select:
                    [
                        new SelectItemProjection("id", new FieldInteger("earlier", "id")),
                    ]
                )
            )
        );
    }

    // Semantics -------------------------------------------------------------

    [Fact]
    public void ARecursionThatStopsOnItsOwnAtMaxDepthOneDoesNotFail()
    {
        // dan refers eve, who refers nobody: levels 0 and 1, level 2 empty.
        AssertRows(ReadingTree(Recursive(Root(4), Children(), 1L)), "4|0", "5|1");
    }

    [Fact]
    public void ACounterReachesMaxDepthExactly()
    {
        // 0 to 300: level 300 holds 300 and level 301 is empty.
        AssertRows(Counter(300, 300L), "301|45150|300");
    }

    [Fact]
    public void ACounterOneLevelBeyondMaxDepthFailsTheQuery()
    {
        PureQLProjection projection = new(Tables, Counter(300, 299L));

        _ = Assert.Throws<InvalidOperationException>(() =>
            projection.AsEnumerable().ToList()
        );
    }

    [Fact]
    public void ADecimalIsHalvedUntilAWhereStopsIt()
    {
        // 1, 0.5, 0.25, 0.125 and 0.0625, at depths 0 to 4.
        AssertRows(Halving(4L), "1", "0.5", "0.25", "0.125", "0.0625");
    }

    [Fact]
    public void ADecimalHalvedOneLevelBeyondMaxDepthFailsTheQuery()
    {
        PureQLProjection projection = new(Tables, Halving(3L));

        _ = Assert.Throws<InvalidOperationException>(() =>
            projection.AsEnumerable().ToList()
        );
    }

    [Fact]
    public void EveryRowOfARepeatedSeedRowIsFollowed()
    {
        // The seed holds bob twice; next sees each row of a level once.
        AssertRows(
            ReadingTree(
                Recursive(
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromSubquery("starts"),
                        select:
                        [
                            new SelectItemProjection(
                                "id",
                                new FieldInteger("starts", "id")
                            ),
                            new SelectItemProjection("depth", new LiteralInteger(0)),
                        ]
                    ),
                    Children()
                ),
                new SubqueryRows(
                    "starts",
                    [
                        [new RowItem("id", new LiteralInteger(2))],
                        [new RowItem("id", new LiteralInteger(2))],
                    ]
                )
            ),
            "2|0",
            "2|0",
            "4|1",
            "4|1",
            "5|2",
            "5|2"
        );
    }

    [Fact]
    public void ABranchingStepGivesEveryPathItsOwnRows()
    {
        // Each row x below 3 is followed by x + 1 and x + 2: level 1 is 1, 2,
        // level 2 is 2, 3, 3, 4, level 3 is 3, 4 (from the 2 of level 2).
        AssertRows(Branching(3L), "0", "1", "2", "2", "3", "3", "3", "4", "4");
    }

    [Fact]
    public void ABranchingStepOneLevelBeyondMaxDepthFailsTheQuery()
    {
        PureQLProjection projection = new(Tables, Branching(2L));

        _ = Assert.Throws<InvalidOperationException>(() =>
            projection.AsEnumerable().ToList()
        );
    }

    [Theory]
    [InlineData("union", "1", "2", "3", "4", "5")]
    [InlineData("intersect", "1", "2", "3", "4", "5")]
    [InlineData("except")]
    [InlineData(
        "unionAll",
        "1",
        "1",
        "2",
        "2",
        "3",
        "3",
        "4",
        "4",
        "4",
        "4",
        "5",
        "5",
        "5",
        "5"
    )]
    public void ARecursionIsAnInputOfASetOperation(
        string operation,
        params string[] expected
    )
    {
        // reach holds 1, 2, 3, 4, 4, 5, 5.
        Query input = new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromSubquery("reach"),
            select: [new SelectItemProjection("id", new FieldInteger("reach", "id"))]
        );
        Subquery[] subqueries = [Reach(10L)];
        PureQLQuery query = operation switch
        {
            "union" => new MainCombinedQueryUnion([input, input], subqueries),
            "unionAll" => new MainCombinedQueryUnionAll([input, input], subqueries),
            "intersect" => new MainCombinedQueryIntersect([input, input], subqueries),
            _ => new MainCombinedQueryExcept([input, input], subqueries),
        };

        Assert.Equal(
            expected.AsEnumerable(),
            Texts(new PureQLProjection(Tables, query)).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void TheUsersExceptTheReachedOnesAreTheOthers()
    {
        AssertRows(
            new MainCombinedQueryExcept(
                [
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromEntity("t.users"),
                        select:
                        [
                            new SelectItemProjection(
                                "id",
                                new FieldInteger("t.users", "id")
                            ),
                        ]
                    ),
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromSubquery("reach"),
                        select:
                        [
                            new SelectItemProjection("id", new FieldInteger("reach", "id")),
                        ]
                    ),
                ],
                [Reach(10L)]
            ),
            "6",
            "7"
        );
    }

    [Fact]
    public void ATotalQueryAggregatesTheRowsOfARecursion()
    {
        // reach: (1, 0), (2, 1), (3, 1), (4, 2) twice, (5, 3) twice.
        AssertRows(
            new MainTotalQuery(
                subqueries: [Reach(10L)],
                from: new FromSubquery("reach"),
                aggregate:
                [
                    new SelectItemTotal("rows", new CountTotal()),
                    new SelectItemTotal(
                        "ids",
                        new CountDistinctTotal(new FieldInteger("reach", "id"))
                    ),
                    new SelectItemTotal(
                        "deepest",
                        new MaxIntegerNullableTotal(new FieldInteger("reach", "depth"))
                    ),
                    new SelectItemTotal(
                        "depths",
                        new SumIntegerTotal(new FieldInteger("reach", "depth"))
                    ),
                ]
            ),
            "7|5|3|12"
        );
    }

    [Fact]
    public void ARecursionIsJoinedWithAStoredTable()
    {
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries: [Reach(10L)],
                from: new FromSubquery("reach"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Inner,
                        "t.users",
                        new EqualDecimalRow(
                            new FieldInteger("t.users", "id"),
                            new FieldInteger("reach", "id")
                        )
                    ),
                ],
                select:
                [
                    new SelectItemProjection("name", new FieldString("t.users", "name")),
                    new SelectItemProjection("depth", new FieldInteger("reach", "depth")),
                ],
                orderBy:
                [
                    new OrderItemProjectionColumn("depth"),
                    new OrderItemProjectionColumn("name"),
                ]
            ),
            "ann|0",
            "bob|1",
            "cid|1",
            "dan|2",
            "dan|2",
            "eve|3",
            "eve|3"
        );
    }

    [Fact]
    public void ARecursionIsTheListOfInOfALaterSubquery()
    {
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    Reach(10L),
                    new SubqueryQuery(
                        "reached",
                        new PlainQueryWithoutPaginationWithoutOrderBy(
                            from: new FromEntity("t.users"),
                            where: new InDecimalRow(
                                new FieldInteger("t.users", "id"),
                                new ListSubqueryColumnInteger("reach", "id")
                            ),
                            select:
                            [
                                new SelectItemProjection(
                                    "name",
                                    new FieldString("t.users", "name")
                                ),
                            ]
                        )
                    ),
                ],
                from: new FromSubquery("reached"),
                select:
                [
                    new SelectItemProjection("name", new FieldString("reached", "name")),
                ],
                orderBy: [new OrderItemProjectionColumn("name")]
            ),
            "ann",
            "bob",
            "cid",
            "dan",
            "eve"
        );
    }

    [Fact]
    public void NextJoinsOnNullableKeysAndPairsTheNulls()
    {
        // ann has no referrer; equal pairs it with every user without one,
        // ann and fay (Null semantics: also in join.on).
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRecursive(
                        "peers",
                        new Recursion(
                            seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.users"),
                                where: IsUser(1),
                                select: Peer("t.users", new LiteralInteger(0))
                            ),
                            next: new PlainNextQuery(
                                from: new FromSubquery("peers"),
                                joins:
                                [
                                    new JoinEntity(
                                        JoinKind.Inner,
                                        "t.users",
                                        new EqualDecimalRow(
                                            new FieldIntegerNullable("t.users", "referrer"),
                                            new FieldIntegerNullable("peers", "referrer")
                                        )
                                    ),
                                ],
                                where: new LessThanDecimalRow(
                                    new FieldInteger("peers", "depth"),
                                    new LiteralInteger(1)
                                ),
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
                                            new FieldInteger("peers", "depth"),
                                            new LiteralInteger(1),
                                        ])
                                    ),
                                ]
                            ),
                            maxDepth: 5L
                        )
                    ),
                ],
                from: new FromSubquery("peers"),
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("peers", "id")),
                    new SelectItemProjection(
                        "referrer",
                        new FieldIntegerNullable("peers", "referrer")
                    ),
                    new SelectItemProjection("depth", new FieldInteger("peers", "depth")),
                ],
                orderBy:
                [
                    new OrderItemProjectionColumn("depth"),
                    new OrderItemProjectionColumn("id"),
                ]
            ),
            "1||0",
            "1||1",
            "6||1"
        );
    }

    [Fact]
    public void OneParameterIsTheMaxDepthAndTheDepthNextStopsAt()
    {
        // A parameter has one type in the whole document; next stops at
        // depth 2, which is maxDepth, so the query does not fail.
        AssertRows(
            new PureQLProjection(
                Tables,
                ReadingTree(
                    Recursive(
                        Root(1),
                        new PlainNextQuery(
                            from: new FromEntity("t.users"),
                            joins: [JoinTree()],
                            where: new LessThanDecimalRow(
                                new FieldInteger("tree", "depth"),
                                new ParamInteger("deepest")
                            ),
                            select: Step(new FieldInteger("t.users", "id"), DepthPlusOne())
                        ),
                        new ParamInteger("deepest")
                    )
                ),
                new Dictionary<string, IParameter>
                {
                    ["deepest"] = new TextParameter("integer", "2"),
                }
            ),
            "1|0",
            "2|1",
            "3|1",
            "4|2"
        );
    }

    [Fact(
        Skip = "Bug: a subquery with orderBy + distinctBy (no pagination) read by in"
            + " drops distinctBy (QueryUse.Membership); spec: next keeps only cid"
            + " (1|0, 3|1), library follows every child (1|0, 2|1, 3|1, 4|2, 5|3)"
    )]
    public void NextFilteredByInOverASubqueryWithDistinctByFollowsOnlyItsRows()
    {
        // chosen keeps, for each referrer, the user of the highest id: 6
        // (none), 3 (ann), 4 (bob), 5 (dan) and 7 (fay). Of ann's bob and
        // cid only cid is chosen, and cid refers nobody.
        AssertRows(
            new PureQLProjection(
                Tables,
                ReadingTree(
                    Recursive(
                        Root(1),
                        new PlainNextQuery(
                            from: new FromEntity("t.users"),
                            joins: [JoinTree()],
                            where: new InDecimalRow(
                                new FieldInteger("t.users", "id"),
                                new ListSubqueryColumnInteger("chosen", "id")
                            ),
                            select: Step(new FieldInteger("t.users", "id"), DepthPlusOne())
                        )
                    ),
                    new SubqueryQuery(
                        "chosen",
                        new PlainQueryWithoutPaginationWithOrderBy(
                            from: new FromEntity("t.users"),
                            select:
                            [
                                new SelectItemProjection(
                                    "referrer",
                                    new FieldIntegerNullable("t.users", "referrer")
                                ),
                                new SelectItemProjection(
                                    "id",
                                    new FieldInteger("t.users", "id")
                                ),
                            ],
                            orderBy:
                            [
                                new OrderItemProjectionColumn("id", SortDirection.Desc),
                            ],
                            distinctBy: ["referrer"]
                        )
                    )
                )
            ),
            "1|0",
            "3|1"
        );
    }

    [Fact]
    public void ACalendarBetweenDateMathParametersMovesWithTheClock()
    {
        // Each run binds now again; the days from three days ago to today.
        FixedClock clock = FixedClock.At("2026-10-29T08:30:00Z");
        PureQLProjection projection = new(
            Tables,
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRows(
                        "first_day",
                        [[new RowItem("day", new ParamDate("from"))]]
                    ),
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
                            maxDepth: 3L
                        )
                    ),
                ],
                from: new FromSubquery("days"),
                select: [new SelectItemProjection("day", new FieldDate("days", "day"))],
                orderBy: [new OrderItemProjectionColumn("day")]
            ),
            new Dictionary<string, IParameter>
            {
                ["from"] = new TextParameter("date", "now-3d"),
                ["to"] = new TextParameter("date", "now"),
            },
            clock
        );

        Assert.Equal(
            ["2026-10-26", "2026-10-27", "2026-10-28", "2026-10-29"],
            Texts(projection)
        );
        clock.Now = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero);
        Assert.Equal(
            ["2026-10-29", "2026-10-30", "2026-10-31", "2026-11-01"],
            Texts(projection)
        );
    }

    [Fact]
    public void TheMaxDepthParameterHasOneTypeInTheWholeDocument()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                ReadingTree(
                    Recursive(
                        Root(1),
                        new PlainNextQuery(
                            from: new FromEntity("t.users"),
                            joins: [JoinTree()],
                            where: new LessThanDecimalRow(
                                new FieldInteger("tree", "depth"),
                                new ParamIntegerNullable("deepest")
                            ),
                            select: Step(new FieldInteger("t.users", "id"), DepthPlusOne())
                        ),
                        new ParamInteger("deepest")
                    )
                ),
                new Dictionary<string, IParameter>
                {
                    ["deepest"] = new TextParameter("integer", "2"),
                }
            )
        );
    }

    [Fact]
    public void ACombinedSeedOfAnIntegerAndADecimalIsFollowedByIntegers()
    {
        // The seed's x is a decimal (1 and 0.5); next gives the integer ids
        // of the users each x referred: 1 is ann, 0.5 is nobody.
        Query one = new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.users"),
            where: IsUser(1),
            select: [new SelectItemProjection("x", new FieldInteger("t.users", "id"))]
        );
        Query half = new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.users"),
            where: IsUser(1),
            select: [new SelectItemProjection("x", new LiteralDecimal(0.5m))]
        );

        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRecursive(
                        "below",
                        new Recursion(
                            seed: new CombinedQueryUnionAll([one, half]),
                            next: new PlainNextQuery(
                                from: new FromEntity("t.users"),
                                joins:
                                [
                                    new JoinSubquery(
                                        JoinKind.Inner,
                                        "below",
                                        new EqualDecimalRow(
                                            new FieldIntegerNullable("t.users", "referrer"),
                                            new FieldDecimal("below", "x")
                                        )
                                    ),
                                ],
                                select:
                                [
                                    new SelectItemRow(
                                        "x",
                                        new FieldInteger("t.users", "id")
                                    ),
                                ]
                            ),
                            maxDepth: 3L
                        )
                    ),
                ],
                from: new FromSubquery("below"),
                select: [new SelectItemProjection("x", new FieldDecimal("below", "x"))],
                orderBy: [new OrderItemProjectionColumn("x")]
            ),
            "0.5",
            "1",
            "2",
            "3",
            "4",
            "5"
        );
    }

    [Fact]
    public void AGroupedNextOverAnEmptySeedGivesNoRows()
    {
        AssertRows(ReadingMerged(Merged(IsUser(99))));
    }

    [Fact]
    public void AGroupedNextMergesARepeatedSeedRow()
    {
        // The seed holds (1, 0) twice: level 1 is 2 and 3, each reached by
        // two rows; level 2 is 4, reached by one row of 2 and one of 3.
        AssertRows(
            ReadingMerged(
                Merged(null),
                new SubqueryRows(
                    "starts",
                    [
                        [new RowItem("id", new LiteralInteger(1))],
                        [new RowItem("id", new LiteralInteger(1))],
                    ]
                )
            ),
            "1|0|1",
            "1|0|1",
            "2|1|2",
            "3|1|2",
            "4|2|2",
            "5|3|1"
        );
    }

    [Fact]
    public void AFailingRecursionReadOnlyByInFailsTheQuery()
    {
        // An error fails the whole query (Semantics); subqueries behave as
        // if each ran once, before the main query.
        AssertFails(
            new MainPlainQueryWithoutPagination(
                subqueries: [Recursive(Root(1), Children(), 2L)],
                from: new FromEntity("t.users"),
                where: new InDecimalRow(
                    new FieldInteger("t.users", "id"),
                    new ListSubqueryColumnInteger("tree", "id")
                ),
                select: [new SelectItemProjection("id", new FieldInteger("t.users", "id"))]
            )
        );
    }

    [Fact]
    public void AFailingRecursionReadOnlyByAnAggregateFailsTheQuery()
    {
        AssertFails(
            new MainTotalQuery(
                subqueries: [Recursive(Root(1), Children(), 2L)],
                from: new FromSubquery("tree"),
                aggregate: [new SelectItemTotal("rows", new CountTotal())]
            )
        );
    }

    [Fact]
    public void AFailingRecursionOfWhichOnePageIsReadFailsTheQuery()
    {
        AssertFails(
            new MainPlainQueryWithPagination(
                subqueries: [Recursive(Root(1), Children(), 2L)],
                from: new FromSubquery("tree"),
                select: [new SelectItemProjection("id", new FieldInteger("tree", "id"))],
                orderBy: [new OrderItemProjectionColumn("id")],
                pagination: new Pagination(take: 1L)
            )
        );
    }

    [Fact]
    public void AFailingRecursionAsAnInputOfAUnionFailsTheQuery()
    {
        AssertFails(
            new MainCombinedQueryUnion(
                [
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromEntity("t.users"),
                        select:
                        [
                            new SelectItemProjection(
                                "id",
                                new FieldInteger("t.users", "id")
                            ),
                        ]
                    ),
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromSubquery("tree"),
                        select:
                        [
                            new SelectItemProjection("id", new FieldInteger("tree", "id")),
                        ]
                    ),
                ],
                [Recursive(Root(1), Children(), 2L)]
            )
        );
    }

    [Fact]
    public void AFailingRecursionJoinedToAStoredTableFailsTheQuery()
    {
        AssertFails(
            new MainPlainQueryWithoutPagination(
                subqueries: [Recursive(Root(1), Children(), 2L)],
                from: new FromEntity("t.users"),
                joins:
                [
                    new JoinSubquery(
                        JoinKind.Left,
                        "tree",
                        new EqualDecimalRow(
                            new FieldInteger("t.users", "id"),
                            new FieldInteger("tree", "id")
                        )
                    ),
                ],
                select: [new SelectItemProjection("id", new FieldInteger("t.users", "id"))]
            )
        );
    }

    // Helpers ---------------------------------------------------------------

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

    private static SelectItemRow[] Step(ExprRow id, ExprRow depth)
    {
        return [new SelectItemRow("id", id), new SelectItemRow("depth", depth)];
    }

    private static AddIntegerRow DepthPlusOne()
    {
        return new AddIntegerRow([new FieldInteger("tree", "depth"), new LiteralInteger(1)]);
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

    // The users the rows of tree referred, with the given columns, by
    // default one level deeper.
    private static NextQuery Children(SelectItemRow[]? select = null)
    {
        return new PlainNextQuery(
            from: new FromEntity("t.users"),
            joins: [JoinTree()],
            select: select ?? Step(new FieldInteger("t.users", "id"), DepthPlusOne())
        );
    }

    private static Subquery Recursive(
        Query seed,
        NextQuery next,
        RecursionMaxDepth? maxDepth = null
    )
    {
        return new SubqueryRecursive(
            "tree",
            new Recursion(seed: seed, next: next, maxDepth: maxDepth ?? 50L)
        );
    }

    // The rows of tree, by depth and id; the given subqueries come first.
    private static PureQLQuery ReadingTree(Subquery tree, params Subquery[] before)
    {
        return new MainPlainQueryWithoutPagination(
            subqueries: [.. before, tree],
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

    // 1 and everything reached from it along the edges, with its depth.
    private static Subquery Reach(RecursionMaxDepth maxDepth)
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
                    ]
                ),
                next: new PlainNextQuery(
                    from: new FromSubquery("reach"),
                    joins:
                    [
                        new JoinEntity(
                            JoinKind.Inner,
                            "t.edges",
                            new EqualDecimalRow(
                                new FieldInteger("t.edges", "source"),
                                new FieldInteger("reach", "id")
                            )
                        ),
                    ],
                    select:
                    [
                        new SelectItemRow("id", new FieldInteger("t.edges", "target")),
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

    // The rows of a counter from 0 to the given last value: their number,
    // their sum and the largest.
    private static PureQLQuery Counter(long last, RecursionMaxDepth maxDepth)
    {
        return new MainTotalQuery(
            subqueries:
            [
                new SubqueryRows("zero", [[new RowItem("x", new LiteralInteger(0))]]),
                new SubqueryRecursive(
                    "counter",
                    new Recursion(
                        seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                            from: new FromSubquery("zero"),
                            select:
                            [
                                new SelectItemProjection(
                                    "x",
                                    new FieldInteger("zero", "x")
                                ),
                            ]
                        ),
                        next: new PlainNextQuery(
                            from: new FromSubquery("counter"),
                            where: new LessThanDecimalRow(
                                new FieldInteger("counter", "x"),
                                new LiteralInteger(last)
                            ),
                            select:
                            [
                                new SelectItemRow(
                                    "x",
                                    new AddIntegerRow([
                                        new FieldInteger("counter", "x"),
                                        new LiteralInteger(1),
                                    ])
                                ),
                            ]
                        ),
                        maxDepth: maxDepth
                    )
                ),
            ],
            from: new FromSubquery("counter"),
            aggregate:
            [
                new SelectItemTotal("rows", new CountTotal()),
                new SelectItemTotal(
                    "sum",
                    new SumIntegerTotal(new FieldInteger("counter", "x"))
                ),
                new SelectItemTotal(
                    "max",
                    new MaxIntegerNullableTotal(new FieldInteger("counter", "x"))
                ),
            ]
        );
    }

    // 1 halved while it is above 0.1, largest first.
    private static PureQLQuery Halving(RecursionMaxDepth maxDepth)
    {
        return new MainPlainQueryWithoutPagination(
            subqueries:
            [
                new SubqueryRows("one", [[new RowItem("x", new LiteralDecimal(1m))]]),
                new SubqueryRecursive(
                    "halves",
                    new Recursion(
                        seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                            from: new FromSubquery("one"),
                            select:
                            [
                                new SelectItemProjection(
                                    "x",
                                    new FieldDecimal("one", "x")
                                ),
                            ]
                        ),
                        next: new PlainNextQuery(
                            from: new FromSubquery("halves"),
                            where: new GreaterThanDecimalRow(
                                new FieldDecimal("halves", "x"),
                                new LiteralDecimal(0.1m)
                            ),
                            select:
                            [
                                new SelectItemRow(
                                    "x",
                                    new DivideDecimalRow([
                                        new FieldDecimal("halves", "x"),
                                        new LiteralInteger(2),
                                    ])
                                ),
                            ]
                        ),
                        maxDepth: maxDepth
                    )
                ),
            ],
            from: new FromSubquery("halves"),
            select: [new SelectItemProjection("x", new FieldDecimal("halves", "x"))],
            orderBy: [new OrderItemProjectionColumn("x", SortDirection.Desc)]
        );
    }

    // From 0, each row x below 3 followed by x + 1 and x + 2, sorted.
    private static PureQLQuery Branching(RecursionMaxDepth maxDepth)
    {
        return new MainPlainQueryWithoutPagination(
            subqueries:
            [
                new SubqueryRows(
                    "steps",
                    [
                        [new RowItem("step", new LiteralInteger(1))],
                        [new RowItem("step", new LiteralInteger(2))],
                    ]
                ),
                new SubqueryRows("zero", [[new RowItem("x", new LiteralInteger(0))]]),
                new SubqueryRecursive(
                    "walk",
                    new Recursion(
                        seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                            from: new FromSubquery("zero"),
                            select:
                            [
                                new SelectItemProjection(
                                    "x",
                                    new FieldInteger("zero", "x")
                                ),
                            ]
                        ),
                        next: new PlainNextQuery(
                            from: new FromSubquery("walk"),
                            joins:
                            [
                                new JoinSubquery(
                                    JoinKind.Inner,
                                    "steps",
                                    new LiteralBoolean(true)
                                ),
                            ],
                            where: new LessThanDecimalRow(
                                new FieldInteger("walk", "x"),
                                new LiteralInteger(3)
                            ),
                            select:
                            [
                                new SelectItemRow(
                                    "x",
                                    new AddIntegerRow([
                                        new FieldInteger("walk", "x"),
                                        new FieldInteger("steps", "step"),
                                    ])
                                ),
                            ]
                        ),
                        maxDepth: maxDepth
                    )
                ),
            ],
            from: new FromSubquery("walk"),
            select: [new SelectItemProjection("x", new FieldInteger("walk", "x"))],
            orderBy: [new OrderItemProjectionColumn("x")]
        );
    }

    private static SelectItemProjection[] Peer(string source, ExprProjection depth)
    {
        return
        [
            new SelectItemProjection("id", new FieldInteger(source, "id")),
            new SelectItemProjection(
                "referrer",
                new FieldIntegerNullable(source, "referrer")
            ),
            new SelectItemProjection("depth", depth),
        ];
    }

    // reach with a grouped next: one row for each target the rows of a
    // level reach, with the number of rows that reach it. The seed is the
    // users the condition keeps, or the inline rows starts when given.
    private static Subquery Merged(BooleanRow? seed)
    {
        Query start = seed is null
            ? new PlainQueryWithoutPaginationWithoutOrderBy(
                from: new FromSubquery("starts"),
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("starts", "id")),
                    new SelectItemProjection("depth", new LiteralInteger(0)),
                    new SelectItemProjection("paths", new LiteralInteger(1)),
                ]
            )
            : new PlainQueryWithoutPaginationWithoutOrderBy(
                from: new FromEntity("t.users"),
                where: seed,
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("t.users", "id")),
                    new SelectItemProjection("depth", new LiteralInteger(0)),
                    new SelectItemProjection("paths", new LiteralInteger(1)),
                ]
            );

        return new SubqueryRecursive(
            "reach",
            new Recursion(
                seed: start,
                next: new GroupedNextQuery(
                    from: new FromSubquery("reach"),
                    joins:
                    [
                        new JoinEntity(
                            JoinKind.Inner,
                            "t.edges",
                            new EqualDecimalRow(
                                new FieldInteger("t.edges", "source"),
                                new FieldInteger("reach", "id")
                            )
                        ),
                    ],
                    groupBy:
                    [
                        new GroupKey("id", new FieldInteger("t.edges", "target")),
                        new GroupKey("depth", new FieldInteger("reach", "depth")),
                    ],
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
                        new SelectItemGroup("paths", new CountGroup()),
                    ]
                ),
                maxDepth: 10L
            )
        );
    }

    private static PureQLQuery ReadingMerged(Subquery reach, params Subquery[] before)
    {
        return new MainPlainQueryWithoutPagination(
            subqueries: [.. before, reach],
            from: new FromSubquery("reach"),
            select:
            [
                new SelectItemProjection("id", new FieldInteger("reach", "id")),
                new SelectItemProjection("depth", new FieldInteger("reach", "depth")),
                new SelectItemProjection("paths", new FieldInteger("reach", "paths")),
            ],
            orderBy:
            [
                new OrderItemProjectionColumn("depth"),
                new OrderItemProjectionColumn("id"),
            ]
        );
    }

    private static void AssertRejected(Subquery tree)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(Tables, ReadingTree(tree))
        );
    }

    private static void AssertFails(PureQLQuery query)
    {
        PureQLProjection projection = new(Tables, query);

        _ = Assert.Throws<InvalidOperationException>(() =>
            projection.AsEnumerable().ToList()
        );
    }

    private static void AssertRows(PureQLQuery query, params string[] expected)
    {
        AssertRows(new PureQLProjection(Tables, query), expected);
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
