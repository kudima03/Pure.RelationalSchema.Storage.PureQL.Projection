using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
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
            SampleDatabase.Users.ShiftStart,
            SampleDatabase.Users.Id,
            SampleDatabase.Users.LastLogin,
            SampleDatabase.Users.Name,
            SampleDatabase.Users.SignupDate,
            SampleDatabase.Users.Active,
            SampleDatabase.Users.Age,
        ];

        for (int iteration = 0; iteration < 25; iteration++)
        {
            SampleDatabase db = new SampleDatabase();

            Query query = new Query(
                new FromExpression(SampleDatabase.Users.Entity),
                [
                    new SelectExpression(
                        new ArrayReturning(
                            new TimeArrayReturning(
                                new TimeField(
                                    SampleDatabase.Users.Entity,
                                    SampleDatabase.Users.ShiftStart
                                )
                            )
                        )
                    ),
                    new SelectExpression(
                        new ArrayReturning(
                            new UuidArrayReturning(
                                new UuidField(
                                    SampleDatabase.Users.Entity,
                                    SampleDatabase.Users.Id
                                )
                            )
                        )
                    ),
                    new SelectExpression(
                        new ArrayReturning(
                            new DateTimeArrayReturning(
                                new DateTimeField(
                                    SampleDatabase.Users.Entity,
                                    SampleDatabase.Users.LastLogin
                                )
                            )
                        )
                    ),
                    new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(
                                    SampleDatabase.Users.Entity,
                                    SampleDatabase.Users.Name
                                )
                            )
                        )
                    ),
                    new SelectExpression(
                        new ArrayReturning(
                            new DateArrayReturning(
                                new DateField(
                                    SampleDatabase.Users.Entity,
                                    SampleDatabase.Users.SignupDate
                                )
                            )
                        )
                    ),
                    new SelectExpression(
                        new ArrayReturning(
                            new BooleanArrayReturning(
                                new BooleanField(
                                    SampleDatabase.Users.Entity,
                                    SampleDatabase.Users.Active
                                )
                            )
                        )
                    ),
                    new SelectExpression(
                        new ArrayReturning(
                            new NumberArrayReturning(
                                new NumberField(
                                    SampleDatabase.Users.Entity,
                                    SampleDatabase.Users.Age
                                )
                            )
                        )
                    ),
                ]
            );

            ProjectionResult result = new ProjectionResult(
                new PureQLProjection(db.Datasets, query)
            );

            Assert.Equal(expectedOrder, result.ColumnNames);

            foreach (ResultRow row in result.Rows)
            {
                Assert.Equal(expectedOrder, row.ColumnNames);
            }
        }
    }
}
