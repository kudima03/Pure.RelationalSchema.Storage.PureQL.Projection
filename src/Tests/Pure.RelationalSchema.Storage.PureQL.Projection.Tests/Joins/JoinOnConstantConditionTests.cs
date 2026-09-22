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
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// A join's on clause may be a single boolean-returning expression; constant
// conditions pin the degenerate join shapes: true yields the full cross
// product, false yields no matches (empty for INNER, fully padded for the
// outer types).
[Trait("Clause", "Join")]
[Trait("Feature", "ConstantCondition")]
public sealed class JoinOnConstantConditionTests
{
    private static Query UsersJoinedToProducts(JoinType joinType, bool condition)
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
                    [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
                ).TextValue,
                                new UserNameColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    joinType,
                    new JoinedString(
                    new DotString(),
                    [new RelationalSchemaWithForeignKeys().Name, new ProductsTable().Name]
                ).TextValue,
                    new BooleanReturning(new BooleanScalar(condition))
                ),
            ],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );
    }

    [Fact]
    public void InnerJoinOnConstantTrueProducesTheCrossProduct()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(
                datasets,
                UsersJoinedToProducts(JoinType.Inner, condition: true)
            )
        );

        Assert.Equal(userRows.Count * productRows.Count, result.Count);
    }

    [Fact]
    public void InnerJoinOnConstantFalseReturnsEmpty()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(
                datasets,
                UsersJoinedToProducts(JoinType.Inner, condition: false)
            )
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    public void LeftJoinOnConstantFalsePadsEveryLeftRowOnce()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(
                datasets,
                UsersJoinedToProducts(JoinType.Left, condition: false)
            )
        );

        Assert.Equal(userRows.Count, result.Count);

        string[] expected =
        [
            .. userRows.Select(user => user.UserName).OrderBy(name => name),
        ];

        Assert.Equal(
            expected,
            result.Column(new UserNameColumn().Name.TextValue).OrderBy(name => name).ToArray()
        );
    }

    [Fact]
    public void FullJoinOnConstantFalseKeepsEverySideUnmatched()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<ProductRecord> productRows = [.. new ProductRecords()];

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(
                datasets,
                UsersJoinedToProducts(JoinType.Full, condition: false)
            )
        );

        Assert.Equal(userRows.Count + productRows.Count, result.Count);
    }
}
