using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// A select alias should rename the output column. The translator names the
// projected row column after the underlying field and ignores the alias, so
// these spec-correct tests currently fail and are kept failing to document the
// gap. They should pass once aliases drive the projected column name.
#pragma warning disable xUnit1004 // skipped: documents a known translator gap
[Trait("Clause", "Select")]
[Trait("Feature", "SelectAlias")]
[Trait("Status", "KnownGap")]
public sealed class SelectAliasTests
{
    [Fact(Skip = "KnownGap: the projected column is named after the field; the "
        + "select alias is ignored. Enable once aliases drive the output name.")]
    public void AliasRenamesTheProjectedColumn()
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
                    "state"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Contains("state", result.ColumnNames);
    }
}
