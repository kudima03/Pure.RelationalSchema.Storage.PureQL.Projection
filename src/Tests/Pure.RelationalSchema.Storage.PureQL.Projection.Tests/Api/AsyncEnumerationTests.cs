using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
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
            ]
        );

        PureQLProjection projection = new PureQLProjection(db.Datasets, query);

        List<string?> statuses = [];
        await foreach (IRow row in projection)
        {
            foreach (KeyValuePair<IColumn, ICell> cell in row.Cells)
            {
                if (cell.Key.Name.TextValue == SampleDatabase.Orders.Status)
                {
                    statuses.Add(cell.Value.Value.TextValue);
                }
            }
        }

        Assert.Equal(
            db.OrderRows.Select(order => order.OrderStatus).ToArray(),
            statuses.ToArray()
        );
    }
}
