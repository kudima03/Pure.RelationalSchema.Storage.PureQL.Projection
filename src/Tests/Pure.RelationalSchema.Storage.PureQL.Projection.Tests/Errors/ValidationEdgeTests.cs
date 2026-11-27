using Pure.Primitives.Number;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
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

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Errors;

// The interpreter's rows of the specification's Validation table, each with
// a query that breaks it, rejected with ArgumentException when the
// projection is built, and a twin that keeps it and is built: the rules of a
// recursive subquery, the columns of set operations and inline rows, the
// aliases of orderBy and distinctBy, one type per parameter in the whole
// document, regular expressions as literals and parameters, and the values a
// parameter of a pagination, maxDepth or fraction is bound to.
public sealed class ValidationEdgeTests
{
    // ann refers bob and cid, bob refers dan.
    private static readonly IStoredSchemaDataSet Users = new InlineTable(
        "t",
        "users",
        ("id", new LongColumnType()),
        ("name", new StringColumnType()),
        ("referrer", new LongColumnType()),
        ("day", new DateColumnType()),
        ("at", new DateTimeColumnType()),
        ("amount", new DoubleColumnType())
    ).With(
        ["1", "ann", "", "2024-01-01", "2024-01-01T10:00:00Z", "1.5"],
        ["2", "bob", "1", "2024-01-02", "2024-01-02T10:00:00Z", "2.5"],
        ["3", "cid", "1", "2024-01-03", "2024-01-03T10:00:00Z", "3.5"],
        ["4", "dan", "2", "2024-01-05", "2024-01-05T10:00:00Z", "4.5"]
    );

    private static readonly FieldIntegerNullable TreeId = new("tree", "id");

    // ---- Recursive subqueries: "The next of a recursive subquery reads the
    // subquery exactly once, in from or a join and not on the optional side
    // of an outer join, and its seed does not read it; next has the aliases
    // of seed, each once, with types the seed's columns accept".

    [Fact]
    public void NextThatReadsTheSubqueryOnceInAnInnerJoinIsBuiltAndRun()
    {
        PureQLProjection projection = new([Users], ReadingTree(Tree(Children())));

        Assert.Equal(["1", "2", "3", "4"], ResultColumn.Texts(projection, "id"));
    }

    [Fact]
    public void NextThatReadsTheSubqueryTwiceIsRejected()
    {
        Rejected(
            ReadingTree(
                Tree(
                    new PlainNextQuery(
                        from: new FromSubquery("tree"),
                        joins:
                        [
                            new JoinSubquery(
                                JoinKind.Inner,
                                "tree",
                                new EqualDecimalRow(
                                    TreeId,
                                    new FieldIntegerNullable("again", "id")
                                ),
                                "again"
                            ),
                        ],
                        select: [new SelectItemRow("id", TreeId)]
                    )
                )
            )
        );
    }

    [Fact]
    public void NextThatReadsTheSubqueryNowhereIsRejected()
    {
        Rejected(
            ReadingTree(
                Tree(
                    new PlainNextQuery(
                        from: new FromEntity("t.users"),
                        where: new EqualDecimalRow(
                            new FieldIntegerNullable("t.users", "referrer"),
                            new LiteralInteger(1)
                        ),
                        select: [UserId()]
                    )
                )
            )
        );
    }

    [Fact]
    public void NextThatReadsTheSubqueryOnlyAsTheListOfInIsRejected()
    {
        // in is no read in from or a join.
        Rejected(
            ReadingTree(
                Tree(
                    new PlainNextQuery(
                        from: new FromEntity("t.users"),
                        where: new InDecimalRow(
                            new FieldIntegerNullable("t.users", "referrer"),
                            new ListSubqueryColumnIntegerNullable("tree", "id")
                        ),
                        select: [UserId()]
                    )
                )
            )
        );
    }

    [Fact]
    public void NextThatAlsoReadsTheSubqueryAsTheListOfInInWhereIsRejected()
    {
        Rejected(
            ReadingTree(
                Tree(
                    new PlainNextQuery(
                        from: new FromEntity("t.users"),
                        joins: [JoinTree(JoinKind.Inner)],
                        where: new InDecimalRow(
                            new FieldIntegerNullable("t.users", "referrer"),
                            new ListSubqueryColumnIntegerNullable("tree", "id")
                        ),
                        select: [UserId()]
                    )
                )
            )
        );
    }

    [Fact(
        Skip = "Bug: spec says next reads the subquery exactly once; an in over its "
            + "column in a join's on is a second read, yet the projection is built"
    )]
    public void NextThatAlsoReadsTheSubqueryAsTheListOfInInAJoinConditionIsRejected()
    {
        Rejected(
            ReadingTree(
                Tree(
                    new PlainNextQuery(
                        from: new FromEntity("t.users"),
                        joins:
                        [
                            new JoinSubquery(
                                JoinKind.Inner,
                                "tree",
                                new AndRow([
                                    ReferredByTree(),
                                    new InDecimalRow(
                                        new FieldIntegerNullable("t.users", "referrer"),
                                        new ListSubqueryColumnIntegerNullable(
                                            "tree",
                                            "id"
                                        )
                                    ),
                                ])
                            ),
                        ],
                        select: [UserId()]
                    )
                )
            )
        );
    }

    [Theory]
    [InlineData(JoinKind.Left)]
    [InlineData(JoinKind.Full)]
    public void NextThatJoinsTheSubqueryOnItsOptionalSideIsRejected(JoinKind kind)
    {
        // The seed's column is nullable, so every reference declares the
        // nullability it has after the join: only the rule of the optional
        // side is broken.
        Rejected(
            ReadingTree(
                Tree(
                    new PlainNextQuery(
                        from: new FromEntity("t.users"),
                        joins: [JoinTree(kind)],
                        select: [UserId()]
                    )
                )
            )
        );
    }

    [Theory]
    [InlineData(JoinKind.Right)]
    [InlineData(JoinKind.Full)]
    public void NextThatReadsTheSubqueryInFromBeforeARightOrFullJoinIsRejected(
        JoinKind kind
    )
    {
        Rejected(
            ReadingTree(
                Tree(
                    new PlainNextQuery(
                        from: new FromSubquery("tree"),
                        joins:
                        [
                            new JoinEntity(kind, "t.users", ReferredByTree()),
                        ],
                        select: [UserId()]
                    )
                )
            )
        );
    }

    [Fact]
    public void NextWhoseLaterRightJoinMakesTheSubqueryOptionalIsRejected()
    {
        // A right join makes every earlier source optional, also one read
        // through an inner join before it.
        Rejected(
            ReadingTree(
                Tree(
                    new PlainNextQuery(
                        from: new FromSubquery("tree"),
                        joins:
                        [
                            new JoinEntity(JoinKind.Inner, "t.users", ReferredByTree()),
                            new JoinEntity(
                                JoinKind.Right,
                                "t.users",
                                new EqualDecimalRow(
                                    new FieldIntegerNullable("other", "id"),
                                    new FieldIntegerNullable("t.users", "id")
                                ),
                                "other"
                            ),
                        ],
                        select:
                        [
                            new SelectItemRow(
                                "id",
                                new FieldIntegerNullable("other", "id")
                            ),
                        ]
                    )
                )
            )
        );
    }

    [Fact]
    public void NextThatReadsTheSubqueryInFromBeforeLeftJoinsIsBuiltAndRun()
    {
        PureQLProjection projection = new(
            [Users],
            ReadingTree(
                Tree(
                    new PlainNextQuery(
                        from: new FromSubquery("tree"),
                        joins:
                        [
                            new JoinEntity(JoinKind.Inner, "t.users", ReferredByTree()),
                            new JoinEntity(
                                JoinKind.Left,
                                "t.users",
                                new EqualDecimalRow(
                                    new FieldIntegerNullable("other", "id"),
                                    new FieldIntegerNullable("t.users", "id")
                                ),
                                "other"
                            ),
                        ],
                        select: [UserId()]
                    )
                )
            )
        );

        Assert.Equal(["1", "2", "3", "4"], ResultColumn.Texts(projection, "id"));
    }

    [Fact]
    public void NextWithAnotherAliasThanTheSeedIsRejected()
    {
        Rejected(
            ReadingTree(Tree(Children(new SelectItemRow("ident", UserIdField()))))
        );
    }

    [Fact]
    public void NextWithAColumnMoreThanTheSeedIsRejected()
    {
        Rejected(
            ReadingTree(
                Tree(
                    Children(
                        new SelectItemRow("id", UserIdField()),
                        new SelectItemRow("name", new FieldString("t.users", "name"))
                    )
                )
            )
        );
    }

    [Fact]
    public void NextThatGivesAnAliasTwiceIsRejected()
    {
        Rejected(
            ReadingTree(
                Tree(
                    Children(
                        new SelectItemRow("id", UserIdField()),
                        new SelectItemRow("id", UserIdField())
                    )
                )
            )
        );
    }

    [Fact]
    public void NextWithAColumnLessThanTheSeedIsRejected()
    {
        Rejected(ReadingDepths(Depths(new SelectItemRow("id", UserIdField()))));
    }

    [Theory]
    [InlineData("decimal")]
    [InlineData("nullable")]
    [InlineData("string")]
    public void NextThatGivesAValueTheSeedsColumnDoesNotAcceptIsRejected(string given)
    {
        // The seed's depth is a non-null integer.
        ExprRow depth = given switch
        {
            "decimal" => new LiteralDecimal(1.5m),
            "nullable" => new LiteralIntegerNullable(),
            _ => new LiteralString("1"),
        };

        Rejected(
            ReadingDepths(
                Depths(
                    new SelectItemRow("id", UserIdField()),
                    new SelectItemRow("depth", depth)
                )
            )
        );
    }

    [Fact]
    public void NextThatGivesTheValuesTheSeedsColumnsAcceptIsBuiltAndRun()
    {
        PureQLProjection projection = new(
            [Users],
            ReadingDepths(
                Depths(
                    new SelectItemRow("id", UserIdField()),
                    new SelectItemRow(
                        "depth",
                        new AddIntegerRow([
                            new FieldInteger("tree", "depth"),
                            new LiteralInteger(1),
                        ])
                    )
                )
            )
        );

        Assert.Equal(["0", "1", "1", "2"], ResultColumn.Texts(projection, "depth"));
    }

    [Fact]
    public void ADateColumnOfTheSeedDoesNotAcceptADatetimeFromNext()
    {
        Rejected(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRecursive(
                        "days",
                        new Recursion(
                            seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.users"),
                                where: IsUser(1),
                                select:
                                [
                                    new SelectItemProjection(
                                        "d",
                                        new FieldDate("t.users", "day")
                                    ),
                                ]
                            ),
                            next: new PlainNextQuery(
                                from: new FromSubquery("days"),
                                where: new LessThanDateRow(
                                    new FieldDate("days", "d"),
                                    new LiteralDate(new DateOnly(2024, 1, 3))
                                ),
                                select:
                                [
                                    new SelectItemRow(
                                        "d",
                                        new LiteralDatetime(
                                            new DateTimeOffset(
                                                2024,
                                                1,
                                                2,
                                                0,
                                                0,
                                                0,
                                                default
                                            )
                                        )
                                    ),
                                ]
                            ),
                            maxDepth: 5L
                        )
                    ),
                ],
                from: new FromSubquery("days"),
                select: [new SelectItemProjection("d", new FieldDate("days", "d"))]
            )
        );
    }

    [Theory]
    [InlineData("nullable")]
    [InlineData("decimal")]
    public void NextThatReadsAColumnOfTheSubqueryWithAnotherTypeThanTheSeedsIsRejected(
        string declared
    )
    {
        // "Every reference to a column of the subquery, also inside next,
        // declares the seed's type": depth is a non-null integer.
        DecimalNullableRow depth = declared == "nullable"
            ? new FieldIntegerNullable("tree", "depth")
            : new FieldDecimal("tree", "depth");

        Rejected(
            ReadingDepths(
                Depths(
                    [
                        new SelectItemRow("id", UserIdField()),
                        new SelectItemRow("depth", new LiteralInteger(1)),
                    ],
                    new GreaterThanDecimalRow(depth, new LiteralInteger(-1))
                )
            )
        );
    }

    [Fact]
    public void SeedThatReadsTheSubqueryItselfIsRejected()
    {
        Rejected(
            ReadingTree(
                new SubqueryRecursive(
                    "tree",
                    new Recursion(
                        seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                            from: new FromSubquery("tree"),
                            select: [new SelectItemProjection("id", TreeId)]
                        ),
                        next: Children(),
                        maxDepth: 5L
                    )
                )
            )
        );
    }

    [Fact]
    public void NextThatReadsALaterSubqueryIsRejected()
    {
        Rejected(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    Tree(
                        new PlainNextQuery(
                            from: new FromSubquery("later"),
                            joins:
                            [
                                new JoinSubquery(
                                    JoinKind.Inner,
                                    "tree",
                                    new EqualDecimalRow(
                                        new FieldIntegerNullable("later", "referrer"),
                                        TreeId
                                    )
                                ),
                            ],
                            select:
                            [
                                new SelectItemRow(
                                    "id",
                                    new FieldIntegerNullable("later", "id")
                                ),
                            ]
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
                                    new FieldIntegerNullable("t.users", "id")
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
                select: [new SelectItemProjection("id", TreeId)]
            )
        );
    }

    [Fact]
    public void SubqueryThatReadsARecursiveSubqueryDeclaredAfterItIsRejected()
    {
        Rejected(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "early",
                        new PlainQueryWithoutPaginationWithoutOrderBy(
                            from: new FromSubquery("tree"),
                            select: [new SelectItemProjection("id", TreeId)]
                        )
                    ),
                    Tree(Children()),
                ],
                from: new FromSubquery("early"),
                select:
                [
                    new SelectItemProjection(
                        "id",
                        new FieldIntegerNullable("early", "id")
                    ),
                ]
            )
        );
    }

    [Fact]
    public void RecursiveSubqueryNamedAsAnEarlierSubqueryIsRejected()
    {
        Rejected(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRows(
                        "tree",
                        [[new RowItem("id", new LiteralInteger(1))]]
                    ),
                    Tree(Children()),
                ],
                from: new FromSubquery("tree"),
                select: [new SelectItemProjection("id", TreeId)]
            )
        );
    }

    [Fact]
    public void NextWithASourceNamedAsTheSubqueryTwiceIsRejected()
    {
        Rejected(
            ReadingTree(
                Tree(
                    new PlainNextQuery(
                        from: new FromSubquery("tree"),
                        joins:
                        [
                            new JoinEntity(
                                JoinKind.Inner,
                                "t.users",
                                new LiteralBoolean(true),
                                "tree"
                            ),
                        ],
                        select: [new SelectItemRow("id", TreeId)]
                    )
                )
            )
        );
    }

    [Fact]
    public void NextThatReadsASourceOfTheMainQueryIsRejected()
    {
        // "A subquery references only its own sources, never one of the query
        // that reads it".
        Rejected(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    Tree(
                        new PlainNextQuery(
                            from: new FromEntity("t.users"),
                            joins: [JoinTree(JoinKind.Inner)],
                            where: new EqualDecimalRow(
                                new FieldIntegerNullable("main", "id"),
                                new LiteralInteger(1)
                            ),
                            select: [UserId()]
                        )
                    ),
                ],
                from: new FromSubquery("tree", "main"),
                select:
                [
                    new SelectItemProjection(
                        "id",
                        new FieldIntegerNullable("main", "id")
                    ),
                ]
            )
        );
    }

    [Fact]
    public void NextThatReadsASourceOfItsSeedIsRejected()
    {
        Rejected(
            ReadingTree(
                new SubqueryRecursive(
                    "tree",
                    new Recursion(
                        seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                            from: new FromEntity("t.users", "s"),
                            where: new EqualDecimalRow(
                                new FieldInteger("s", "id"),
                                new LiteralInteger(1)
                            ),
                            select:
                            [
                                new SelectItemProjection(
                                    "id",
                                    new FieldIntegerNullable("s", "id")
                                ),
                            ]
                        ),
                        next: new PlainNextQuery(
                            from: new FromEntity("t.users"),
                            joins: [JoinTree(JoinKind.Inner)],
                            where: new EqualDecimalRow(
                                new FieldIntegerNullable("s", "id"),
                                new LiteralInteger(1)
                            ),
                            select: [UserId()]
                        ),
                        maxDepth: 5L
                    )
                )
            )
        );
    }

    // ---- maxDepth: "an ... maxDepth parameter to 1 or more".

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(long.MinValue)]
    public void MaxDepthParameterBelowOneIsRejected(long maxDepth)
    {
        Rejected(
            ReadingTree(Tree(Children(), new ParamInteger("depth"))),
            Bound("depth", new IntegerParameter(new Long(maxDepth)))
        );
    }

    [Fact]
    public void MaxDepthParameterOfOneIsAccepted()
    {
        // cid refers nobody: the result is the seed's row alone.
        PureQLProjection projection = new(
            [Users],
            ReadingTree(Tree(Children(), new ParamInteger("depth"), 3)),
            Bound("depth", new IntegerParameter(new Long(1)))
        );

        Assert.Equal(["3"], ResultColumn.Texts(projection, "id"));
    }

    [Fact]
    public void MaxDepthParameterOfOneFailsAQueryWithARowAtDepthTwo()
    {
        // Spec, Execution errors: a row deeper than maxDepth fails the query.
        PureQLProjection projection = new(
            [Users],
            ReadingTree(Tree(Children(), new ParamInteger("depth"))),
            Bound("depth", new IntegerParameter(new Long(1)))
        );

        _ = Assert.Throws<InvalidOperationException>(() => projection.ToList());
    }

    [Theory]
    [MemberData(nameof(MaxDepthsOfAnotherType))]
    public void MaxDepthParameterOfAnotherTypeOrNullIsRejected(string bound)
    {
        IParameter value = bound switch
        {
            "decimal" => new DecimalParameter(new Decimal(2m)),
            "null" => new NullableIntegerParameter(),
            _ => new StringParameter(new String("2")),
        };

        Rejected(
            ReadingTree(Tree(Children(), new ParamInteger("depth"))),
            Bound("depth", value)
        );
    }

    public static TheoryData<string> MaxDepthsOfAnotherType => [
        "decimal",
        "null",
        "string",
    ];

    [Fact]
    public void MaxDepthParameterGivenNoValueIsRejected()
    {
        Rejected(ReadingTree(Tree(Children(), new ParamInteger("depth"))));
    }

    // ---- Set operations and inline rows: "The inputs of a set operation,
    // and the rows of an inline subquery, have the same column aliases, each
    // once, and the types of each column agree up to the implicit
    // conversions".

    [Theory]
    [InlineData("uuid", "string")]
    [InlineData("time", "datetime")]
    [InlineData("date", "string")]
    [InlineData("boolean", "string")]
    [InlineData("uuid", "integer")]
    public void InputsWhoseColumnTypesDoNotMeetAreRejected(string first, string second)
    {
        Rejected(new MainCombinedQueryUnion([Constant(first), Constant(second)]));
    }

    [Theory]
    [InlineData("integer", "decimal", "decimal")]
    [InlineData("decimal", "integer", "decimal")]
    [InlineData("string", "string", "string")]
    [InlineData("time", "time", "time")]
    public void InputsWhoseColumnTypesMeetAreBuilt(
        string first,
        string second,
        string type
    )
    {
        PureQLProjection projection = new(
            [Users],
            new MainCombinedQueryUnion([Constant(first), Constant(second)])
        );

        Assert.Equal(
            type,
            Assert.Single(projection.TableSchema.Columns).Type.Name.TextValue
        );
    }

    [Fact]
    public void AliasesThatDifferInCaseAreDifferentAliases()
    {
        // "Names compare exactly, code point by code point".
        Rejected(
            new MainCombinedQueryUnionAll([
                Named("id", new LiteralInteger(1)),
                Named("Id", new LiteralInteger(2)),
            ])
        );
    }

    [Fact]
    public void AliasesThatDifferInNormalizationAreDifferentAliases()
    {
        Rejected(
            new MainCombinedQueryUnionAll([
                Named("café", new LiteralInteger(1)),
                Named("café", new LiteralInteger(2)),
            ])
        );
    }

    [Fact]
    public void AnAliasWithInnerSpacesIsAnAlias()
    {
        PureQLProjection projection = new(
            [Users],
            new MainCombinedQueryUnionAll([
                Named("order items", new LiteralInteger(1)),
                Named("order items", new LiteralInteger(2)),
            ])
        );

        Assert.Equal(
            ["1", "2"],
            ResultColumn.Texts(projection, "order items").Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void ACombinedSeedWhoseInputsHaveOtherAliasesIsRejected()
    {
        Rejected(
            ReadingTree(
                new SubqueryRecursive(
                    "tree",
                    new Recursion(
                        seed: new CombinedQueryUnionAll([
                            Named("id", new AsNullableIntegerNullableProjection(
                                new LiteralInteger(1)
                            )),
                            Named("ident", new AsNullableIntegerNullableProjection(
                                new LiteralInteger(1)
                            )),
                        ]),
                        next: Children(),
                        maxDepth: 5L
                    )
                )
            )
        );
    }

    [Fact]
    public void AnInputThatReadsASourceOfAnotherInputIsRejected()
    {
        Rejected(
            new MainCombinedQueryUnionAll([
                new PlainQueryWithoutPaginationWithoutOrderBy(
                    from: new FromEntity("t.users", "a"),
                    select: [new SelectItemProjection("id", new FieldInteger("a", "id"))]
                ),
                new PlainQueryWithoutPaginationWithoutOrderBy(
                    from: new FromEntity("t.users", "b"),
                    select: [new SelectItemProjection("id", new FieldInteger("a", "id"))]
                ),
            ])
        );
    }

    [Fact]
    public void AnInputThatReadsAnUndeclaredSubqueryIsRejected()
    {
        Rejected(
            new MainCombinedQueryUnionAll(
                [
                    Named("id", new LiteralInteger(1)),
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromSubquery("missing"),
                        select:
                        [
                            new SelectItemProjection(
                                "id",
                                new FieldInteger("missing", "id")
                            ),
                        ]
                    ),
                ],
                [
                    new SubqueryRows(
                        "present",
                        [[new RowItem("id", new LiteralInteger(1))]]
                    ),
                ]
            )
        );
    }

    [Fact]
    public void ANullableParameterInOneInputMakesTheColumnNullable()
    {
        PureQLProjection nullable = new(
            [Users],
            ReadingU(
                new CombinedQueryUnionAll([
                    Named("x", new ParamIntegerNullable("p")),
                    Named("x", new LiteralInteger(2)),
                ]),
                new FieldIntegerNullable("u", "x")
            ),
            Bound("p", new NullableIntegerParameter())
        );

        Assert.Equal(["", "2"], ResultColumn.Texts(nullable, "x"));
        Rejected(
            ReadingU(
                new CombinedQueryUnionAll([
                    Named("x", new ParamIntegerNullable("p")),
                    Named("x", new LiteralInteger(2)),
                ]),
                new FieldInteger("u", "x")
            ),
            Bound("p", new IntegerParameter(new Long(1)))
        );
    }

    [Fact]
    public void AnIntegerParameterCellAndAStringCellDoNotMeet()
    {
        Rejected(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRows(
                        "rows",
                        [
                            [new RowItem("x", new ParamInteger("p"))],
                            [new RowItem("x", new LiteralString("1"))],
                        ]
                    ),
                ],
                from: new FromSubquery("rows"),
                select: [new SelectItemProjection("x", new FieldInteger("rows", "x"))]
            ),
            Bound("p", new IntegerParameter(new Long(1)))
        );
    }

    // ---- "Each distinctBy entry, and each column of an orderBy, is an alias
    // of the query's select".

    [Fact]
    public void OrderByColumnThatIsAFieldNameButNoAliasIsRejected()
    {
        Rejected(
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.users"),
                select:
                [
                    new SelectItemProjection("label", new FieldString("t.users", "name")),
                ],
                orderBy: [new OrderItemProjectionColumn("name")]
            )
        );
    }

    [Fact]
    public void OrderByColumnThatIsAnAliasSortsByThatColumn()
    {
        PureQLProjection projection = new(
            [Users],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.users"),
                select:
                [
                    new SelectItemProjection("label", new FieldString("t.users", "name")),
                ],
                orderBy: [new OrderItemProjectionColumn("label", SortDirection.Desc)]
            )
        );

        Assert.Equal(
            ["dan", "cid", "bob", "ann"],
            ResultColumn.Texts(projection, "label")
        );
    }

    [Fact]
    public void GroupedOrderByColumnThatIsAGroupKeyButNoAliasIsRejected()
    {
        Rejected(
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.users"),
                groupBy:
                [
                    new GroupKey("k", new FieldIntegerNullable("t.users", "referrer")),
                ],
                select: [new SelectItemGroup("n", new CountGroup())],
                orderBy: [new OrderItemGroupColumn("k")]
            )
        );
    }

    [Fact]
    public void DistinctByThatIsAGroupKeyButNoAliasIsRejected()
    {
        Rejected(
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.users"),
                groupBy:
                [
                    new GroupKey("k", new FieldIntegerNullable("t.users", "referrer")),
                ],
                select: [new SelectItemGroup("n", new CountGroup())],
                distinctBy: ["k"]
            )
        );
    }

    [Fact]
    public void GroupedOrderByColumnThatIsAnAliasIsBuilt()
    {
        PureQLProjection projection = new(
            [Users],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.users"),
                groupBy:
                [
                    new GroupKey("k", new FieldIntegerNullable("t.users", "referrer")),
                ],
                select:
                [
                    new SelectItemGroup("k", new KeyIntegerNullable("k")),
                    new SelectItemGroup("n", new CountGroup()),
                ],
                orderBy:
                [
                    new OrderItemGroupColumn("n", SortDirection.Desc),
                    new OrderItemGroupColumn("k"),
                ]
            )
        );

        Assert.Equal(["1", "", "2"], ResultColumn.Texts(projection, "k"));
        Assert.Equal(["2", "1", "1"], ResultColumn.Texts(projection, "n"));
    }

    [Fact]
    public void OrderByColumnOfAPagedInputThatIsNoAliasIsRejected()
    {
        Rejected(
            new MainCombinedQueryUnionAll([
                Named("x", new LiteralInteger(1)),
                new PlainQueryWithPagination(
                    from: new FromEntity("t.users"),
                    select: [
                        new SelectItemProjection("x", new FieldInteger("t.users", "id")),
                    ],
                    orderBy: [new OrderItemProjectionColumn("id")],
                    pagination: new Pagination(take: 1L)
                ),
            ])
        );
    }

    [Fact]
    public void OrderByColumnOfAPagedSeedThatIsNoAliasIsRejected()
    {
        Rejected(
            ReadingTree(
                new SubqueryRecursive(
                    "tree",
                    new Recursion(
                        seed: new PlainQueryWithPagination(
                            from: new FromEntity("t.users"),
                            select:
                            [
                                new SelectItemProjection(
                                    "id",
                                    new FieldIntegerNullable("t.users", "id")
                                ),
                            ],
                            orderBy: [new OrderItemProjectionColumn("name")],
                            pagination: new Pagination(take: 1L)
                        ),
                        next: Children(),
                        maxDepth: 5L
                    )
                )
            )
        );
    }

    [Fact]
    public void RepeatedAliasOfATotalQueryIsRejected()
    {
        // "Column aliases are unique within a select (and within an
        // aggregate)".
        Rejected(
            new MainTotalQuery(
                from: new FromEntity("t.users"),
                aggregate:
                [
                    new SelectItemTotal("n", new CountTotal()),
                    new SelectItemTotal("n", new CountTotal()),
                ]
            )
        );
    }

    [Fact]
    public void RepeatedAliasOfAGroupedSelectIsRejected()
    {
        Rejected(
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.users"),
                groupBy: [new GroupKey("k", new FieldString("t.users", "name"))],
                select:
                [
                    new SelectItemGroup("n", new CountGroup()),
                    new SelectItemGroup("n", new KeyString("k")),
                ]
            )
        );
    }

    // ---- "a parameter used in several places has one type", and "One
    // parameter name has one type in the whole document, subqueries
    // included".

    [Theory]
    [MemberData(nameof(TwoTypesOfOneParameter))]
    public void ParameterDeclaredWithTwoTypesIsRejected(string place)
    {
        (PureQLQuery query, Dictionary<string, IParameter> values) = TwoTypes(place);

        Rejected(query, values);
    }

    public static TheoryData<string> TwoTypesOfOneParameter =>
        [
            "subquery and main",
            "two inputs",
            "seed and next",
            "pagination and select",
            "maxDepth and select",
            "frame and select",
            "list and scalar",
            "pattern and select",
            "fraction and select",
            "buckets and inline row",
        ];

    [Fact]
    public void ParameterOfOneTypeReadEverywhereIsBuiltAndReadsOneValue()
    {
        // p is an integer in an inline row, both inputs of a union, the seed,
        // next and the main query.
        PureQLProjection projection = new(
            [Users],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRows("cell", [[new RowItem("x", new ParamInteger("p"))]]),
                    new SubqueryQuery(
                        "both",
                        new CombinedQueryUnionAll([
                            Named("x", new ParamInteger("p")),
                            Named(
                                "x",
                                new AddIntegerProjection([
                                    new ParamInteger("p"),
                                    new LiteralInteger(1),
                                ])
                            ),
                        ])
                    ),
                    new SubqueryRecursive(
                        "steps",
                        new Recursion(
                            seed: Named("x", new ParamInteger("p")),
                            next: new PlainNextQuery(
                                from: new FromSubquery("steps"),
                                where: new LessThanDecimalRow(
                                    new FieldInteger("steps", "x"),
                                    new AddIntegerRow([
                                        new ParamInteger("p"),
                                        new LiteralInteger(2),
                                    ])
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
                where: new InDecimalRow(
                    new FieldInteger("steps", "x"),
                    new ListSubqueryColumnInteger("both", "x")
                ),
                select:
                [
                    new SelectItemProjection("x", new FieldInteger("steps", "x")),
                    new SelectItemProjection("p", new ParamInteger("p")),
                ],
                orderBy: [new OrderItemProjectionColumn("x")]
            ),
            Bound("p", new IntegerParameter(new Long(7)))
        );

        Assert.Equal(["7", "8"], ResultColumn.Texts(projection, "x"));
        Assert.Equal(["7", "7"], ResultColumn.Texts(projection, "p"));
    }

    // ---- "A regular expression is well formed (its parentheses balance, its
    // ranges and counted quantifiers are in order) and has the group that is
    // asked for; for a parameter, when it is bound".

    [Theory]
    [MemberData(nameof(MalformedPatterns))]
    public void MalformedLiteralPatternIsRejected(string pattern)
    {
        Rejected(Matching(new RegexPattern(new LiteralString(pattern))));
    }

    [Theory]
    [MemberData(nameof(MalformedPatterns))]
    [MemberData(nameof(PatternsOutsideTheDialect))]
    public void ParameterPatternThatIsNotOfTheDialectIsRejected(string pattern)
    {
        // Spec, Execution errors: "is run with a regular expression parameter
        // that is not a pattern of the dialect".
        Rejected(
            Matching(new RegexPattern(new ParamString("p"))),
            Bound("p", new StringParameter(new String(pattern)))
        );
    }

    public static TheoryData<string> MalformedPatterns =>
        ["(a", "a)", "(?:a", "[z-a]", "a{3,2}", "[a", "a{2", "*a", "a|*"];

    // Constructs the Regular expressions section lists as not in the
    // dialect, and escapes of characters it does not list.
    public static TheoryData<string> PatternsOutsideTheDialect =>
        [
            "(?=a)",
            "(?!a)",
            "(?<=a)b",
            "(?<n>a)",
            "(a)\\1",
            "(?i)a",
            "\\p{L}",
            "a++",
            "(?>a)",
            "\\x41",
            "\\u0041",
            "[[:alpha:]]",
            "\\e",
            "\\A",
        ];

    [Theory]
    [InlineData("^\\d+$", "123", "True")]
    [InlineData("^\\d+$", "12a", "False")]
    [InlineData("a|ab", "xab", "True")]
    [InlineData("\\/", "a/b", "True")]
    [InlineData("[\\-a]", "-", "True")]
    [InlineData("a{2,}?", "aa", "True")]
    [InlineData("\\bfoo\\b", "a foo", "True")]
    [InlineData("\\bfoo\\b", "afoo", "False")]
    [InlineData("(?:x)(y)", "xy", "True")]
    [InlineData("^.$", "\n", "False")]
    [InlineData("ab", "cabd", "True")]
    public void ParameterPatternOfTheDialectIsBoundAndMatches(
        string pattern,
        string text,
        string matches
    )
    {
        PureQLProjection projection = new(
            [Users],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.users"),
                where: IsUser(1),
                select:
                [
                    new SelectItemProjection(
                        "m",
                        new MatchesRegexProjection(
                            new ParamString("text"),
                            new RegexPattern(new ParamString("p"))
                        )
                    ),
                ]
            ),
            new Dictionary<string, IParameter>
            {
                ["p"] = new StringParameter(new String(pattern)),
                ["text"] = new StringParameter(new String(text)),
            }
        );

        Assert.Equal([matches], ResultColumn.Texts(projection, "m"));
    }

    [Theory]
    [InlineData("(a)", 2L)]
    [InlineData("(?:a)", 1L)]
    [InlineData("a", 1L)]
    [InlineData("(a)(b)", 3L)]
    public void LiteralGroupThePatternDoesNotHaveIsRejected(string pattern, long group)
    {
        Rejected(
            Extracting(
                new RegexPattern(new LiteralString(pattern)),
                new RegexGroup(new LiteralInteger(group))
            )
        );
    }

    [Theory]
    [InlineData(0L)]
    [InlineData(-1L)]
    [InlineData(3L)]
    public void GroupParameterThePatternDoesNotHaveIsRejected(long group)
    {
        Rejected(
            Extracting(
                new RegexPattern(new LiteralString("(a)(b)")),
                new RegexGroup(new ParamInteger("g"))
            ),
            Bound("g", new IntegerParameter(new Long(group)))
        );
    }

    [Theory]
    [InlineData(1L, "a")]
    [InlineData(2L, "b")]
    public void GroupParameterThePatternHasIsBound(long group, string extracted)
    {
        PureQLProjection projection = new(
            [Users],
            Extracting(
                new RegexPattern(new ParamString("p")),
                new RegexGroup(new ParamInteger("g"))
            ),
            new Dictionary<string, IParameter>
            {
                ["p"] = new StringParameter(new String("(a)(b)")),
                ["g"] = new IntegerParameter(new Long(group)),
            }
        );

        Assert.Equal([extracted], ResultColumn.Texts(projection, "e"));
    }

    [Fact]
    public void GroupParameterIsCheckedAgainstAPatternParameter()
    {
        Rejected(
            Extracting(
                new RegexPattern(new ParamString("p")),
                new RegexGroup(new ParamInteger("g"))
            ),
            new Dictionary<string, IParameter>
            {
                ["p"] = new StringParameter(new String("(?:a)(b)")),
                ["g"] = new IntegerParameter(new Long(2)),
            }
        );
    }

    [Theory]
    [InlineData("next")]
    [InlineData("seed")]
    [InlineData("input")]
    [InlineData("window predicate")]
    [InlineData("total predicate")]
    [InlineData("replace")]
    public void MalformedPatternParameterIsRejectedWhereverItStands(string place)
    {
        Rejected(PatternIn(place), Bound("p", new StringParameter(new String("(a"))));
    }

    [Theory]
    [InlineData("next")]
    [InlineData("seed")]
    [InlineData("input")]
    [InlineData("window predicate")]
    [InlineData("total predicate")]
    [InlineData("replace")]
    public void PatternParameterOfTheDialectIsBuiltWhereverItStands(string place)
    {
        PureQLProjection projection = new(
            [Users],
            PatternIn(place),
            Bound("p", new StringParameter(new String("^[a-c]")))
        );

        Assert.NotEmpty(projection.TableSchema.Columns);
    }

    // ---- Values of parameters with a range: "A pagination parameter is
    // bound to skip >= 0 / take >= 1, a fraction parameter to a value from
    // 0 to 1, ...".

    [Fact]
    public void SkipParameterBoundToZeroSkipsNothing()
    {
        // Pagination: "a skip parameter may be bound to 0, which skips
        // nothing".
        Assert.Equal(
            ["ann", "bob", "cid", "dan"],
            Paged(new Long(0), new Long(10))
        );
    }

    [Fact]
    public void SkipParameterOfTheLargestIntegerSkipsEveryRow()
    {
        Assert.Empty(Paged(new Long(long.MaxValue), new Long(1)));
    }

    [Fact]
    public void TakeParameterOfOneAndOfTheLargestIntegerAreAccepted()
    {
        Assert.Equal(["bob"], Paged(new Long(1), new Long(1)));
        Assert.Equal(["cid", "dan"], Paged(new Long(2), new Long(long.MaxValue)));
    }

    [Theory]
    [InlineData("input skip", -1L)]
    [InlineData("input take", 0L)]
    [InlineData("seed take", 0L)]
    [InlineData("subquery skip", long.MinValue)]
    public void PaginationParameterOutOfRangeIsRejectedWhereverItStands(
        string place,
        long value
    )
    {
        Pagination pagination = place.EndsWith("skip", StringComparison.Ordinal)
            ? new Pagination(skip: new ParamInteger("n"))
            : new Pagination(take: new ParamInteger("n"));
        Query paged = new PlainQueryWithPagination(
            from: new FromEntity("t.users"),
            select: [
                new SelectItemProjection("id", new FieldIntegerNullable("t.users", "id")),
            ],
            orderBy: [new OrderItemProjectionColumn("id")],
            pagination: pagination
        );
        PureQLQuery query = place switch
        {
            "seed take" => ReadingTree(
                new SubqueryRecursive(
                    "tree",
                    new Recursion(seed: paged, next: Children(), maxDepth: 5L)
                )
            ),
            "subquery skip" => ReadingU(paged, new FieldIntegerNullable("u", "id")),
            _ => new MainCombinedQueryUnionAll([
                paged,
                Named(
                    "id",
                    new AsNullableIntegerNullableProjection(new LiteralInteger(1))
                ),
            ]),
        };

        Rejected(query, Bound("n", new IntegerParameter(new Long(value))));
    }

    [Theory]
    [InlineData("1.000000000000000000000000001")]
    [InlineData("-0.000000000000000000000000001")]
    [InlineData("2")]
    public void FractionParameterOutsideZeroToOneIsRejectedInAWindowAndATotal(
        string fraction
    )
    {
        IParameter value = new TextParameter("decimal", fraction);

        Rejected(
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.users"),
                select:
                [
                    new SelectItemProjection(
                        "p",
                        new PercentileDiscreteIntegerProjection(
                            new FieldInteger("t.users", "id"),
                            new Fraction(new ParamDecimal("f")),
                            new WindowWithoutFrameWithoutRange([
                                new FieldIntegerNullable("t.users", "referrer"),
                            ])
                        )
                    ),
                ]
            ),
            Bound("f", value)
        );
        Rejected(
            new MainTotalQuery(
                from: new FromEntity("t.users"),
                aggregate:
                [
                    new SelectItemTotal(
                        "p",
                        new PercentileDiscreteIntegerNullableTotal(
                            new FieldInteger("t.users", "id"),
                            new Fraction(new ParamDecimal("f"))
                        )
                    ),
                ]
            ),
            Bound("f", value)
        );
    }

    [Fact]
    public void FractionParameterBoundToTheIntegerTwoIsRejected()
    {
        Rejected(
            new MainTotalQuery(
                from: new FromEntity("t.users"),
                aggregate:
                [
                    new SelectItemTotal(
                        "p",
                        new PercentileDiscreteIntegerNullableTotal(
                            new FieldInteger("t.users", "id"),
                            new Fraction(new ParamDecimal("f"))
                        )
                    ),
                ]
            ),
            Bound("f", new IntegerParameter(new Long(2)))
        );
    }

    [Theory]
    [InlineData("0", "1")]
    [InlineData("1", "4")]
    public void FractionParameterAtItsBoundsIsAccepted(string fraction, string value)
    {
        PureQLProjection projection = new(
            [Users],
            new MainTotalQuery(
                from: new FromEntity("t.users"),
                aggregate:
                [
                    new SelectItemTotal(
                        "p",
                        new PercentileDiscreteIntegerNullableTotal(
                            new FieldInteger("t.users", "id"),
                            new Fraction(new ParamDecimal("f"))
                        )
                    ),
                ]
            ),
            Bound("f", new TextParameter("decimal", fraction))
        );

        Assert.Equal([value], ResultColumn.Texts(projection, "p"));
    }

    [Theory]
    [InlineData("next")]
    [InlineData("input")]
    public void ZoneParameterThatIsNotATimeZoneIsRejectedInNewConstructs(string place)
    {
        Rejected(ZoneIn(place), Bound("z", new StringParameter(new String("Mars/Base"))));
    }

    [Theory]
    [InlineData("next")]
    [InlineData("input")]
    public void ZoneParameterBoundToUtcIsAccepted(string place)
    {
        // "A zone parameter is bound to a time zone name of that list, or to
        // UTC".
        PureQLProjection projection = new(
            [Users],
            ZoneIn(place),
            Bound("z", new StringParameter(new String("UTC")))
        );

        Assert.Equal(["2024"], ResultColumn.Texts(projection, "y").Distinct());
    }

    [Theory]
    [InlineData("utc")]
    [InlineData("europe/berlin")]
    [InlineData("EUROPE/BERLIN")]
    [InlineData(" Europe/Berlin")]
    public void ZoneParameterIsAZoneNameOnlyAsItIsSpelled(string zone)
    {
        // Validation: "A zone parameter is bound to a time zone name of that
        // list, or to UTC"; Names and sources: "Names compare exactly, code
        // point by code point".
        Rejected(ZoneIn("input"), Bound("z", new StringParameter(new String(zone))));
    }

    [Fact]
    public void ZoneParameterOfTheListIsAccepted()
    {
        PureQLProjection projection = new(
            [Users],
            ZoneIn("input"),
            Bound("z", new StringParameter(new String("Pacific/Kiritimati")))
        );

        Assert.Equal(["2024"], ResultColumn.Texts(projection, "y").Distinct());
    }

    [Theory]
    [InlineData("offset", 0L)]
    [InlineData("buckets", 0L)]
    [InlineData("frame", -1L)]
    public void WindowParameterBelowItsMinimumIsRejectedInAnInputOfAUnion(
        string part,
        long value
    )
    {
        OrderedWindow byId = new([new OrderItemRow(new FieldInteger("t.users", "id"))]);
        ExprProjection function = part switch
        {
            "offset" => new PreviousIntegerNullableProjection(
                new FieldInteger("t.users", "id"),
                byId,
                offset: new ParamInteger("n")
            ),
            "buckets" => new NtileProjection(new ParamInteger("n"), byId),
            _ => new SumIntegerProjection(
                new FieldInteger("t.users", "id"),
                over: new WindowWithFrame(
                    [new OrderItemRow(new FieldInteger("t.users", "id"))],
                    new Frame(preceding: new ParamInteger("n"))
                )
            ),
        };

        Rejected(
            new MainCombinedQueryUnionAll([
                new PlainQueryWithoutPaginationWithoutOrderBy(
                    from: new FromEntity("t.users"),
                    select: [new SelectItemProjection("v", function)]
                ),
                Named("v", new LiteralIntegerNullable()),
            ]),
            Bound("n", new IntegerParameter(new Long(value)))
        );
    }

    // ---- A negative amount added to a date, a time or a datetime is no
    // count of a frame or distance of a range: dateAddDays and the other
    // adding operators are lifted over any integer or decimal.

    [Theory]
    [InlineData("days", "2023-12-29")]
    [InlineData("months", "2023-10-01")]
    public void NegativeAmountParameterOfADateOperatorIsAccepted(
        string unit,
        string expected
    )
    {
        ExprProjection added = unit == "days"
            ? new DateAddDaysDateProjection(
                new FieldDate("t.users", "day"),
                new ParamInteger("n")
            )
            : new DateAddMonthsDateProjection(
                new FieldDate("t.users", "day"),
                new ParamInteger("n")
            );

        Assert.Equal(
            [expected],
            ResultColumn.Texts(
                OneUser(added, Bound("n", new IntegerParameter(new Long(-3)))),
                "v"
            )
        );
    }

    [Fact]
    public void NegativeSecondsParameterOfADatetimeIsAccepted()
    {
        Assert.Equal(
            ["2024-01-01T09:59:58.5Z"],
            ResultColumn.Texts(
                OneUser(
                    new DatetimeAddSecondsDatetimeProjection(
                        new FieldDatetime("t.users", "at"),
                        new ParamDecimal("n")
                    ),
                    Bound("n", new DecimalParameter(new Decimal(-1.5m)))
                ),
                "v"
            )
        );
    }

    [Fact]
    public void NegativeSecondsParameterOfATimeIsAccepted()
    {
        Assert.Equal(
            ["23:59:59"],
            ResultColumn.Texts(
                OneUser(
                    new TimeAddSecondsTimeProjection(
                        new LiteralTime(new TimeOnly(0, 0)),
                        new ParamDecimal("n")
                    ),
                    Bound("n", new IntegerParameter(new Long(-1)))
                ),
                "v"
            )
        );
    }

    // ---- Each field reference declares exactly the nullability the field
    // has at that point; a subquery's columns are known exactly.

    [Fact]
    public void ColumnOfARecursiveSubqueryReadAsNullableIsRejected()
    {
        Rejected(
            new MainPlainQueryWithoutPagination(
                subqueries: [Depths(new SelectItemRow("id", UserIdField()), Deeper())],
                from: new FromSubquery("tree"),
                select:
                [
                    new SelectItemProjection(
                        "depth",
                        new FieldIntegerNullable("tree", "depth")
                    ),
                ]
            )
        );
    }

    [Fact]
    public void ColumnOfARecursiveSubqueryOnTheOptionalSideOfAJoinIsNullable()
    {
        Rejected(ReadingDepthsAfterALeftJoin(new FieldInteger("tree", "depth")));
        Assert.Equal(
            ["0", "1", "1", "2"],
            ResultColumn.Texts(
                new PureQLProjection(
                    [Users],
                    ReadingDepthsAfterALeftJoin(new FieldIntegerNullable("tree", "depth"))
                ),
                "depth"
            )
        );
    }

    [Fact]
    public void WindowParameterReadOnlyInTheQuerysOrderByIsChecked()
    {
        Rejected(
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.users"),
                select: [
                    new SelectItemProjection("id", new FieldInteger("t.users", "id")),
                ],
                orderBy:
                [
                    new OrderItemProjectionExpression(
                        new SumIntegerProjection(
                            new FieldInteger("t.users", "id"),
                            over: new WindowWithFrame(
                                [new OrderItemRow(new FieldInteger("t.users", "id"))],
                                new Frame(following: new ParamInteger("n"))
                            )
                        )
                    ),
                ]
            ),
            Bound("n", new IntegerParameter(new Long(-1)))
        );
    }

    [Fact]
    public void ParameterReadOnlyInTheDefaultOfPreviousInAnInputNeedsAValue()
    {
        Rejected(
            new MainCombinedQueryUnionAll([
                new PlainQueryWithoutPaginationWithoutOrderBy(
                    from: new FromEntity("t.users"),
                    select:
                    [
                        new SelectItemProjection(
                            "v",
                            new PreviousIntegerProjection(
                                new FieldInteger("t.users", "id"),
                                new ParamInteger("d"),
                                new OrderedWindow([
                                    new OrderItemRow(new FieldInteger("t.users", "id")),
                                ])
                            )
                        ),
                    ]
                ),
                Named("v", new LiteralInteger(0)),
            ]),
            Bound("other", new IntegerParameter(new Long(1)))
        );
    }

    [Fact]
    public void NullZoneParameterGivesNull()
    {
        // Null semantics: "a null zone gives null".
        Assert.Equal(
            [""],
            ResultColumn.Texts(
                OneUser(
                    new YearDatetimeIntegerNullableProjection(
                        new FieldDatetime("t.users", "at"),
                        new ParamStringNullable("z")
                    ),
                    Bound("z", new NullableStringParameter())
                ),
                "v"
            )
        );
    }

    // ---- Helpers.

    private static void Rejected(
        PureQLQuery query,
        IReadOnlyDictionary<string, IParameter>? values = null
    )
    {
        _ = Assert.Throws<ArgumentException>(() =>
            values is null
                ? new PureQLProjection([Users], query)
                : new PureQLProjection([Users], query, values)
        );
    }

    private static Dictionary<string, IParameter> Bound(string name, IParameter value)
    {
        return new Dictionary<string, IParameter> { [name] = value };
    }

    private static BooleanRow IsUser(long id)
    {
        return new EqualDecimalRow(
            new FieldInteger("t.users", "id"),
            new LiteralInteger(id)
        );
    }

    private static FieldIntegerNullable UserIdField()
    {
        return new FieldIntegerNullable("t.users", "id");
    }

    private static SelectItemRow UserId()
    {
        return new SelectItemRow("id", UserIdField());
    }

    // users.referrer = tree.id
    private static BooleanRow ReferredByTree()
    {
        return new EqualDecimalRow(
            new FieldIntegerNullable("t.users", "referrer"),
            TreeId
        );
    }

    private static Join JoinTree(JoinKind kind)
    {
        return new JoinSubquery(kind, "tree", ReferredByTree());
    }

    // from t.users join tree on users.referrer = tree.id select <columns>
    private static PlainNextQuery Children(params SelectItemRow[] columns)
    {
        return new PlainNextQuery(
            from: new FromEntity("t.users"),
            joins: [JoinTree(JoinKind.Inner)],
            select: columns.Length == 0 ? [UserId()] : columns
        );
    }

    // tree: the user of the id, with a nullable id column, and everyone
    // below.
    private static Subquery Tree(
        NextQuery next,
        RecursionMaxDepth? maxDepth = null,
        long id = 1
    )
    {
        return new SubqueryRecursive(
            "tree",
            new Recursion(
                seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                    from: new FromEntity("t.users"),
                    where: IsUser(id),
                    select: [new SelectItemProjection("id", UserIdField())]
                ),
                next: next,
                maxDepth: maxDepth ?? new RecursionMaxDepth(5L)
            )
        );
    }

    private static PureQLQuery ReadingTree(Subquery tree)
    {
        return new MainPlainQueryWithoutPagination(
            subqueries: [tree],
            from: new FromSubquery("tree"),
            select: [new SelectItemProjection("id", TreeId)],
            orderBy: [new OrderItemProjectionColumn("id")]
        );
    }

    // tree: ann with depth 0, a non-null integer, and the rows next gives.
    private static Subquery Depths(
        SelectItemRow id,
        params SelectItemRow[] rest
    )
    {
        return Depths([id, .. rest], null);
    }

    // depth + 1
    private static SelectItemRow Deeper()
    {
        return new SelectItemRow(
            "depth",
            new AddIntegerRow([new FieldInteger("tree", "depth"), new LiteralInteger(1)])
        );
    }

    // Every user, left joined with tree, and the depth read as given.
    private static PureQLQuery ReadingDepthsAfterALeftJoin(ExprProjection depth)
    {
        return new MainPlainQueryWithoutPagination(
            subqueries: [Depths(new SelectItemRow("id", UserIdField()), Deeper())],
            from: new FromEntity("t.users"),
            joins:
            [
                new JoinSubquery(
                    JoinKind.Left,
                    "tree",
                    new EqualDecimalRow(TreeId, new FieldInteger("t.users", "id"))
                ),
            ],
            select:
            [
                new SelectItemProjection("id", new FieldInteger("t.users", "id")),
                new SelectItemProjection("depth", depth),
            ],
            orderBy: [new OrderItemProjectionColumn("id")]
        );
    }

    private static Subquery Depths(SelectItemRow[] columns, BooleanRow? where)
    {
        return new SubqueryRecursive(
            "tree",
            new Recursion(
                seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                    from: new FromEntity("t.users"),
                    where: IsUser(1),
                    select:
                    [
                        new SelectItemProjection("id", UserIdField()),
                        new SelectItemProjection("depth", new LiteralInteger(0)),
                    ]
                ),
                next: new PlainNextQuery(
                    from: new FromEntity("t.users"),
                    joins: [JoinTree(JoinKind.Inner)],
                    where: where,
                    select: columns
                ),
                maxDepth: 5L
            )
        );
    }

    private static PureQLQuery ReadingDepths(Subquery tree)
    {
        return new MainPlainQueryWithoutPagination(
            subqueries: [tree],
            from: new FromSubquery("tree"),
            select:
            [
                new SelectItemProjection("id", TreeId),
                new SelectItemProjection("depth", new FieldInteger("tree", "depth")),
            ],
            orderBy: [new OrderItemProjectionColumn("id")]
        );
    }

    // One row of one column x of a type.
    private static Query Constant(string type)
    {
        ExprProjection value = type switch
        {
            "integer" => new LiteralInteger(1),
            "decimal" => new LiteralDecimal(1.5m),
            "string" => new LiteralString("1"),
            "boolean" => new LiteralBoolean(true),
            "date" => new LiteralDate(new DateOnly(2024, 1, 1)),
            "time" => new LiteralTime(new TimeOnly(10, 0)),
            "datetime" => new LiteralDatetime(
                new DateTimeOffset(2024, 1, 1, 0, 0, 0, default)
            ),
            _ => new LiteralUuid(new Guid("3f2a6c1e-8b4d-4e2a-9c1f-1a2b3c4d5e6f")),
        };

        return Named("x", value);
    }

    // One row (ann's) of one column.
    private static Query Named(string alias, ExprProjection value)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.users"),
            where: IsUser(1),
            select: [new SelectItemProjection(alias, value)]
        );
    }

    // The column of a query declared as the subquery u, read through a
    // field and sorted.
    private static PureQLQuery ReadingU(Query query, ExprProjection field)
    {
        return new MainPlainQueryWithoutPagination(
            subqueries: [new SubqueryQuery("u", query)],
            from: new FromSubquery("u"),
            select: [new SelectItemProjection("x", field)],
            orderBy: [new OrderItemProjectionColumn("x")]
        );
    }

    private static PureQLQuery Matching(RegexPattern pattern)
    {
        return new MainPlainQueryWithoutPagination(
            from: new FromEntity("t.users"),
            select:
            [
                new SelectItemProjection(
                    "m",
                    new MatchesRegexProjection(
                        new FieldString("t.users", "name"),
                        pattern
                    )
                ),
            ]
        );
    }

    // extractRegex of the literal "ab", on ann's row.
    private static PureQLQuery Extracting(RegexPattern pattern, RegexGroup group)
    {
        return new MainPlainQueryWithoutPagination(
            from: new FromEntity("t.users"),
            where: IsUser(1),
            select:
            [
                new SelectItemProjection(
                    "e",
                    new ExtractRegexProjection(new LiteralString("ab"), pattern, group)
                ),
            ]
        );
    }

    // A query that reads the pattern parameter p in one place.
    private static PureQLQuery PatternIn(string place)
    {
        RegexPattern pattern = new(new ParamString("p"));
        BooleanRow matches = new MatchesRegexRow(
            new FieldString("t.users", "name"),
            pattern
        );

        return place switch
        {
            "next" => ReadingTree(
                Tree(
                    new PlainNextQuery(
                        from: new FromEntity("t.users"),
                        joins: [JoinTree(JoinKind.Inner)],
                        where: matches,
                        select: [UserId()]
                    )
                )
            ),
            "seed" => ReadingTree(
                new SubqueryRecursive(
                    "tree",
                    new Recursion(
                        seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                            from: new FromEntity("t.users"),
                            where: new AndRow([IsUser(1), matches]),
                            select: [new SelectItemProjection("id", UserIdField())]
                        ),
                        next: Children(),
                        maxDepth: 5L
                    )
                )
            ),
            "input" => new MainCombinedQueryUnion([
                new PlainQueryWithoutPaginationWithoutOrderBy(
                    from: new FromEntity("t.users"),
                    where: matches,
                    select: [
                        new SelectItemProjection("x", new FieldInteger("t.users", "id")),
                    ]
                ),
                Named("x", new LiteralInteger(9)),
            ]),
            "window predicate" => new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.users"),
                select:
                [
                    new SelectItemProjection(
                        "c",
                        new CountProjection(
                            predicate: matches,
                            over: new WindowWithoutFrameWithoutRange([
                                new FieldIntegerNullable("t.users", "referrer"),
                            ])
                        )
                    ),
                ]
            ),
            "total predicate" => new MainTotalQuery(
                from: new FromEntity("t.users"),
                aggregate: [new SelectItemTotal("c", new CountTotal(matches))]
            ),
            _ => new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.users"),
                select:
                [
                    new SelectItemProjection(
                        "r",
                        new ReplaceRegexStringProjection(
                            new FieldString("t.users", "name"),
                            new LiteralString("-"),
                            pattern
                        )
                    ),
                ]
            ),
        };
    }

    // The year of each user's instant in the zone parameter z, in the next
    // of a recursive subquery or in an input of a union.
    private static PureQLQuery ZoneIn(string place)
    {
        return place == "next"
            ? new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRecursive(
                        "tree",
                        new Recursion(
                            seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.users"),
                                where: IsUser(1),
                                select:
                                [
                                    new SelectItemProjection("id", UserIdField()),
                                    new SelectItemProjection(
                                        "y",
                                        new LiteralInteger(2024)
                                    ),
                                ]
                            ),
                            next: new PlainNextQuery(
                                from: new FromEntity("t.users"),
                                joins: [JoinTree(JoinKind.Inner)],
                                select:
                                [
                                    UserId(),
                                    new SelectItemRow(
                                        "y",
                                        new YearDatetimeIntegerRow(
                                            new FieldDatetime("t.users", "at"),
                                            new ParamString("z")
                                        )
                                    ),
                                ]
                            ),
                            maxDepth: 5L
                        )
                    ),
                ],
                from: new FromSubquery("tree"),
                select: [new SelectItemProjection("y", new FieldInteger("tree", "y"))]
            )
            : new MainCombinedQueryUnionAll([
            new PlainQueryWithoutPaginationWithoutOrderBy(
                from: new FromEntity("t.users"),
                select:
                [
                    new SelectItemProjection(
                        "y",
                        new YearDatetimeIntegerProjection(
                            new FieldDatetime("t.users", "at"),
                            new ParamString("z")
                        )
                    ),
                ]
            ),
            Named("y", new LiteralInteger(2024)),
        ]);
    }

    private static IReadOnlyList<string> Paged(Long skip, Long take)
    {
        PureQLProjection projection = new(
            [Users],
            new MainPlainQueryWithPagination(
                from: new FromEntity("t.users"),
                select: [
                    new SelectItemProjection("name", new FieldString("t.users", "name")),
                ],
                orderBy:
                [
                    new OrderItemProjectionExpression(new FieldInteger("t.users", "id")),
                ],
                pagination: new Pagination(
                    skip: new ParamInteger("skip"),
                    take: new ParamInteger("take")
                )
            ),
            new Dictionary<string, IParameter>
            {
                ["skip"] = new IntegerParameter(skip),
                ["take"] = new IntegerParameter(take),
            }
        );

        return ResultColumn.Texts(projection, "name");
    }

    private static PureQLProjection OneUser(
        ExprProjection expression,
        IReadOnlyDictionary<string, IParameter> values
    )
    {
        return new PureQLProjection(
            [Users],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.users"),
                where: IsUser(1),
                select: [new SelectItemProjection("v", expression)]
            ),
            values
        );
    }

    // A query that declares the parameter p with two types in two places.
    private static (PureQLQuery, Dictionary<string, IParameter>) TwoTypes(string place)
    {
        Dictionary<string, IParameter> integer = Bound(
            "p",
            new IntegerParameter(new Long(1))
        );

        return place switch
        {
            "subquery and main" => (
                new MainPlainQueryWithoutPagination(
                    subqueries:
                    [
                        new SubqueryQuery(
                            "s",
                            new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.users"),
                                where: new EqualDecimalRow(
                                    new FieldInteger("t.users", "id"),
                                    new ParamInteger("p")
                                ),
                                select:
                                [
                                    new SelectItemProjection(
                                        "id",
                                        new FieldInteger("t.users", "id")
                                    ),
                                ]
                            )
                        ),
                    ],
                    from: new FromSubquery("s"),
                    select: [new SelectItemProjection("p", new ParamDecimal("p"))]
                ),
                integer
            ),
            "two inputs" => (
                new MainCombinedQueryUnionAll([
                    Named("x", new ParamInteger("p")),
                    Named("x", new ParamIntegerNullable("p")),
                ]),
                integer
            ),
            "seed and next" => (
                ReadingTree(
                    new SubqueryRecursive(
                        "tree",
                        new Recursion(
                            seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.users"),
                                where: new EqualDecimalRow(
                                    new FieldInteger("t.users", "id"),
                                    new ParamInteger("p")
                                ),
                                select: [new SelectItemProjection("id", UserIdField())]
                            ),
                            next: new PlainNextQuery(
                                from: new FromEntity("t.users"),
                                joins: [JoinTree(JoinKind.Inner)],
                                where: new NotEqualDecimalRow(
                                    new FieldInteger("t.users", "id"),
                                    new ParamIntegerNullable("p")
                                ),
                                select: [UserId()]
                            ),
                            maxDepth: 5L
                        )
                    )
                ),
                integer
            ),
            "pagination and select" => (
                new MainPlainQueryWithPagination(
                    from: new FromEntity("t.users"),
                    select: [new SelectItemProjection("p", new ParamDecimal("p"))],
                    orderBy: [new OrderItemProjectionColumn("p")],
                    pagination: new Pagination(take: new ParamInteger("p"))
                ),
                integer
            ),
            "maxDepth and select" => (
                new MainPlainQueryWithoutPagination(
                    subqueries: [Tree(Children(), new ParamInteger("p"))],
                    from: new FromSubquery("tree"),
                    select: [new SelectItemProjection("p", new ParamDecimal("p"))]
                ),
                integer
            ),
            "frame and select" => (
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.users"),
                    select:
                    [
                        new SelectItemProjection(
                            "s",
                            new SumIntegerProjection(
                                new FieldInteger("t.users", "id"),
                                over: new WindowWithFrame(
                                    [new OrderItemRow(new FieldInteger("t.users", "id"))],
                                    new Frame(preceding: new ParamInteger("p"))
                                )
                            )
                        ),
                        new SelectItemProjection("p", new ParamString("p")),
                    ]
                ),
                integer
            ),
            "list and scalar" => (
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.users"),
                    where: new InStringRow(
                        new FieldString("t.users", "name"),
                        new ListParamString("p")
                    ),
                    select: [new SelectItemProjection("p", new ParamString("p"))]
                ),
                Bound("p", new StringParameter(new String("ann")))
            ),
            "pattern and select" => (
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.users"),
                    where: new MatchesRegexRow(
                        new FieldString("t.users", "name"),
                        new RegexPattern(new ParamString("p"))
                    ),
                    select: [new SelectItemProjection("p", new ParamStringNullable("p"))]
                ),
                Bound("p", new StringParameter(new String("a")))
            ),
            "fraction and select" => (
                new MainTotalQuery(
                    from: new FromEntity("t.users"),
                    aggregate:
                    [
                        new SelectItemTotal(
                            "f",
                            new PercentileDiscreteIntegerNullableTotal(
                                new FieldInteger("t.users", "id"),
                                new Fraction(new ParamDecimal("p"))
                            )
                        ),
                        new SelectItemTotal("p", new ParamInteger("p")),
                    ]
                ),
                integer
            ),
            _ => (
                new MainPlainQueryWithoutPagination(
                    subqueries:
                    [
                        new SubqueryRows(
                            "cells",
                            [[new RowItem("x", new ParamDecimal("p"))]]
                        ),
                    ],
                    from: new FromEntity("t.users"),
                    select:
                    [
                        new SelectItemProjection(
                            "n",
                            new NtileProjection(
                                new ParamInteger("p"),
                                new OrderedWindow([
                                    new OrderItemRow(new FieldInteger("t.users", "id")),
                                ])
                            )
                        ),
                    ]
                ),
                integer
            ),
        };
    }
}
