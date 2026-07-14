using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;
using ModelPagination = PureQL.CSharp.Model.Pagination;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Combined;

// Every clause at once, in pipeline order: JOIN -> WHERE -> ORDER BY ->
// GROUP BY -> HAVING -> projection -> pagination. The expected window is
// computed step-by-step from the ground-truth records.
[Trait("Clause", "Combined")]
[Trait("Feature", "FullPipeline")]
public sealed class FullPipelineTests
{
    [Fact]
    public void JoinWhereGroupByHavingOrderByPaginationCompose()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
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
                        )
                    ),
                    "orderCount"
                ),
            ],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachBooleanEquality(
                        new BooleanArrayReturning(
                            new BooleanField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Active
                            )
                        ),
                        new BooleanReturning(new BooleanScalar(true))
                    )
                )
            ),
            [
                new Join(
                    JoinType.Inner,
                    SampleDatabase.Users.Entity,
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        SampleDatabase.Orders.Entity,
                                        SampleDatabase.Orders.UserId
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        SampleDatabase.Users.Entity,
                                        SampleDatabase.Users.Id
                                    )
                                )
                            )
                        )
                    )
                ),
            ],
            [
                new Field(
                    new StringField(
                        SampleDatabase.Orders.Entity,
                        SampleDatabase.Orders.Status
                    )
                ),
            ],
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThan,
                        new NumberReturning(
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
                        ),
                        new NumberReturning(new NumberScalar(1))
                    )
                )
            ),
            [
                new OrderByItem(
                    new Field(
                        new StringField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Status
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            new ModelPagination(1, 2)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        (string, double)[] expected =
        [
            .. db.OrderRows
                .Where(order =>
                    db.UserRows.Single(user =>
                        user.UserId == order.OrderUserId
                    ).UserActive
                )
                .GroupBy(order => order.OrderStatus)
                .Where(group => group.Count() > 1)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => (group.Key, (double)group.Count()))
                .Skip(1)
                .Take(2),
        ];

        (string, double)[] actual =
        [
            .. result.Rows.Select(row =>
                (
                    row[SampleDatabase.Orders.Status]!,
                    row.Double("orderCount")!.Value
                )
            ),
        ];

        Assert.Equal(expected, actual);
    }
}
