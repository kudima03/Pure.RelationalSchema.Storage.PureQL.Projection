using System.Linq.Expressions;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Api;

// Where a query over a filtered schema dataset runs. The conditions of the
// tables are part of the query the provider of the stored tables is given,
// so the provider still runs the whole query, the tables are not read in
// full, and the filtered table itself, read on its own, is a query of the
// provider too. When the provider refuses the query it runs in memory, over
// the same filtered rows.
public sealed class FilteredSchemaExecutionTests
{
    private const string RedPeople =
        "[people].Where(people => (people[team] == \"red\"))";

    [Fact]
    public void QueryOverFilteredTablesGoesWholeToTheProviderAndTheTablesAreNotRead()
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
            Staff.PeopleWithOrders(JoinKind.Left)
        );

        Assert.Equal(["Ann|5", "Dee|9"], Staff.Lines(projection).Order());
        Assert.Equal(QueryExecution.Translated, projection.Execution);
        Assert.Null(projection.FallbackReason);
        Assert.All(schema.Values, table => Assert.Equal(0, ((ProvidedTable)table).Reads));
        Assert.Same(
            ((MethodCallExpression)projection.Expression).Arguments[0],
            Assert.Single(provider.Queries)
        );
    }

    [Fact]
    public void QueryTheProviderRefusesRunsInMemoryOverTheFilteredRows()
    {
        RecordingProvider provider = new(Refusal.OnCreation);
        PureQLProjection projection = new(
            [
                Staff.Filtered(
                    ProvidedTable.Schema(Staff.Schema(), provider),
                    ("people", Staff.RedTeam),
                    ("orders", Staff.BigOrders)
                ),
            ],
            Staff.PeopleWithOrders(JoinKind.Left)
        );

        Assert.Equal(["Ann|5", "Dee|9"], Staff.Lines(projection).Order());
        Assert.Equal(QueryExecution.InMemory, projection.Execution);
        Assert.NotNull(projection.FallbackReason);
    }

    [Fact]
    public void FilteredTableReadOnItsOwnIsAQueryOfTheProvider()
    {
        RecordingProvider provider = new();
        IStoredSchemaDataSet schema = ProvidedTable.Schema(Staff.Schema(), provider);
        IStoredTableDataSet people = Staff.Filtered(schema, ("people", Staff.RedTeam))[
            Staff.Table(schema, "people")
        ];

        Assert.Equal(["1|Ann|red|True", "4|Dee|red|False"], Staff.Lines(people));
        Assert.Equal(RedPeople, TreeText.Of(Assert.Single(provider.Queries)));
        Assert.Same(provider, people.Provider);
        Assert.Equal(RedPeople, TreeText.Of(people.Expression));
        Assert.All(schema.Values, table => Assert.Equal(0, ((ProvidedTable)table).Reads));
    }
}
