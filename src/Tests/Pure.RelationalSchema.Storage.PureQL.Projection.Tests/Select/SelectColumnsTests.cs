using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

[Trait("Clause", "Select")]
[Trait("Feature", "SelectColumns")]
public sealed class SelectColumnsTests
{
    [Fact]
    public void SelectSingleStringColumnReturnsThatColumnForEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Status)
                        )
                    )
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(db.OrderRows.Count, result.Count);
        Assert.Equal([SampleDatabase.Orders.Status], result.ColumnNames);
        Assert.Equal(
            [.. db.OrderRows.Select(order => order.OrderStatus)],
            result.Column(SampleDatabase.Orders.Status)
        );
    }

    [Fact]
    public void SelectMultipleColumnsProjectsAllOfThemPreservingRowOrder()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Status)
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.Total)
                        )
                    )
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(db.OrderRows.Count, result.Count);
        Assert.Contains(SampleDatabase.Orders.Status, result.ColumnNames);
        Assert.Contains(SampleDatabase.Orders.Total, result.ColumnNames);
        Assert.Equal(
            db.OrderRows.Select(order => (double?)order.OrderTotal).ToArray(),
            [.. result.Rows.Select(row => row.Double(SampleDatabase.Orders.Total))]
        );
    }

    [Fact]
    public void SelectUuidColumnRoundTripsEachIdentifier()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(SampleDatabase.Users.Entity, SampleDatabase.Users.Id)
                        )
                    )
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.UserRows.Select(user => (Guid?)user.UserId).ToArray(),
            [.. result.Rows.Select(row => row.Uuid(SampleDatabase.Users.Id))]
        );
    }
}
