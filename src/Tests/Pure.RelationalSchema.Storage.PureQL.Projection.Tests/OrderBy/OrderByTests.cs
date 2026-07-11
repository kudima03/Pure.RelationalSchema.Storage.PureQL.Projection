using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.OrderBy;

// ORDER BY across each value type, ascending and descending, plus a stable
// multi-key sort. Expected sequences are produced by the equivalent stable
// LINQ ordering over the ground-truth records.
[Trait("Clause", "OrderBy")]
[Trait("Feature", "OrderBy")]
public sealed class OrderByTests
{
    [Fact]
    public void OrderByNumberAscendingSortsRowsLowToHigh()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Total
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.OrderRows.OrderBy(order => order.OrderTotal)
                .Select(order => (double?)order.OrderTotal)
                .ToArray(),
            [.. result.Rows.Select(row => row.Double(SampleDatabase.Orders.Total))]
        );
    }

    [Fact]
    public void OrderByNumberDescendingSortsRowsHighToLow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Total
                        )
                    ),
                    SortDirection.Desc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.OrderRows.OrderByDescending(order => order.OrderTotal)
                .Select(order => (double?)order.OrderTotal)
                .ToArray(),
            [.. result.Rows.Select(row => row.Double(SampleDatabase.Orders.Total))]
        );
    }

    [Fact]
    public void OrderByStringAscendingSortsRowsAlphabetically()
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
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new StringField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Name
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.UserRows.OrderBy(user => user.UserName)
                .Select(user => user.UserName)
                .ToArray(),
            result.Column(SampleDatabase.Users.Name).ToArray()
        );
    }

    [Fact]
    public void OrderByDateDescendingSortsRowsLatestFirst()
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
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new DateField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.SignupDate
                        )
                    ),
                    SortDirection.Desc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.UserRows.OrderByDescending(user => user.SignupDate)
                .Select(user => (DateOnly?)user.SignupDate)
                .ToArray(),
            [.. result.Rows.Select(row => row.Date(SampleDatabase.Users.SignupDate))]
        );
    }

    [Fact]
    public void OrderByDateTimeAscendingSortsRowsEarliestFirst()
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
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new DateTimeField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.LastLogin
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.UserRows.OrderBy(user => user.LastLogin)
                .Select(user => (DateTime?)user.LastLogin)
                .ToArray(),
            [.. result.Rows.Select(row => row.DateTime(SampleDatabase.Users.LastLogin))]
        );
    }

    [Fact]
    public void OrderByTimeAscendingSortsRowsEarliestFirst()
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
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new TimeField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.ShiftStart
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.UserRows.OrderBy(user => user.ShiftStart)
                .Select(user => (TimeOnly?)user.ShiftStart)
                .ToArray(),
            [.. result.Rows.Select(row => row.Time(SampleDatabase.Users.ShiftStart))]
        );
    }

    [Fact]
    public void OrderByUuidAscendingMatchesGuidComparerOrdering()
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
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new UuidField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Id
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.UserRows.OrderBy(user => user.UserId)
                .Select(user => (Guid?)user.UserId)
                .ToArray(),
            [.. result.Rows.Select(row => row.Uuid(SampleDatabase.Users.Id))]
        );
    }

    [Fact]
    public void OrderByTwoKeysAppliesStableSecondaryOrdering()
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
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Age
                        )
                    ),
                    SortDirection.Asc
                ),
                new OrderByItem(
                    new Field(
                        new StringField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Name
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.UserRows.OrderBy(user => user.UserAge)
                .ThenBy(user => user.UserName)
                .Select(user => user.UserName)
                .ToArray(),
            result.Column(SampleDatabase.Users.Name).ToArray()
        );
    }
}
