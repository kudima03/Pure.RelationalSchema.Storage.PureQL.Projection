using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// A LEFT join whose non-equi ON condition matches nothing: every left row is
// preserved exactly once (selecting only left-side columns is well-defined).
[Trait("Clause", "Join")]
[Trait("Feature", "OuterNonEquiJoin")]
public sealed class OuterNonEquiJoinTests
{
    [Fact]
    public void LeftJoinWithNonMatchingInequalityPreservesEveryLeftRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        // user_age (25..42) is never greater than order_total (50..300), so no
        // order matches any user and every user survives the left join once.
        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.users",
                                "user_name"
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Left,
                    "schema_with_foreign_keys.orders",
                    new BooleanArrayReturning(
                        new EachComparison(
                            new EachNumberComparison(
                                EachComparisonOperator.EachGreaterThan,
                                new NumberArrayReturning(
                                    new NumberField(
                                        "schema_with_foreign_keys.users",
                                        "user_age"
                                    )
                                ),
                                new NumberArrayReturning(
                                    new NumberField(
                                        "schema_with_foreign_keys.orders",
                                        "order_total"
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

        string[] expected =
        [
            .. userRows.Select(user => user.UserName).OrderBy(name => name),
        ];

        string?[] actual =
        [
            .. result.Column("user_name").OrderBy(name => name),
        ];

        Assert.Equal(userRows.Count, result.Count);
        Assert.Equal(expected, actual);
    }
}
