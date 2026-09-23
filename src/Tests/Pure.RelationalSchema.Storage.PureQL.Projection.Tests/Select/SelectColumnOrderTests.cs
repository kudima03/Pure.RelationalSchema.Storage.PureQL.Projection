using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Select;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// Regression coverage for issue #107: ProjectionResult/ResultRow.ColumnNames
// must reflect select-expression order, not the enumeration order of the
// underlying row.Cells dictionary (which lazily materializes a
// FrozenDictionary whose key order is unspecified). Selecting all seven typed
// columns in an order deliberately different from both their declaration
// order and their apparent hash order, and repeating the run many times,
// would have exposed the bug if ColumnNames were derived from cell-map
// enumeration instead of the query's select order.
[Trait("Clause", "Select")]
[Trait("Feature", "SelectColumnOrder")]
public sealed class SelectColumnOrderTests
{
    [Fact]
    public void SelectManyColumnsInShuffledOrderPreservesSelectExpressionOrder()
    {
        string[] expectedOrder =
        [
            new ShiftStartColumn().Name.TextValue,
            new UserIdColumn().Name.TextValue,
            new LastLoginColumn().Name.TextValue,
            new UserNameColumn().Name.TextValue,
            new SignupDateColumn().Name.TextValue,
            new UserActiveColumn().Name.TextValue,
            new UserAgeColumn().Name.TextValue,
        ];

        for (int iteration = 0; iteration < 25; iteration++)
        {
            IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

            Query query = new SelectManyColumnsInShuffledOrderQuery().Value;

            ProjectionResult result = new ProjectionResult(
                new PureQLProjection(datasets, query)
            );

            Assert.Equal(expectedOrder, result.ColumnNames);

            foreach (ResultRow row in result.Rows)
            {
                Assert.Equal(expectedOrder, row.ColumnNames);
            }
        }
    }
}
