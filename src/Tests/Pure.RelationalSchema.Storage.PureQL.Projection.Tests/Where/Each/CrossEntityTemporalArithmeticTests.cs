using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachDateArithmetics;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Per-row temporal arithmetic whose operands come from both sides of a
// join: eachDateDiffDays(order date, user signup date) computes a per-row
// day gap across the merged row, then feeds a numeric comparison.
[Trait("Clause", "Where")]
[Trait("Feature", "EachDateArithmetic")]
public sealed class CrossEntityTemporalArithmeticTests
{
    [Fact]
    public void EachDateDiffDaysAcrossJoinedTablesFiltersByTheGap()
    {
        SampleDatabase db = new SampleDatabase();

        const double thresholdDays = 1200;

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Id
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new EachDateDiffDays(
                                new DateArrayReturning(
                                    new DateField(
                                        SampleDatabase.Orders.Entity,
                                        SampleDatabase.Orders.PlacedOn
                                    )
                                ),
                                new DateArrayReturning(
                                    new DateField(
                                        SampleDatabase.Users.Entity,
                                        SampleDatabase.Users.SignupDate
                                    )
                                )
                            )
                        ),
                        new NumberReturning(new NumberScalar(thresholdDays))
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
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderRows
                .Where(order =>
                {
                    UserRow user = db.UserRows.Single(candidate =>
                        candidate.UserId == order.OrderUserId
                    );

                    int gap = order.PlacedOn.DayNumber
                        - user.SignupDate.DayNumber;

                    return gap > thresholdDays;
                })
                .Select(order => order.OrderId)
                .OrderBy(id => id),
        ];

        Guid[] actual =
        [
            .. result.Rows
                .Select(row => row.Uuid(SampleDatabase.Orders.Id)!.Value)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual);
    }
}
