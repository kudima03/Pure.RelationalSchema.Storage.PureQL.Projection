using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.BooleanOperations;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.Equalities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// HAVING composed with and / or / not over aggregate comparisons, plus
// equality between two aggregates of the same group. Orders are grouped by
// their user; expected groups are computed from the ground-truth records.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "Having")]
public sealed class HavingCompositeTests
{
    private static NumberReturning OrderCount()
    {
        return new NumberReturning(
            new Count(
                new ArrayReturning(
                    new UuidArrayReturning(
                        new UuidField(
                            "schema_with_foreign_keys.orders",
                            "order_id"
                        )
                    )
                )
            )
        );
    }

    private static NumberArrayReturning Totals()
    {
        return new NumberArrayReturning(
            new NumberField(
                "schema_with_foreign_keys.orders",
                "order_total"
            )
        );
    }

    private static NumberReturning MinTotal()
    {
        return new NumberReturning(new NumberAggregate(new MinNumber(Totals())));
    }

    private static NumberReturning MaxTotal()
    {
        return new NumberReturning(new NumberAggregate(new MaxNumber(Totals())));
    }

    private static BooleanReturning CountGreaterThan(double threshold)
    {
        return new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    ComparisonOperator.GreaterThan,
                    OrderCount(),
                    new NumberReturning(new NumberScalar(threshold))
                )
            )
        );
    }

    private static BooleanReturning MaxTotalAtLeast(double threshold)
    {
        return new BooleanReturning(
            new Comparison(
                new NumberComparison(
                    ComparisonOperator.GreaterThanOrEqual,
                    MaxTotal(),
                    new NumberReturning(new NumberScalar(threshold))
                )
            )
        );
    }

    private static Query OrdersGroupedByUser(BooleanReturning having)
    {
        return new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.orders",
                                "order_user_id"
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new UuidField(
                        "schema_with_foreign_keys.orders",
                        "order_user_id"
                    )
                ),
            ],
            having,
            orderBy: null,
            pagination: null
        );
    }

    [Fact]
    public void HavingAndOfTwoAggregateComparisonsRequiresBoth()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new BooleanOperator(
                    new AndOperator([CountGreaterThan(1), MaxTotalAtLeast(200)])
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows
            .GroupBy(order => order.OrderUserId)
            .Count(group =>
                group.Count() > 1 && group.Max(order => order.OrderTotal) >= 200
            );

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    public void HavingOrOfTwoAggregateComparisonsAcceptsEither()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new BooleanOperator(
                    new OrOperator([CountGreaterThan(1), MaxTotalAtLeast(200)])
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows
            .GroupBy(order => order.OrderUserId)
            .Count(group =>
                group.Count() > 1 || group.Max(order => order.OrderTotal) >= 200
            );

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    public void HavingNotInvertsAnAggregateComparison()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new BooleanOperator(new NotOperator(CountGreaterThan(1)))
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows
            .GroupBy(order => order.OrderUserId)
            .Count(group => group.Count() <= 1);

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    public void HavingEqualityOfMinAndMaxKeepsConstantGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new Equality(
                    new SingleValueEquality(
                        new NumberEquality(MinTotal(), MaxTotal())
                    )
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows
            .GroupBy(order => order.OrderUserId)
            .Count(group =>
                group.Min(order => order.OrderTotal)
                == group.Max(order => order.OrderTotal)
            );

        Assert.Equal(expected, result.Count);
    }
}
