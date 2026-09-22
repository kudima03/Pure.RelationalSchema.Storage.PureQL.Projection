using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Api;

// PureQLProjection is IAsyncEnumerable<IRow>; enumerating it asynchronously must
// yield the same rows in the same order as the synchronous path.
[Trait("Clause", "Select")]
[Trait("Feature", "AsyncEnumeration")]
public sealed class AsyncEnumerationTests
{
    [Fact]
    public async Task AsyncEnumerationYieldsTheSameRowsAsGroundTruth()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    )
                ),
            ]
        );

        PureQLProjection projection = new PureQLProjection(datasets, query);

        List<string?> statuses = [];
        await foreach (IRow row in projection)
        {
            foreach (KeyValuePair<IColumn, ICell> cell in row.Cells)
            {
                if (cell.Key.Name.TextValue == "order_status")
                {
                    statuses.Add(cell.Value.Value.TextValue);
                }
            }
        }

        Assert.Equal(
            orderRows.Select(order => order.OrderStatus).ToArray(),
            statuses.ToArray()
        );
    }
}
