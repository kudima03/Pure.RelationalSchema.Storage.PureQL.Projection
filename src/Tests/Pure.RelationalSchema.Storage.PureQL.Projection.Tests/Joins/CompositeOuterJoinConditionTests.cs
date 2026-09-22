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
using PureQL.CSharp.Model.EachBooleanOperations;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// Composite per-row on conditions (and / or / not around equalities and
// comparisons) on inner and outer joins. Expected row sets are computed
// pair-by-pair from the ground-truth records.
[Trait("Clause", "Join")]
[Trait("Feature", "CompositeCondition")]
public sealed class CompositeOuterJoinConditionTests
{
    private static BooleanArrayReturning UserKeyMatch()
    {
        return new BooleanArrayReturning(
            new EachEquality(
                new EachUuidEquality(
                    new UuidArrayReturning(
                        new UuidField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new UsersTable().Name,
                                ]
                            ).TextValue,
                            new UserIdColumn().Name.TextValue
                        )
                    ),
                    new UuidArrayReturning(
                        new UuidField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new OrdersTable().Name,
                                ]
                            ).TextValue,
                            new OrderUserIdColumn().Name.TextValue
                        )
                    )
                )
            )
        );
    }

    private static BooleanArrayReturning TotalGreaterThan(double threshold)
    {
        return new BooleanArrayReturning(
            new EachComparison(
                new EachNumberComparison(
                    EachComparisonOperator.EachGreaterThan,
                    new NumberArrayReturning(
                        new NumberField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new OrdersTable().Name,
                                ]
                            ).TextValue,
                            new OrderTotalColumn().Name.TextValue
                        )
                    ),
                    new NumberReturning(new NumberScalar(threshold))
                )
            )
        );
    }

    private static Query UsersJoinedToOrders(
        JoinType joinType,
        BooleanArrayReturning onCondition
    )
    {
        return new Query(
            new FromExpression(new JoinedString(
                new DotString(),
                [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
            ).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue,
                                new UserNameColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            where: null,
            [new Join(joinType, new JoinedString(
                new DotString(),
                [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
            ).TextValue, onCondition)],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );
    }

    [Fact]
    public void LeftJoinOnKeyAndThresholdPadsUsersWithoutQualifyingOrders()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        const double threshold = 100;

        Query query = UsersJoinedToOrders(
            JoinType.Left,
            new BooleanArrayReturning(
                new EachAndOperator([UserKeyMatch(), TotalGreaterThan(threshold)])
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedCount = userRows.Sum(user =>
            Math.Max(
                1,
                orderRows.Count(order =>
                    order.OrderUserId == user.UserId
                    && order.OrderTotal > threshold
                )
            )
        );

        Assert.Equal(expectedCount, result.Count);

        foreach (UserRecord user in userRows)
        {
            int expectedAppearances = Math.Max(
                1,
                orderRows.Count(order =>
                    order.OrderUserId == user.UserId
                    && order.OrderTotal > threshold
                )
            );

            Assert.Equal(
                expectedAppearances,
                result
                    .Column(new UserNameColumn().Name.TextValue)
                    .Count(name => name == user.UserName)
            );
        }
    }

    [Fact]
    public void InnerJoinOnDisjunctiveConditionKeepsEitherMatch()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        const double markerTotal = 200;

        Query query = UsersJoinedToOrders(
            JoinType.Inner,
            new BooleanArrayReturning(
                new EachOrOperator(
                    [
                        UserKeyMatch(),
                        new BooleanArrayReturning(
                            new EachEquality(
                                new EachNumberEquality(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            new JoinedString(
                                                new DotString(),
                                                [
                                                    new RelationalSchemaWithForeignKeys().Name,
                                                    new OrdersTable().Name,
                                                ]
                                            ).TextValue,
                                            new OrderTotalColumn().Name.TextValue
                                        )
                                    ),
                                    new NumberReturning(
                                        new NumberScalar(markerTotal)
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedCount = userRows.Sum(user =>
            orderRows.Count(order =>
                order.OrderUserId == user.UserId
                || order.OrderTotal == markerTotal
            )
        );

        Assert.Equal(expectedCount, result.Count);
    }

    [Fact]
    public void InnerJoinOnNegatedKeyEqualityKeepsOnlyNonMatchingPairs()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = UsersJoinedToOrders(
            JoinType.Inner,
            new BooleanArrayReturning(new EachNotOperator(UserKeyMatch()))
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedCount = userRows.Sum(user =>
            orderRows.Count(order => order.OrderUserId != user.UserId)
        );

        Assert.Equal(expectedCount, result.Count);
    }
}
