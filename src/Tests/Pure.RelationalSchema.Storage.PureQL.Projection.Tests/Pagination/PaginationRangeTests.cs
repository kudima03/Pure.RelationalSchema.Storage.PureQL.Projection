using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;
using ModelPagination = PureQL.CSharp.Model.Pagination;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Pagination;

// Pagination carries skip/take as Int64, but they are applied through an
// unchecked cast to Int32, so values beyond int.MaxValue wrap and silently
// produce the wrong window (issue #85).
[Trait("Clause", "Pagination")]
[Trait("Feature", "Range")]
public sealed class PaginationRangeTests
{
    private static Query AllUserNames(ModelPagination pagination)
    {
        return new Query(
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
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination
        );
    }

    [Fact]
    public void TakeBeyondIntMaxReturnsEveryRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(
                datasets,
                AllUserNames(new ModelPagination(0, long.MaxValue))
            )
        );

        Assert.Equal(userRows.Count, result.Count);
    }

    [Fact]
    public void SkipBeyondIntMaxReturnsNoRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(
                datasets,
                AllUserNames(new ModelPagination(long.MaxValue, 1))
            )
        );

        Assert.Equal(0, result.Count);
    }
}
