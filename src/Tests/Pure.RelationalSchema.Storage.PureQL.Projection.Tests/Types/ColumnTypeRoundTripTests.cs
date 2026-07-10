using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Types;

// Round-trips every stored column type through selection: the projected cell
// text must parse back to the ground-truth typed value for each of the seven
// PureQL value types. This also proves the CellText formatter and the
// translator's CellValueExtractor agree.
[Trait("Clause", "Select")]
[Trait("Feature", "ColumnTypeRoundTrip")]
public sealed class ColumnTypeRoundTripTests
{
    [Fact]
    public void UuidColumnRoundTrips()
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

    [Fact]
    public void StringColumnRoundTrips()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
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
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.UserRows.Select(user => user.UserName).ToArray(),
            result.Column(SampleDatabase.Users.Name).ToArray()
        );
    }

    [Fact]
    public void DoubleColumnRoundTrips()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
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

        Assert.Equal(
            db.UserRows.Select(user => (double?)user.UserAge).ToArray(),
            [.. result.Rows.Select(row => row.Double(SampleDatabase.Users.Age))]
        );
    }

    [Fact]
    public void BoolColumnRoundTrips()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
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
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.UserRows.Select(user => (bool?)user.UserActive).ToArray(),
            [.. result.Rows.Select(row => row.Bool(SampleDatabase.Users.Active))]
        );
    }

    [Fact]
    public void DateColumnRoundTrips()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
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
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.UserRows.Select(user => (DateOnly?)user.SignupDate).ToArray(),
            [.. result.Rows.Select(row => row.Date(SampleDatabase.Users.SignupDate))]
        );
    }

    [Fact]
    public void DateTimeColumnRoundTrips()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
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
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.UserRows.Select(user => (DateTime?)user.LastLogin).ToArray(),
            [.. result.Rows.Select(row => row.DateTime(SampleDatabase.Users.LastLogin))]
        );
    }

    [Fact]
    public void TimeColumnRoundTrips()
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
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.UserRows.Select(user => (TimeOnly?)user.ShiftStart).ToArray(),
            [.. result.Rows.Select(row => row.Time(SampleDatabase.Users.ShiftStart))]
        );
    }
}
