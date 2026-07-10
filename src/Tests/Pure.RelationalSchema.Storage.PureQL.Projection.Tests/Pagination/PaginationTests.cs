using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Pagination;

// Skip/Take pagination. Paired with ORDER BY for a deterministic window, plus
// boundary cases (take beyond the end, skip beyond the end, full page).
[Trait("Clause", "Pagination")]
[Trait("Feature", "Pagination")]
public sealed class PaginationTests
{
    [Fact]
    public void SkipAndTakeReturnTheRequestedWindowOfAnOrderedResult()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Total
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            new global::PureQL.CSharp.Model.Pagination(2, 2)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.OrderRows.OrderBy(order => order.OrderTotal)
                .Skip(2)
                .Take(2)
                .Select(order => (double?)order.OrderTotal)
                .ToArray(),
            [.. result.Rows.Select(row => row.Double(SampleDatabase.Orders.Total))]
        );
    }

    [Fact]
    public void TakeBeyondEndReturnsAllRemainingRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Total
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            new global::PureQL.CSharp.Model.Pagination(0, 100)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(db.OrderRows.Count, result.Count);
    }

    [Fact]
    public void SkipBeyondEndReturnsNoRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Total
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            new global::PureQL.CSharp.Model.Pagination(100, 5)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void FullPageReturnsEveryRowInOrder()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Total
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            new global::PureQL.CSharp.Model.Pagination(0, 6)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.OrderRows.OrderBy(order => order.OrderTotal)
                .Select(order => (double?)order.OrderTotal)
                .ToArray(),
            [.. result.Rows.Select(row => row.Double(SampleDatabase.Orders.Total))]
        );
    }
}
