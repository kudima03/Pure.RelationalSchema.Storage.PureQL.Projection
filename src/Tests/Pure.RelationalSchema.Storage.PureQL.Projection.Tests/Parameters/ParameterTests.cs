using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Parameters;

// PureQL parameters are placeholders bound to values at execution time. The
// PureQLProjection public API exposes no surface to supply parameter values, so
// there is no defined result for a parameterised query through this entry
// point; the translator fails fast with NotSupportedException instead of
// silently mis-binding. These tests pin that explicit-failure contract. When a
// binding API is added, they should be replaced with value-binding assertions.
[Trait("Clause", "Where")]
[Trait("Feature", "Parameter")]
public sealed class ParameterTests
{
    [Fact]
    public void StringParameterInEachEqualityFailsFastWithoutBinding()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

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
            ],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachStringEquality(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        ),
                        new StringReturning(new StringParameter("status"))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(datasets, query)
        );
    }

    [Fact]
    public void NumberParameterInEachEqualityFailsFastWithoutBinding()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

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
            new BooleanArrayReturning(
                new EachEquality(
                    new EachNumberEquality(
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.users",
                                "user_age"
                            )
                        ),
                        new NumberReturning(new NumberParameter("age"))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(datasets, query)
        );
    }
}
