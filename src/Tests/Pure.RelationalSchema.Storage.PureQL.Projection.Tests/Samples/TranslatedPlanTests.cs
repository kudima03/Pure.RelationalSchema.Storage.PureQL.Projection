using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.SelectItems;
using PureQL.CSharp.Model.TotalExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Samples;

// The cost of a query translated over the fixtures stored in PostgreSQL, read
// from its plan: an aggregate over all rows in a plain select reads the
// sources once, as a window aggregate (issue #248), and a stored field is
// compared as its typed column, not as its cell text parsed back (issue #250).
// Needs Docker.
public sealed class TranslatedPlanTests(PostgreSqlDatabase database)
    : IClassFixture<PostgreSqlDatabase>
{
    private const string Orders = "schema_with_foreign_keys.orders";

    // A hash or merge condition over two stored columns as they are.
    private const string ColumnsCompared =
        @" Cond: \(t\d\.""[0-9A-F]+"" = t\d\.""[0-9A-F]+""\)";

    private readonly PostgreSqlDatabase _database = database;

    [Fact]
    public void AggregateOverAllRowsInAPlainSelectReadsTheSourceOnce()
    {
        Sample sample = Sample.Named("ShareOfTotalQuery");
        PureQLProjection projection = new(_database.Datasets, sample.Query);

        _ = projection.ToList();
        string plan = _database.LastPlan();

        Assert.Equal(QueryExecution.Translated, projection.Execution);
        _ = Assert.Single(
            plan.Split('\n'),
            line => line.Contains("Scan on \"", StringComparison.Ordinal)
        );
    }

    [Fact]
    public void DateRangeJoinComparesTheStoredDates()
    {
        PureQLProjection projection = new(_database.Datasets, DateRangeJoin());

        IReadOnlyList<string> count = ResultColumn.Texts(projection, "n");
        string plan = _database.LastPlan();

        Assert.Equal(QueryExecution.Translated, projection.Execution);
        Assert.Equal(["11"], count);
        Assert.DoesNotContain("EXTRACT", plan, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("::text", plan, StringComparison.Ordinal);
    }

    [Fact]
    public void JoinOnUuidKeysComparesTheStoredUuids()
    {
        PureQLProjection projection = new(
            _database.Datasets,
            Sample.Named("InnerJoinQuery").Query
        );

        _ = projection.ToList();
        string plan = _database.LastPlan();

        Assert.Equal(QueryExecution.Translated, projection.Execution);
        Assert.DoesNotContain("::text", plan, StringComparison.Ordinal);
        Assert.DoesNotContain("CAST(", plan, StringComparison.Ordinal);
        Assert.Matches(ColumnsCompared, plan);
    }

    // The count of the pairs of orders o and p where p is placed on o's day or
    // the next: the six orders are placed on six consecutive days, so 6 + 5.
    private static PureQLQuery DateRangeJoin()
    {
        return new PureQLQuery(
            new MainTotalQuery(
                new From(new FromEntity(Orders, "o")),
                [
                    new SelectItemTotal(
                        new SelectItemTotalNonNullable(
                            new SelectItemTotalInteger(
                                "n",
                                new IntegerTotal(
                                    new AggregateIntegerTotal(new CountTotal())
                                )
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
                            Orders,
                            new BooleanRow(
                                new LogicalRow(
                                    new AndRow(
                                        [
                                            new BooleanRow(
                                                new ComparisonRow(
                                                    new GreaterThanOrEqualRow(
                                                        new GreaterThanOrEqualDateRow(
                                                            PlacedOn("p"),
                                                            PlacedOn("o")
                                                        )
                                                    )
                                                )
                                            ),
                                            new BooleanRow(
                                                new ComparisonRow(
                                                    new LessThanRow(
                                                        new LessThanDateRow(
                                                            PlacedOn("p"),
                                                            new DateNullableRow(
                                                                new DateRow(
                                                                    new DateAddDaysDateRow(
                                                                        new DateRow(
                                                                            new FieldDate(
                                                                                "o",
                                                                                "placed_on"
                                                                            )
                                                                        ),
                                                                        new IntegerRow(
                                                                            new LiteralInteger(
                                                                                2
                                                                            )
                                                                        )
                                                                    )
                                                                )
                                                            )
                                                        )
                                                    )
                                                )
                                            ),
                                        ]
                                    )
                                )
                            ),
                            "p"
                        )
                    ),
                ],
                where: null
            )
        );
    }

    private static DateNullableRow PlacedOn(string source)
    {
        return new DateNullableRow(new DateRow(new FieldDate(source, "placed_on")));
    }
}
