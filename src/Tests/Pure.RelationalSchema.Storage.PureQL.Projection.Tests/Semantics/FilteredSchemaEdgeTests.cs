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

// A row a filtered schema dataset's condition drops is a row its table does
// not have, also for the constructs of specification 3: both inputs of a set
// operation, the seed and the next of a recursive subquery, the rows a window
// function reads, an outer join in an input, and inline rows joined with
// it. Each result is the one the query gives over the tables holding only the
// rows the conditions keep. See Staff for the rows.
public sealed class FilteredSchemaEdgeTests
{
    // 1 is the root; 2 and 4 are below it, 3 is below 2.
    private static readonly InlineTable TreeTable = new(
        "t",
        "tree",
        ("id", new LongColumnType()),
        ("parent", new LongColumnType())
    );

    [Fact]
    public void BothInputsOfAUnionAllReadOnlyTheRowsTheConditionKeeps()
    {
        PureQLProjection projection = new(
            [Red()],
            new MainCombinedQueryUnionAll([PeopleNames(null), PeopleNames(null)])
        );

        Assert.Equal(["Ann", "Ann", "Dee", "Dee"], Staff.Lines(projection).Order());
    }

    [Fact]
    public void ExceptOfTheTableAndItsRedRowsIsEmptyWhenOnlyRedRowsExist()
    {
        PureQLQuery query = new MainCombinedQueryExcept([
            PeopleNames(null),
            PeopleNames(Staff.TeamIs("red", "p")),
        ]);

        Assert.Empty(Staff.Lines(new PureQLProjection([Red()], query)));
        Assert.Equal(
            ["Bob", "Cy"],
            Staff.Lines(new PureQLProjection([Staff.Schema()], query)).Order()
        );
    }

    [Fact]
    public void IntersectWithInlineRowsHoldsOnlyTheNamesTheConditionKeeps()
    {
        PureQLProjection projection = new(
            [Red()],
            new MainCombinedQueryIntersect(
                [
                    PeopleNames(null),
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
                ],
                [
                    new SubqueryRows(
                        "wanted",
                        [
                            [new RowItem("name", new LiteralString("Ann"))],
                            [new RowItem("name", new LiteralString("Bob"))],
                        ]
                    ),
                ]
            )
        );

        Assert.Equal(["Ann"], Staff.Lines(projection));
    }

    [Fact]
    public void TotalInputsCountOnlyTheRowsTheConditionsKeep()
    {
        PureQLProjection projection = new(
            [
                Staff.Filtered(
                    Staff.Schema(),
                    ("people", Staff.RedTeam),
                    ("orders", Staff.BigOrders)
                ),
            ],
            new MainCombinedQueryUnionAll([
                Counted("t.people", "people"),
                Counted("t.orders", "orders"),
            ])
        );

        Assert.Equal(["orders|3", "people|2"], Staff.Lines(projection).Order());
    }

    [Fact]
    public void FullJoinInBothInputsOfAUnionSeesOnlyTheRowsTheConditionsKeep()
    {
        // As the full join alone gives Ann|5, Dee|9 and |7; union keeps each
        // once.
        PureQLProjection projection = new(
            [
                Staff.Filtered(
                    Staff.Schema(),
                    ("people", Staff.RedTeam),
                    ("orders", Staff.BigOrders)
                ),
            ],
            new MainCombinedQueryUnion([PeopleWithOrders(), PeopleWithOrders()])
        );

        Assert.Equal(
            ["Ann|5", "Dee|9", "|7"],
            Staff.Lines(projection).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void WindowFunctionsNumberAndCountOnlyTheRowsTheConditionKeeps()
    {
        OrderedWindow byId = new([new OrderItemRow(new FieldInteger("p", "id"))]);
        PureQLProjection projection = new(
            [Red()],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.people", "p"),
                select:
                [
                    new SelectItemProjection(
                        "name",
                        new FieldStringNullable("p", "name")
                    ),
                    new SelectItemProjection("n", new RowNumberProjection(byId)),
                    new SelectItemProjection("count", new CountProjection()),
                    new SelectItemProjection(
                        "previous",
                        new PreviousStringNullableProjection(
                            new FieldStringNullable("p", "name"),
                            byId
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionColumn("n")]
            )
        );

        Assert.Equal(["Ann|1|2|", "Dee|2|2|Ann"], Staff.Lines(projection));
    }

    [Fact]
    public void WindowOverALeftJoinSumsOnlyTheOrdersTheConditionKeeps()
    {
        // Ann's orders 5 and 1: only 5 is big, so her partition sums 5;
        // the total over every row is 5 + 7 + 9, Cy's row without an order
        // adding nothing.
        PureQLProjection projection = new(
            [Staff.Filtered(Staff.Schema(), ("orders", Staff.BigOrders))],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.people", "p"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Left,
                        "t.orders",
                        new EqualDecimalRow(
                            new FieldIntegerNullable("o", "person"),
                            new FieldIntegerNullable("p", "id")
                        ),
                        "o"
                    ),
                ],
                select:
                [
                    new SelectItemProjection(
                        "name",
                        new FieldStringNullable("p", "name")
                    ),
                    new SelectItemProjection(
                        "mine",
                        new SumIntegerProjection(
                            new FieldIntegerNullable("o", "total"),
                            over: new WindowWithoutFrameWithoutRange([
                                new FieldIntegerNullable("p", "id"),
                            ])
                        )
                    ),
                    new SelectItemProjection(
                        "all",
                        new SumIntegerProjection(new FieldIntegerNullable("o", "total"))
                    ),
                ],
                orderBy: [new OrderItemProjectionColumn("name")]
            )
        );

        Assert.Equal(
            ["Ann|5|21", "Bob|7|21", "Cy|0|21", "Dee|9|21"],
            Staff.Lines(projection)
        );
    }

    [Fact]
    public void RecursiveSubqueryDoesNotWalkThroughARowTheConditionDrops()
    {
        IStoredSchemaDataSet tree = Tree();
        PureQLQuery query = Descendants(1);

        Assert.Equal(
            ["1", "2", "3", "4"],
            Staff.Lines(new PureQLProjection([tree], query))
        );
        Assert.Equal(
            ["1", "4"],
            Staff.Lines(
                new PureQLProjection(
                    [Staff.Filtered(tree, ("tree", IdIsNot(2)))],
                    query
                )
            )
        );
    }

    [Fact]
    public void RecursiveSubqueryWhoseSeedRowTheConditionDropsHasNoRows()
    {
        Assert.Empty(
            Staff.Lines(
                new PureQLProjection(
                    [Staff.Filtered(Tree(), ("tree", IdIsNot(1)))],
                    Descendants(1)
                )
            )
        );
    }

    [Fact]
    public void InlineRowsJoinedWithAFilteredTableMeetOnlyItsKeptRows()
    {
        PureQLProjection projection = new(
            [Red()],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRows(
                        "ranks",
                        [
                            [
                                new RowItem("name", new LiteralString("Bob")),
                                new RowItem("rank", new LiteralInteger(1)),
                            ],
                            [
                                new RowItem("name", new LiteralString("Dee")),
                                new RowItem("rank", new LiteralInteger(2)),
                            ],
                        ]
                    ),
                ],
                from: new FromSubquery("ranks", "r"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Left,
                        "t.people",
                        new EqualStringRow(
                            new FieldStringNullable("p", "name"),
                            new FieldString("r", "name")
                        ),
                        "p"
                    ),
                ],
                select:
                [
                    new SelectItemProjection("name", new FieldString("r", "name")),
                    new SelectItemProjection("id", new FieldIntegerNullable("p", "id")),
                ],
                orderBy: [new OrderItemProjectionColumn("name")]
            )
        );

        Assert.Equal(["Bob|", "Dee|4"], Staff.Lines(projection));
    }

    [Fact]
    public void ListSubqueryOverAFilteredTableIsReadInTheNextOfARecursion()
    {
        // The tree is walked only through the ids the people table keeps:
        // 1 and 4 under the red team.
        IStoredSchemaDataSet schema = Staff.Schema(TreeData(), Staff.People());
        PureQLProjection projection = new(
            [Staff.Filtered(schema, ("people", Staff.RedTeam))],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "kept",
                        new PlainQueryWithoutPaginationWithoutOrderBy(
                            from: new FromEntity("t.people"),
                            select:
                            [
                                new SelectItemProjection(
                                    "id",
                                    new FieldInteger("t.people", "id")
                                ),
                            ]
                        )
                    ),
                    Walk(
                        new InDecimalRow(
                            new FieldInteger("t.tree", "id"),
                            new ListSubqueryColumnInteger("kept", "id")
                        )
                    ),
                ],
                from: new FromSubquery("walk"),
                select: [new SelectItemProjection("id", new FieldInteger("walk", "id"))],
                orderBy: [new OrderItemProjectionColumn("id")]
            )
        );

        Assert.Equal(["1", "4"], Staff.Lines(projection));
    }

    [Fact]
    public void ParameterOfTheQueryIsReadOverTheKeptRows()
    {
        PureQLProjection projection = new(
            [Red()],
            new MainCombinedQueryUnionAll([
                PeopleNames(
                    new GreaterThanDecimalRow(
                        new FieldIntegerNullable("p", "id"),
                        new ParamInteger("min")
                    )
                ),
                PeopleNames(
                    new LessThanDecimalRow(
                        new FieldIntegerNullable("p", "id"),
                        new ParamInteger("min")
                    )
                ),
            ]),
            new Dictionary<string, IParameter>
            {
                ["min"] = new IntegerParameter(new Long(2)),
            }
        );

        Assert.Equal(["Ann", "Dee"], Staff.Lines(projection).Order());
    }

    [Fact]
    public void UnionOverFilteredProvidedTablesGoesWholeToTheProvider()
    {
        RecordingProvider provider = new();
        IStoredSchemaDataSet schema = ProvidedTable.Schema(Staff.Schema(), provider);
        PureQLProjection projection = new(
            [
                Staff.Filtered(
                    schema,
                    ("people", Staff.RedTeam),
                    ("orders", Staff.BigOrders)
                ),
            ],
            new MainCombinedQueryUnion([PeopleWithOrders(), PeopleWithOrders()])
        );

        Assert.Equal(
            ["Ann|5", "Dee|9", "|7"],
            Staff.Lines(projection).Order(StringComparer.Ordinal)
        );
        Assert.Equal(QueryExecution.Translated, projection.Execution);
        _ = Assert.Single(provider.Queries);
        Assert.All(schema.Values, table => Assert.Equal(0, ((ProvidedTable)table).Reads));
    }

    [Theory]
    [InlineData(Refusal.OnCreation)]
    [InlineData(Refusal.OnFirstValue)]
    public void RecursionOverAFilteredTableTheProviderRefusesRunsInMemoryOverTheKeptRows(
        Refusal refusal
    )
    {
        IStoredSchemaDataSet schema = ProvidedTable.Schema(
            TreeData(),
            new RecordingProvider(refusal)
        );
        PureQLProjection projection = new(
            [Staff.Filtered(schema, ("tree", IdIsNot(2)))],
            Descendants(1)
        );

        Assert.Equal(["1", "4"], Staff.Lines(projection));
        Assert.Equal(QueryExecution.InMemory, projection.Execution);
        _ = Assert.IsType<NotSupportedException>(projection.FallbackReason);
    }

    [Fact]
    public void RecursionOverAFilteredProvidedTableIsTranslatedWithTheSameRows()
    {
        RecordingProvider provider = new();
        IStoredSchemaDataSet schema = ProvidedTable.Schema(TreeData(), provider);
        PureQLProjection projection = new(
            [Staff.Filtered(schema, ("tree", IdIsNot(2)))],
            Descendants(1)
        );

        Assert.Equal(["1", "4"], Staff.Lines(projection));
        Assert.Equal(QueryExecution.Translated, projection.Execution);
        Assert.All(schema.Values, table => Assert.Equal(0, ((ProvidedTable)table).Reads));
    }

    // ---- Helpers.

    private static PureQLFilteredSchemaDataSet Red()
    {
        return Staff.Filtered(Staff.Schema(), ("people", Staff.RedTeam));
    }

    private static IStoredSchemaDataSet TreeData()
    {
        return TreeTable.With(["1", ""], ["2", "1"], ["3", "2"], ["4", "1"]);
    }

    private static IStoredSchemaDataSet Tree()
    {
        return Staff.Schema(TreeData());
    }

    private static BooleanRow IdIsNot(long id)
    {
        return new NotEqualDecimalRow(
            new FieldInteger("tree", "id"),
            new LiteralInteger(id)
        );
    }

    // from t.people p [where <condition>] select p.name
    private static Query PeopleNames(BooleanRow? where)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.people", "p"),
            where: where,
            select: [
                new SelectItemProjection("name", new FieldStringNullable("p", "name")),
            ]
        );
    }

    // from t.people p full join t.orders o on o.person = p.id
    // select p.name, o.total
    private static Query PeopleWithOrders()
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.people", "p"),
            joins:
            [
                new JoinEntity(
                    JoinKind.Full,
                    "t.orders",
                    new EqualDecimalRow(
                        new FieldIntegerNullable("o", "person"),
                        new FieldIntegerNullable("p", "id")
                    ),
                    "o"
                ),
            ],
            select:
            [
                new SelectItemProjection("name", new FieldStringNullable("p", "name")),
                new SelectItemProjection("total", new FieldIntegerNullable("o", "total")),
            ]
        );
    }

    // aggregate <kind> as table, count() as count
    private static Query Counted(string entity, string kind)
    {
        return new TotalQuery(
            from: new FromEntity(entity),
            aggregate:
            [
                new SelectItemTotal("table", new LiteralString(kind)),
                new SelectItemTotal("count", new CountTotal()),
            ]
        );
    }

    // walk: the node 1 and the nodes below it whose rows the condition of
    // next keeps.
    private static Subquery Walk(BooleanRow? step)
    {
        BooleanRow below = new EqualDecimalRow(
            new FieldIntegerNullable("t.tree", "parent"),
            new FieldInteger("walk", "id")
        );

        return new SubqueryRecursive(
            "walk",
            new Recursion(
                seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                    from: new FromEntity("t.tree"),
                    where: new EqualDecimalRow(
                        new FieldInteger("t.tree", "id"),
                        new LiteralInteger(1)
                    ),
                    select:
                    [
                        new SelectItemProjection("id", new FieldInteger("t.tree", "id")),
                    ]
                ),
                next: new PlainNextQuery(
                    from: new FromEntity("t.tree"),
                    joins: [new JoinSubquery(JoinKind.Inner, "walk", below)],
                    where: step,
                    select: [new SelectItemRow("id", new FieldInteger("t.tree", "id"))]
                ),
                maxDepth: 10L
            )
        );
    }

    // The node and every node below it.
    private static PureQLQuery Descendants(long root)
    {
        BooleanRow below = new EqualDecimalRow(
            new FieldIntegerNullable("t.tree", "parent"),
            new FieldInteger("walk", "id")
        );

        return new MainPlainQueryWithoutPagination(
            subqueries:
            [
                new SubqueryRecursive(
                    "walk",
                    new Recursion(
                        seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                            from: new FromEntity("t.tree"),
                            where: new EqualDecimalRow(
                                new FieldInteger("t.tree", "id"),
                                new LiteralInteger(root)
                            ),
                            select:
                            [
                                new SelectItemProjection(
                                    "id",
                                    new FieldInteger("t.tree", "id")
                                ),
                            ]
                        ),
                        next: new PlainNextQuery(
                            from: new FromEntity("t.tree"),
                            joins: [new JoinSubquery(JoinKind.Inner, "walk", below)],
                            select:
                            [
                                new SelectItemRow("id", new FieldInteger("t.tree", "id")),
                            ]
                        ),
                        maxDepth: 10L
                    )
                ),
            ],
            from: new FromSubquery("walk"),
            select: [new SelectItemProjection("id", new FieldInteger("walk", "id"))],
            orderBy: [new OrderItemProjectionColumn("id")]
        );
    }
}
