using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        const double thresholdDays = 1200;

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.orders",
                                "order_id"
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
                                        "schema_with_foreign_keys.orders",
                                        "placed_on"
                                    )
                                ),
                                new DateArrayReturning(
                                    new DateField(
                                        "schema_with_foreign_keys.users",
                                        "signup_date"
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
                    "schema_with_foreign_keys.users",
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        "schema_with_foreign_keys.orders",
                                        "order_user_id"
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        "schema_with_foreign_keys.users",
                                        "user_id"
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
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(order =>
                {
                    UserRecord user = userRows.Single(candidate =>
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
                .Select(row => row.Uuid("order_id")!.Value)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual);
    }
}
