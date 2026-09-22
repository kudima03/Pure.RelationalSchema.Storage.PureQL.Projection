using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

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
            "shift_start",
            "user_id",
            "last_login",
            "user_name",
            "signup_date",
            "user_active",
            "user_age",
        ];

        for (int iteration = 0; iteration < 25; iteration++)
        {
            IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

            Query query = new Query(
                new FromExpression("schema_with_foreign_keys.users"),
                [
                    new SelectExpression(
                        new ArrayReturning(
                            new TimeArrayReturning(
                                new TimeField(
                                    "schema_with_foreign_keys.users",
                                    "shift_start"
                                )
                            )
                        )
                    ),
                    new SelectExpression(
                        new ArrayReturning(
                            new UuidArrayReturning(
                                new UuidField(
                                    "schema_with_foreign_keys.users",
                                    "user_id"
                                )
                            )
                        )
                    ),
                    new SelectExpression(
                        new ArrayReturning(
                            new DateTimeArrayReturning(
                                new DateTimeField(
                                    "schema_with_foreign_keys.users",
                                    "last_login"
                                )
                            )
                        )
                    ),
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
                    new SelectExpression(
                        new ArrayReturning(
                            new DateArrayReturning(
                                new DateField(
                                    "schema_with_foreign_keys.users",
                                    "signup_date"
                                )
                            )
                        )
                    ),
                    new SelectExpression(
                        new ArrayReturning(
                            new BooleanArrayReturning(
                                new BooleanField(
                                    "schema_with_foreign_keys.users",
                                    "user_active"
                                )
                            )
                        )
                    ),
                    new SelectExpression(
                        new ArrayReturning(
                            new NumberArrayReturning(
                                new NumberField(
                                    "schema_with_foreign_keys.users",
                                    "user_age"
                                )
                            )
                        )
                    ),
                ]
            );

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
