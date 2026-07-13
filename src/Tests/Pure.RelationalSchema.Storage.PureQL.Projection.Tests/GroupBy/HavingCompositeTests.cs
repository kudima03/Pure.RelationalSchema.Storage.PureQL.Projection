using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
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
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Id
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
                SampleDatabase.Orders.Entity,
                SampleDatabase.Orders.Total
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
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.UserId
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
                        SampleDatabase.Orders.Entity,
                        SampleDatabase.Orders.UserId
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
        SampleDatabase db = new SampleDatabase();

        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new BooleanOperator(
                    new AndOperator([CountGreaterThan(1), MaxTotalAtLeast(200)])
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int expected = db.OrderRows
            .GroupBy(order => order.OrderUserId)
            .Count(group =>
                group.Count() > 1 && group.Max(order => order.OrderTotal) >= 200
            );

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    public void HavingOrOfTwoAggregateComparisonsAcceptsEither()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new BooleanOperator(
                    new OrOperator([CountGreaterThan(1), MaxTotalAtLeast(200)])
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int expected = db.OrderRows
            .GroupBy(order => order.OrderUserId)
            .Count(group =>
                group.Count() > 1 || group.Max(order => order.OrderTotal) >= 200
            );

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    public void HavingNotInvertsAnAggregateComparison()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = OrdersGroupedByUser(
            new BooleanReturning(
                new BooleanOperator(new NotOperator(CountGreaterThan(1)))
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int expected = db.OrderRows
            .GroupBy(order => order.OrderUserId)
            .Count(group => group.Count() <= 1);

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    public void HavingEqualityOfMinAndMaxKeepsConstantGroups()
    {
        SampleDatabase db = new SampleDatabase();

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
            new PureQLProjection(db.Datasets, query)
        );

        int expected = db.OrderRows
            .GroupBy(order => order.OrderUserId)
            .Count(group =>
                group.Min(order => order.OrderTotal)
                == group.Max(order => order.OrderTotal)
            );

        Assert.Equal(expected, result.Count);
    }
}
