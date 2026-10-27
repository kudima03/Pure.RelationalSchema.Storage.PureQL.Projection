using System.Linq.Expressions;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.SelectItems;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Api;

// Where a projection runs its query. The query below the result rows goes to
// the provider of its stored tables, which reads them as a database does:
// the tables themselves are never read in full (the defect of issue #237).
// The tree runs in memory only over tables held in memory, or when the
// provider refuses the query, and the projection says which happened.
public sealed class ExecutionTests
{
    private static readonly InlineTable Items = new(
        "t",
        "items",
        ("id", new LongColumnType()),
        ("name", new StringColumnType())
    );

    private static readonly InlineTable Tags = new(
        "s",
        "tags",
        ("item_id", new LongColumnType()),
        ("tag", new StringColumnType())
    );

    private static readonly string[][] ItemRows = [["1", "pen"], ["2", "ink"], ["3", "pad"]];

    // from t.items where id > 1 select name
    private static readonly PureQLQuery NamesAboveOne = new(
        new MainPlainQuery(
            new From(new FromEntity("t.items", "i")),
            [
                new SelectItemProjection(
                    new SelectItemProjectionNonNullable(
                        new SelectItemProjectionString(
                            "name",
                            new StringProjection(new FieldString("i", "name"))
                        )
                    )
                ),
            ],
            subqueries: null,
            joins: null,
            where: new BooleanRow(
                new ComparisonRow(
                    new GreaterThanRow(
                        new GreaterThanDecimalRow(
                            new DecimalNullableRow(new IntegerRow(new FieldInteger("i", "id"))),
                            new DecimalNullableRow(new IntegerRow(new LiteralInteger(1)))
                        )
                    )
                )
            ),
            orderBy: null,
            pagination: null,
            distinctBy: null
        )
    );

    [Fact]
    public void QueryBelowTheResultRowsGoesToTheProviderAndTheTableIsNotRead()
    {
        RecordingProvider provider = new();
        IStoredSchemaDataSet schema = ProvidedTable.Schema(Items.With(ItemRows), provider);
        ProvidedTable table = (ProvidedTable)schema.Values.Single();
        PureQLProjection projection = new([schema], NamesAboveOne);

        Assert.Equal(QueryExecution.NotRun, projection.Execution);
        Assert.Equal(["ink", "pad"], ResultColumn.Texts(projection, "name"));
        Assert.Equal(QueryExecution.Translated, projection.Execution);
        Assert.Null(projection.FallbackReason);
        Assert.Equal(0, table.Reads);

        Expression query = Assert.Single(provider.Queries);
        Assert.Same(((MethodCallExpression)projection.Expression).Arguments[0], query);
        Assert.Equal(
            "[items].Select(i => (i[id], i[name])).Where(i => (i.Item1 > 1L))"
                + ".Select(row => (row.Item2))",
            TreeText.Of(query)
        );
        Assert.DoesNotContain("ResultRow", TreeText.Of(query), StringComparison.Ordinal);
    }

    [Fact]
    public void EveryEnumerationAsksTheProviderAgain()
    {
        RecordingProvider provider = new();
        PureQLProjection projection = new(
            [ProvidedTable.Schema(Items.With(ItemRows), provider)],
            NamesAboveOne
        );

        Assert.Equal(2, projection.Count());
        Assert.Equal(2, projection.Count());
        Assert.Equal(2, provider.Queries.Count);
    }

    [Theory]
    [InlineData(Refusal.OnCreation)]
    [InlineData(Refusal.OnEnumeration)]
    [InlineData(Refusal.OnFirstValue)]
    public void QueryTheProviderRefusesRunsInMemory(Refusal refusal)
    {
        RecordingProvider provider = new(refusal);
        IStoredSchemaDataSet schema = ProvidedTable.Schema(Items.With(ItemRows), provider);
        ProvidedTable table = (ProvidedTable)schema.Values.Single();
        PureQLProjection projection = new([schema], NamesAboveOne);

        Assert.Equal(["ink", "pad"], ResultColumn.Texts(projection, "name"));
        Assert.Equal(QueryExecution.InMemory, projection.Execution);
        Assert.Contains(
            refusal.ToString(),
            projection.FallbackReason!.Message,
            StringComparison.Ordinal
        );
        _ = Assert.Single(provider.Queries);
        Assert.Equal(1, table.Reads);
    }

    [Fact]
    public void ProviderFailureThatIsNotARefusalFailsTheQuery()
    {
        RecordingProvider provider = new(failure: new InvalidOperationException("down"));
        IStoredSchemaDataSet schema = ProvidedTable.Schema(Items.With(ItemRows), provider);
        ProvidedTable table = (ProvidedTable)schema.Values.Single();
        PureQLProjection projection = new([schema], NamesAboveOne);

        InvalidOperationException failure = Assert.Throws<InvalidOperationException>(() =>
            projection.ToList()
        );

        Assert.Equal("down", failure.Message);
        Assert.Equal(QueryExecution.NotRun, projection.Execution);
        Assert.Equal(0, table.Reads);
    }

    [Fact]
    public void TablesHeldInMemoryRunInMemoryWithoutAProvider()
    {
        PureQLProjection projection = new([Items.With(ItemRows)], NamesAboveOne);

        Assert.Equal(["ink", "pad"], ResultColumn.Texts(projection, "name"));
        Assert.Equal(QueryExecution.InMemory, projection.Execution);
        Assert.Null(projection.FallbackReason);
    }

    [Fact]
    public void LastEnumerationTellsWhereItRan()
    {
        RecordingProvider provider = new();
        IStoredSchemaDataSet provided = ProvidedTable.Schema(Items.With(ItemRows), provider);
        PureQLProjection translated = new([provided], NamesAboveOne);
        PureQLProjection refused = new(
            [ProvidedTable.Schema(Items.With(ItemRows), new RecordingProvider(Refusal.OnCreation))],
            NamesAboveOne
        );

        _ = translated.ToList();
        _ = refused.ToList();

        Assert.Equal(QueryExecution.Translated, translated.Execution);
        Assert.Equal(QueryExecution.InMemory, refused.Execution);
        Assert.NotNull(refused.FallbackReason);
    }

    [Fact]
    public void QueryComposedOverTheProjectionRunsInMemoryOverTheProviderRows()
    {
        RecordingProvider provider = new();
        IStoredSchemaDataSet schema = ProvidedTable.Schema(Items.With(ItemRows), provider);
        ProvidedTable table = (ProvidedTable)schema.Values.Single();
        PureQLProjection projection = new([schema], NamesAboveOne);

        Assert.Equal(["ink"], ResultColumn.Texts(Queryable.Take(projection, 1), "name"));
        Assert.Equal(
            2,
            projection.Provider.Execute<int>(
                Expression.Call(
                    typeof(Queryable),
                    nameof(Queryable.Count),
                    [typeof(IRow)],
                    projection.Expression
                )
            )
        );

        Assert.Equal(2, provider.Queries.Count);
        Assert.All(
            provider.Queries,
            query => Assert.Same(((MethodCallExpression)projection.Expression).Arguments[0], query)
        );
        Assert.Equal(QueryExecution.Translated, projection.Execution);
        Assert.Equal(0, table.Reads);
    }

    [Fact]
    public async Task AsyncEnumerationGoesThroughTheProvider()
    {
        RecordingProvider provider = new();
        IStoredSchemaDataSet schema = ProvidedTable.Schema(Items.With(ItemRows), provider);
        ProvidedTable table = (ProvidedTable)schema.Values.Single();
        PureQLProjection projection = new([schema], NamesAboveOne);
        List<IRow> rows = [];

        await foreach (IRow row in projection)
        {
            rows.Add(row);
        }

        Assert.Equal(["ink", "pad"], ResultColumn.Texts(rows, "name"));
        Assert.Equal(QueryExecution.Translated, projection.Execution);
        _ = Assert.Single(provider.Queries);
        Assert.Equal(0, table.Reads);
    }

    [Fact]
    public void JoinOverTwoProvidedTablesIsOneQueryOverBothRoots()
    {
        RecordingProvider provider = new();
        IStoredSchemaDataSet items = ProvidedTable.Schema(Items.With(ItemRows), provider);
        IStoredSchemaDataSet tags = ProvidedTable.Schema(
            Tags.With(["1", "pen"], ["3", "office"], ["4", "spare"]),
            provider
        );
        PureQLProjection projection = new(
            [items, tags],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionString(
                                    "tag",
                                    new StringProjection(new FieldString("g", "tag"))
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins:
                    [
                        new Join(
                            new JoinEntity(
                                JoinKind.Inner,
                                "s.tags",
                                new BooleanRow(
                                    new ComparisonRow(
                                        new EqualRow(
                                            new EqualDecimalRow(
                                                new DecimalNullableRow(
                                                    new IntegerRow(
                                                        new FieldInteger("g", "item_id")
                                                    )
                                                ),
                                                new DecimalNullableRow(
                                                    new IntegerRow(new FieldInteger("i", "id"))
                                                )
                                            )
                                        )
                                    )
                                ),
                                "g"
                            )
                        ),
                    ],
                    where: null,
                    orderBy: null,
                    pagination: null,
                    distinctBy: null
                )
            )
        );

        Assert.Equal(["pen", "office"], ResultColumn.Texts(projection, "tag"));
        Assert.Equal(QueryExecution.Translated, projection.Execution);

        Expression query = Assert.Single(provider.Queries);
        Assert.StartsWith("[items].Select(i => (i[id])).Join([tags]", TreeText.Of(query));
        Assert.All(
            items.Values.Concat(tags.Values).Cast<ProvidedTable>(),
            table => Assert.Equal(0, table.Reads)
        );
    }
}
