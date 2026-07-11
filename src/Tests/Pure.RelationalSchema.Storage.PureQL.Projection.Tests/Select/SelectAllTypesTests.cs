using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// Projecting all seven typed columns of a table in a single query: every column
// is present and each cell round-trips to its ground-truth typed value.
[Trait("Clause", "Select")]
[Trait("Feature", "SelectAllTypes")]
public sealed class SelectAllTypesTests
{
    [Fact]
    public void SelectAllUserColumnsProjectsEveryTypedColumn()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
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
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Age
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
                        new TimeArrayReturning(
                            new TimeField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.ShiftStart
                            )
                        )
                    )
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(db.UserRows.Count, result.Count);
        Assert.Contains(SampleDatabase.Users.Id, result.ColumnNames);
        Assert.Contains(SampleDatabase.Users.Name, result.ColumnNames);
        Assert.Contains(SampleDatabase.Users.Age, result.ColumnNames);
        Assert.Contains(SampleDatabase.Users.Active, result.ColumnNames);
        Assert.Contains(SampleDatabase.Users.SignupDate, result.ColumnNames);
        Assert.Contains(SampleDatabase.Users.LastLogin, result.ColumnNames);
        Assert.Contains(SampleDatabase.Users.ShiftStart, result.ColumnNames);

        UserRow first = db.UserRows[0];
        ResultRow row = result.Row(0);
        Assert.Equal(first.UserId, row.Uuid(SampleDatabase.Users.Id));
        Assert.Equal(first.UserName, row[SampleDatabase.Users.Name]);
        Assert.Equal(first.UserAge, row.Double(SampleDatabase.Users.Age));
        Assert.Equal(first.UserActive, row.Bool(SampleDatabase.Users.Active));
        Assert.Equal(first.SignupDate, row.Date(SampleDatabase.Users.SignupDate));
        Assert.Equal(first.LastLogin, row.DateTime(SampleDatabase.Users.LastLogin));
        Assert.Equal(first.ShiftStart, row.Time(SampleDatabase.Users.ShiftStart));
    }
}
