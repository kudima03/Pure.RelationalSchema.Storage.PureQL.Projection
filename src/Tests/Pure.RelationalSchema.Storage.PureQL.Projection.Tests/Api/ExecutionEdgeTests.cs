using System.Linq.Expressions;
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
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Api;

// Where the constructs of specification 3 run: a set operation, a recursive
// subquery (plain or grouped next), window functions and inline rows over
// provided tables are handed to the provider whole, as one query, and the
// tables are not read; a refusal (NotSupportedException) at any stage runs
// the query in memory with the same rows, any other exception fails it, and
// an execution error fails it whichever way it runs. Every enumeration gives
// the same rows, and LINQ composed over the projection reads them.
public sealed class ExecutionEdgeTests
{
    // pen is the root; ink and pad are below it, cap below ink.
    private static readonly InlineTable Items = new(
        "t",
        "items",
        ("id", new LongColumnType()),
        ("name", new StringColumnType()),
        ("parent", new LongColumnType()),
        ("price", new LongColumnType())
    );

    private static readonly string[][] ItemRows =
    [
        ["1", "pen", "", "10"],
        ["2", "ink", "1", "20"],
        ["3", "pad", "1", "30"],
        ["4", "cap", "2", "40"],
    ];

    private static readonly FieldInteger Id = new("t.items", "id");

    private static readonly Dictionary<string, string[]> Expected = new()
    {
        ["union"] = ["cap", "ink", "ink", "pad", "pen"],
        ["recursion"] = ["1|0", "2|1", "3|1", "4|2"],
        ["grouped next"] = ["1|0", "2|1", "3|1", "4|2"],
        ["window"] = ["1|4|10||2", "2|3|30|pen|2", "3|2|50|ink|2", "4|1|70|pad|1"],
        ["inline"] = ["ink|2", "pen|1"],
    };

    [Theory]
    [MemberData(nameof(Constructs))]
    public void QueryOfANewConstructGoesWholeToTheProviderAndTheTableIsNotRead(
        string construct
    )
    {
        RecordingProvider provider = new();
        IStoredSchemaDataSet schema = ProvidedTable.Schema(
            Items.With(ItemRows),
            provider
        );
        PureQLProjection projection = new([schema], Query(construct), Values());

        Assert.Equal(Expected[construct], Lines(projection));
        Assert.Equal(QueryExecution.Translated, projection.Execution);
        Assert.Null(projection.FallbackReason);
        Assert.Same(
            ((MethodCallExpression)projection.Expression).Arguments[0],
            Assert.Single(provider.Queries)
        );
        Assert.Equal(0, Table(schema).Reads);
    }

    [Theory]
    [MemberData(nameof(ConstructsAndRefusals))]
    public void QueryOfANewConstructTheProviderRefusesRunsInMemoryWithTheSameRows(
        string construct,
        Refusal refusal
    )
    {
        RecordingProvider provider = new(refusal);
        IStoredSchemaDataSet schema = ProvidedTable.Schema(
            Items.With(ItemRows),
            provider
        );
        PureQLProjection projection = new([schema], Query(construct), Values());

        Assert.Equal(Expected[construct], Lines(projection));
        Assert.Equal(QueryExecution.InMemory, projection.Execution);
        _ = Assert.IsType<NotSupportedException>(projection.FallbackReason);
        _ = Assert.Single(provider.Queries);
    }

    [Theory]
    [MemberData(nameof(Constructs))]
    public void QueryOfANewConstructOverTablesHeldInMemoryRunsInMemory(string construct)
    {
        PureQLProjection projection = new(
            [Items.With(ItemRows)],
            Query(construct),
            Values()
        );

        Assert.Equal(Expected[construct], Lines(projection));
        Assert.Equal(QueryExecution.InMemory, projection.Execution);
        Assert.Null(projection.FallbackReason);
    }

    [Theory]
    [MemberData(nameof(Constructs))]
    public void ProviderFailureThatIsNotARefusalFailsAQueryOfANewConstruct(
        string construct
    )
    {
        RecordingProvider provider = new(failure: new InvalidOperationException("down"));
        IStoredSchemaDataSet schema = ProvidedTable.Schema(
            Items.With(ItemRows),
            provider
        );
        PureQLProjection projection = new([schema], Query(construct), Values());

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            projection.ToList()
        );

        Assert.Equal("down", failure.Message);
        Assert.NotEqual(QueryExecution.InMemory, projection.Execution);
        Assert.Equal(0, Table(schema).Reads);
    }

    [Theory]
    [MemberData(nameof(Constructs))]
    public void EveryEnumerationGivesTheSameRowsAndAsksTheProviderAgain(string construct)
    {
        RecordingProvider provider = new();
        PureQLProjection projection = new(
            [ProvidedTable.Schema(Items.With(ItemRows), provider)],
            Query(construct),
            Values()
        );

        IReadOnlyList<string> first = Lines(projection);
        IReadOnlyList<string> second = Lines(projection);

        Assert.Equal(Expected[construct], first);
        Assert.Equal(first, second);
        Assert.Equal(2, provider.Queries.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public void RowBeyondMaxDepthFailsTheQueryWhereverItRuns(bool provided)
    {
        // Semantics, Recursive subqueries: "If level maxDepth + 1 would hold
        // a row, the query fails"; an error fails the whole query, so no row
        // is returned and nothing falls back.
        IStoredSchemaDataSet schema = provided
            ? ProvidedTable.Schema(Items.With(ItemRows), new RecordingProvider())
            : Items.With(ItemRows);
        PureQLProjection projection = new([schema], Descendants(1L));

        _ = Assert.Throws<InvalidOperationException>(() => projection.ToList());
        Assert.NotEqual(QueryExecution.Translated, projection.Execution);
    }

    [Fact]
    public void WhereAndTakeComposedOverAUnionReadTheProvidersRows()
    {
        RecordingProvider provider = new();
        IStoredSchemaDataSet schema = ProvidedTable.Schema(
            Items.With(ItemRows),
            provider
        );
        PureQLProjection projection = new([schema], Query("union"));

        IQueryable<IRow> inks = Queryable.Where(
            projection,
            row =>
            row.Cells.Any(cell => cell.Value.Value.TextValue == "ink")
        );

        Assert.Equal(["ink", "ink"], ResultColumn.Texts(inks, "name"));
        Assert.Equal(5, projection.Count());
        _ = Assert.Single(Queryable.Take(projection, 1));
        Assert.Equal(3, provider.Queries.Count);
        Assert.All(provider.Queries, query =>
            Assert.Same(((MethodCallExpression)projection.Expression).Arguments[0], query)
        );
        Assert.Equal(0, Table(schema).Reads);
    }

    [Fact]
    public void LinqComposedOverARecursionReadsItsRows()
    {
        PureQLProjection projection = new(
            [ProvidedTable.Schema(Items.With(ItemRows), new RecordingProvider())],
            Query("recursion")
        );

        Assert.Equal(
            ["4"],
            ResultColumn.Texts(
                Queryable.Where(
                    projection,
                    row =>
                    row.Cells.Any(cell =>
                        cell.Key.Name.TextValue == "depth"
                        && cell.Value.Value.TextValue == "2"
                    )
                ),
                "id"
            )
        );
    }

    [Fact]
    public async Task AsyncEnumerationOfARecursionGoesThroughTheProvider()
    {
        RecordingProvider provider = new();
        IStoredSchemaDataSet schema = ProvidedTable.Schema(
            Items.With(ItemRows),
            provider
        );
        PureQLProjection projection = new([schema], Query("recursion"));
        List<IRow> rows = [];

        await foreach (IRow row in projection)
        {
            rows.Add(row);
        }

        Assert.Equal(["1", "2", "3", "4"], ResultColumn.Texts(rows, "id"));
        Assert.Equal(QueryExecution.Translated, projection.Execution);
        _ = Assert.Single(provider.Queries);
        Assert.Equal(0, Table(schema).Reads);
    }

    [Fact]
    public void UnionOfTwoProvidedTablesIsOneQuery()
    {
        RecordingProvider provider = new();
        IStoredSchemaDataSet items = ProvidedTable.Schema(Items.With(ItemRows), provider);
        IStoredSchemaDataSet extra = ProvidedTable.Schema(
            new InlineTable("s", "extra", ("name", new StringColumnType())).With(
                ["ink"],
                ["box"]
            ),
            provider
        );
        PureQLProjection projection = new(
            [items, extra],
            new MainCombinedQueryUnion([
                Names("t.items", null),
                new PlainQueryWithoutPaginationWithoutOrderBy(
                    from: new FromEntity("s.extra"),
                    select:
                    [
                        new SelectItemProjection(
                            "name",
                            new FieldString("s.extra", "name")
                        ),
                    ]
                ),
            ])
        );

        Assert.Equal(["box", "cap", "ink", "pad", "pen"], Lines(projection));
        Assert.Equal(QueryExecution.Translated, projection.Execution);
        _ = Assert.Single(provider.Queries);
        Assert.Equal(0, Table(items).Reads);
        Assert.Equal(0, Table(extra).Reads);
    }

    [Fact]
    public void ListAndWindowParametersAreHandedToTheProviderWithTheQuery()
    {
        RecordingProvider provider = new();
        PureQLProjection projection = new(
            [ProvidedTable.Schema(Items.With(ItemRows), provider)],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items"),
                where: new InStringRow(
                    new FieldString("t.items", "name"),
                    new ListParamString("names")
                ),
                select:
                [
                    new SelectItemProjection("id", Id),
                    new SelectItemProjection(
                        "s",
                        new SumIntegerProjection(
                            new FieldInteger("t.items", "price"),
                            over: new WindowWithFrame(
                                [new OrderItemRow(Id)],
                                new Frame(preceding: new ParamInteger("rows"))
                            )
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionColumn("id")]
            ),
            new Dictionary<string, IParameter>
            {
                ["names"] = new StringListParameter([
                    new Primitives.String.String("pen"),
                    new Primitives.String.String("pad"),
                    new Primitives.String.String("cap"),
                ]),
                ["rows"] = new IntegerParameter(new Long(1)),
            }
        );

        Assert.Equal(["1|10", "3|40", "4|70"], Lines(projection));
        Assert.Equal(QueryExecution.Translated, projection.Execution);
    }

    [Fact]
    public void QueryOfInlineRowsAloneReadsNoProvidedTable()
    {
        RecordingProvider provider = new();
        IStoredSchemaDataSet schema = ProvidedTable.Schema(
            Items.With(ItemRows),
            provider
        );
        PureQLProjection projection = new(
            [schema],
            new MainCombinedQueryExcept(
                [
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromSubquery("letters"),
                        select:
                        [
                            new SelectItemProjection(
                                "x",
                                new FieldString("letters", "x")
                            ),
                        ]
                    ),
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromSubquery("letters"),
                        where: new EqualStringRow(
                            new FieldString("letters", "x"),
                            new LiteralString("b")
                        ),
                        select:
                        [
                            new SelectItemProjection(
                                "x",
                                new FieldString("letters", "x")
                            ),
                        ]
                    ),
                ],
                [
                    new SubqueryRows(
                        "letters",
                        [
                            [new RowItem("x", new LiteralString("a"))],
                            [new RowItem("x", new LiteralString("b"))],
                            [new RowItem("x", new LiteralString("a"))],
                        ]
                    ),
                ]
            )
        );

        Assert.Equal(["a"], Lines(projection));
        Assert.Equal(0, Table(schema).Reads);
    }

    [Fact]
    public void UnionOfAProvidedTableAndInlineRowsIsHandedToTheProvider()
    {
        RecordingProvider provider = new();
        IStoredSchemaDataSet schema = ProvidedTable.Schema(
            Items.With(ItemRows),
            provider
        );
        PureQLProjection projection = new(
            [schema],
            new MainCombinedQueryUnion(
                [
                    Names("t.items", new LessThanDecimalRow(Id, new LiteralInteger(3))),
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromSubquery("more"),
                        select:
                        [
                            new SelectItemProjection(
                                "name",
                                new FieldString("more", "name")
                            ),
                        ]
                    ),
                ],
                [
                    new SubqueryRows(
                        "more",
                        [
                            [new RowItem("name", new LiteralString("ink"))],
                            [new RowItem("name", new LiteralString("box"))],
                        ]
                    ),
                ]
            )
        );

        Assert.Equal(["box", "ink", "pen"], Lines(projection));
        Assert.Equal(QueryExecution.Translated, projection.Execution);
        _ = Assert.Single(provider.Queries);
        Assert.Equal(0, Table(schema).Reads);
    }

    [Fact]
    public void UnionOfTablesOfTwoProvidersGivesTheRowsOfBoth()
    {
        IStoredSchemaDataSet items = ProvidedTable.Schema(
            Items.With(ItemRows),
            new RecordingProvider()
        );
        IStoredSchemaDataSet extra = ProvidedTable.Schema(
            new InlineTable("s", "extra", ("name", new StringColumnType())).With(
                ["ink"],
                ["box"]
            ),
            new RecordingProvider()
        );
        PureQLProjection projection = new(
            [items, extra],
            new MainCombinedQueryUnionAll([
                Names("t.items", new LessThanDecimalRow(Id, new LiteralInteger(3))),
                new PlainQueryWithoutPaginationWithoutOrderBy(
                    from: new FromEntity("s.extra"),
                    select:
                    [
                        new SelectItemProjection(
                            "name",
                            new FieldString("s.extra", "name")
                        ),
                    ]
                ),
            ])
        );

        Assert.Equal(["box", "ink", "ink", "pen"], Lines(projection));
    }

    [Fact]
    public void RecursionWithAListParameterIsHandedToTheProvider()
    {
        RecordingProvider provider = new();
        IStoredSchemaDataSet schema = ProvidedTable.Schema(
            Items.With(ItemRows),
            provider
        );
        PureQLProjection projection = new(
            [schema],
            ReadingWalk(
                new SubqueryRecursive(
                    "walk",
                    new Recursion(
                        seed: Root(),
                        next: new PlainNextQuery(
                            from: new FromEntity("t.items"),
                            joins: [Below()],
                            where: new InStringRow(
                                new FieldString("t.items", "name"),
                                new ListParamString("names")
                            ),
                            select:
                            [
                                new SelectItemRow("id", Id),
                                new SelectItemRow(
                                    "depth",
                                    new AddIntegerRow([
                                        new FieldInteger("walk", "depth"),
                                        new LiteralInteger(1),
                                    ])
                                ),
                            ]
                        ),
                        maxDepth: 10L
                    )
                )
            ),
            new Dictionary<string, IParameter>
            {
                ["names"] = new StringListParameter([
                    new Primitives.String.String("ink"),
                    new Primitives.String.String("cap"),
                ]),
            }
        );

        Assert.Equal(["1|0", "2|1", "4|2"], Lines(projection));
        Assert.Equal(QueryExecution.Translated, projection.Execution);
        Assert.Equal(0, Table(schema).Reads);
    }

    // ---- Helpers.

    private static readonly string[] ConstructNames =
    [
        "union",
        "recursion",
        "grouped next",
        "window",
        "inline",
    ];

    public static TheoryData<string> Constructs => [.. ConstructNames];

    public static TheoryData<string, Refusal> ConstructsAndRefusals
    {
        get
        {
            TheoryData<string, Refusal> data = [];

            foreach (string construct in ConstructNames)
            {
                data.Add(construct, Refusal.OnCreation);
                data.Add(construct, Refusal.OnEnumeration);
                data.Add(construct, Refusal.OnFirstValue);
            }

            return data;
        }
    }

    private static ProvidedTable Table(IStoredSchemaDataSet schema)
    {
        return (ProvidedTable)schema.Values.Single();
    }

    private static Dictionary<string, IParameter> Values()
    {
        return new Dictionary<string, IParameter>
        {
            ["offset"] = new IntegerParameter(new Long(1)),
        };
    }

    private static PureQLQuery Query(string construct)
    {
        return construct switch
        {
            "union" => new MainCombinedQueryUnionAll([
                Names("t.items", new LessThanDecimalRow(Id, new LiteralInteger(3))),
                Names(
                    "t.items",
                    new GreaterThanOrEqualDecimalRow(Id, new LiteralInteger(2))
                ),
            ]),
            "recursion" => Descendants(10L),
            "grouped next" => GroupedDescendants(),
            "window" => Windowed(),
            _ => Inline(),
        };
    }

    // from t.items [where <condition>] select name
    private static Query Names(string entity, BooleanRow? where)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity(entity),
            where: where,
            select: [new SelectItemProjection("name", new FieldString(entity, "name"))]
        );
    }

    private static Query Root()
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.items"),
            where: new EqualDecimalRow(Id, new LiteralInteger(1)),
            select:
            [
                new SelectItemProjection("id", Id),
                new SelectItemProjection("depth", new LiteralInteger(0)),
            ]
        );
    }

    private static Join Below()
    {
        return new JoinSubquery(
            JoinKind.Inner,
            "walk",
            new EqualDecimalRow(
                new FieldIntegerNullable("t.items", "parent"),
                new FieldInteger("walk", "id")
            )
        );
    }

    private static PureQLQuery ReadingWalk(Subquery walk)
    {
        return new MainPlainQueryWithoutPagination(
            subqueries: [walk],
            from: new FromSubquery("walk"),
            select:
            [
                new SelectItemProjection("id", new FieldInteger("walk", "id")),
                new SelectItemProjection("depth", new FieldInteger("walk", "depth")),
            ],
            orderBy: [new OrderItemProjectionColumn("id")]
        );
    }

    // pen and every item below it, with its depth.
    private static PureQLQuery Descendants(RecursionMaxDepth maxDepth)
    {
        return ReadingWalk(
            new SubqueryRecursive(
                "walk",
                new Recursion(
                    seed: Root(),
                    next: new PlainNextQuery(
                        from: new FromEntity("t.items"),
                        joins: [Below()],
                        select:
                        [
                            new SelectItemRow("id", Id),
                            new SelectItemRow(
                                "depth",
                                new AddIntegerRow([
                                    new FieldInteger("walk", "depth"),
                                    new LiteralInteger(1),
                                ])
                            ),
                        ]
                    ),
                    maxDepth: maxDepth
                )
            )
        );
    }

    // The same walk with a step that groups the rows it reads by item.
    private static PureQLQuery GroupedDescendants()
    {
        return ReadingWalk(
            new SubqueryRecursive(
                "walk",
                new Recursion(
                    seed: Root(),
                    next: new GroupedNextQuery(
                        from: new FromEntity("t.items"),
                        joins: [Below()],
                        groupBy:
                        [
                            new GroupKey("id", Id),
                            new GroupKey("depth", new FieldInteger("walk", "depth")),
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
                        ]
                    ),
                    maxDepth: 10L
                )
            )
        );
    }

    // id, rowNumber by price descending, a moving sum of two rows, the
    // previous name and the number of later items with a price above 25.
    private static PureQLQuery Windowed()
    {
        FieldInteger price = new("t.items", "price");
        OrderedWindow byId = new([new OrderItemRow(Id)]);

        return new MainPlainQueryWithoutPagination(
            from: new FromEntity("t.items"),
            select:
            [
                new SelectItemProjection("id", Id),
                new SelectItemProjection(
                    "n",
                    new RowNumberProjection(
                        new OrderedWindow([new OrderItemRow(price, SortDirection.Desc)])
                    )
                ),
                new SelectItemProjection(
                    "sum",
                    new SumIntegerProjection(
                        price,
                        over: new WindowWithFrame([new OrderItemRow(Id)], new Frame(1L))
                    )
                ),
                new SelectItemProjection(
                    "previous",
                    new PreviousStringNullableProjection(
                        new FieldString("t.items", "name"),
                        byId,
                        offset: new ParamInteger("offset")
                    )
                ),
                new SelectItemProjection(
                    "later",
                    new CountProjection(
                        predicate: new GreaterThanDecimalRow(
                            price,
                            new LiteralInteger(25)
                        ),
                        over: new WindowWithFrame(
                            [new OrderItemRow(Id)],
                            new Frame(following: new FrameRows(new All()))
                        )
                    )
                ),
            ],
            orderBy: [new OrderItemProjectionColumn("id")]
        );
    }

    // Inline ranks joined with the items by name.
    private static PureQLQuery Inline()
    {
        return new MainPlainQueryWithoutPagination(
            subqueries:
            [
                new SubqueryRows(
                    "ranks",
                    [
                        [
                            new RowItem("name", new LiteralString("pen")),
                            new RowItem("rank", new LiteralInteger(1)),
                        ],
                        [
                            new RowItem("name", new LiteralString("ink")),
                            new RowItem("rank", new LiteralInteger(2)),
                        ],
                        [
                            new RowItem("name", new LiteralString("box")),
                            new RowItem("rank", new LiteralInteger(3)),
                        ],
                    ]
                ),
            ],
            from: new FromEntity("t.items"),
            joins:
            [
                new JoinSubquery(
                    JoinKind.Inner,
                    "ranks",
                    new EqualStringRow(
                        new FieldString("ranks", "name"),
                        new FieldString("t.items", "name")
                    )
                ),
            ],
            select:
            [
                new SelectItemProjection("name", new FieldString("t.items", "name")),
                new SelectItemProjection("rank", new FieldInteger("ranks", "rank")),
            ]
        );
    }

    // Every row as its cell texts in column order, joined by '|', sorted:
    // a combined query and an unsorted join are unordered.
    private static IReadOnlyList<string> Lines(PureQLProjection projection)
    {
        return [.. Staff.Lines(projection).Order(StringComparer.Ordinal)];
    }
}
