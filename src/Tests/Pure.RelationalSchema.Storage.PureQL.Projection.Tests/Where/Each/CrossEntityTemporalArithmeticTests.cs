using Pure.Primitives.String;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Samples.Schemas;
using Pure.RelationalSchema.Samples.Tables;
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
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderIdColumn().Name.TextValue
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
                                        new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                        new PlacedOnColumn().Name.TextValue
                                    )
                                ),
                                new DateArrayReturning(
                                    new DateField(
                                        new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
).TextValue,
                                        new SignupDateColumn().Name.TextValue
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
                    new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
).TextValue,
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                        new OrderUserIdColumn().Name.TextValue
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
).TextValue,
                                        new UserIdColumn().Name.TextValue
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
                .Select(row => row.Uuid(new OrderIdColumn().Name.TextValue)!.Value)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.Equal(expected, actual);
    }
}
