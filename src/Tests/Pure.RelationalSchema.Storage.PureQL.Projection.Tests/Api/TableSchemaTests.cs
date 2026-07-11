using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Api;

// PureQLProjection.TableSchema is the derived output schema. Its columns follow
// the select expressions: one column per expression, named by the select alias
// and typed by the expression's value type.
[Trait("Clause", "Select")]
[Trait("Feature", "TableSchema")]
public sealed class TableSchemaTests
{
    [Fact]
    public void TableSchemaColumnsFollowTheAliasedSelectExpressions()
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
                    "oid"
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

        PureQLProjection projection = new PureQLProjection(db.Datasets, query);

        IColumn[] columns = [.. projection.TableSchema.Columns];

        Assert.Equal(
            ["oid", "state", "amount"],
            [.. columns.Select(column => column.Name.TextValue)]
        );
        Assert.Equal(
            ["uuid", "string", "double"],
            [.. columns.Select(column => column.Type.Name.TextValue)]
        );
    }
}
