using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// Aliases can cover every select item in a query, and an alias may collide
// with a different source column's name without the projected value being
// confused with that other column.
[Trait("Clause", "Select")]
[Trait("Feature", "SelectAlias")]
public sealed class AliasCoverageTests
{
    [Fact]
    public void AliasesOnEverySelectItemRenameAllColumns()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Id
                            )
                        )
                    ),
                    "id"
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        )
                    ),
                    "state"
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        )
                    ),
                    "amount"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(["id", "state", "amount"], result.ColumnNames);
        Assert.DoesNotContain(SampleDatabase.Orders.Id, result.ColumnNames);
        Assert.DoesNotContain(SampleDatabase.Orders.Status, result.ColumnNames);
        Assert.DoesNotContain(SampleDatabase.Orders.Total, result.ColumnNames);
    }

    [Fact]
    public void AliasEqualToAnotherFieldNameShadowsInProjection()
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
                    ),
                    SampleDatabase.Orders.Total
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal([SampleDatabase.Orders.Total], result.ColumnNames);

        string?[] expected = [.. db.OrderRows.Select(order => order.OrderStatus)];
        string?[] actual = [.. result.Column(SampleDatabase.Orders.Total)];

        Assert.Equal(expected, actual);
    }
}
