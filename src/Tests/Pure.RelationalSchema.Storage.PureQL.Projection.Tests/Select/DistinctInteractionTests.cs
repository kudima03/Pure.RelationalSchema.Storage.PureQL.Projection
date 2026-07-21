using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using ModelPagination = PureQL.CSharp.Model.Pagination;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// DISTINCT interacting with the rest of the pipeline: multi-column tuple
// dedup, join fan-out collapse, ordering/pagination interplay, aliasing, and
// the remaining typed columns of the seven-type matrix (date/number/uuid/
// time/datetime; string and bool are covered by DistinctTests).
[Trait("Clause", "Select")]
[Trait("Feature", "Distinct")]
public sealed class DistinctInteractionTests
{
    private static Join ItemsToProductsJoin()
    {
        return new Join(
            JoinType.Inner,
            SampleDatabase.Products.Entity,
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.OrderItems.Entity,
                                SampleDatabase.OrderItems.ProductId
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Products.Entity,
                                SampleDatabase.Products.Id
                            )
                        )
                    )
                )
            )
        );
    }

    [Fact]
    public void DistinctOnMultiColumnProjectionDeduplicatesTuples()
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
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null,
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        (double, bool)[] expected =
        [
            .. db.UserRows
                .Select(user => (user.UserAge, user.UserActive))
                .Distinct()
                .OrderBy(pair => pair.UserAge)
                .ThenBy(pair => pair.UserActive),
        ];

        (double, bool)[] actual =
        [
            .. result.Rows
                .Select(row =>
                    (
                        row.Double(SampleDatabase.Users.Age)!.Value,
                        row.Bool(SampleDatabase.Users.Active)!.Value
                    )
                )
                .OrderBy(pair => pair.Item1)
                .ThenBy(pair => pair.Item2),
        ];

        Assert.True(expected.Length < db.UserRows.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DistinctCollapsesJoinFanOutDuplicates()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.OrderItems.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.OrderItems.Entity,
                                SampleDatabase.OrderItems.OrderId
                            )
                        )
                    )
                ),
            ],
            where: null,
            [ItemsToProductsJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null,
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Guid[] expected =
        [
            .. db.OrderItemRows
                .Select(item => item.ItemOrderId)
                .Distinct()
                .OrderBy(id => id),
        ];

        Guid[] actual =
        [
            .. result.Column(SampleDatabase.OrderItems.OrderId)
                .Select(text => Guid.Parse(text!))
                .OrderBy(id => id),
        ];

        Assert.True(expected.Length < db.OrderItemRows.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DistinctPreservesFirstOccurrenceOrderAfterOrderBy()
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
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Status
                        )
                    ),
                    SortDirection.Desc
                ),
            ],
            pagination: null,
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.OrderRows
                .Select(order => order.OrderStatus)
                .OrderByDescending(status => status, StringComparer.Ordinal)
                .Distinct(),
        ];

        string?[] actual = [.. result.Column(SampleDatabase.Orders.Status)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DistinctAppliesBeforePagination()
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
                            SampleDatabase.Orders.Entity,
                            SampleDatabase.Orders.Status
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            new ModelPagination(0, 2),
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.OrderRows
                .Select(order => order.OrderStatus)
                .Distinct()
                .OrderBy(status => status, StringComparer.Ordinal)
                .Take(2),
        ];

        string?[] actual = [.. result.Column(SampleDatabase.Orders.Status)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void DistinctOnAliasedColumnDeduplicatesProjectedValues()
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
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null,
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Contains("state", result.ColumnNames);
        Assert.Equal(
            db.OrderRows.Select(order => order.OrderStatus).Distinct().Count(),
            result.Count
        );
    }

    [Fact]
    public void DistinctOnDateColumnCollapsesDuplicateValues()
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
            orderBy: null,
            pagination: null,
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int expectedDistinct = db.UserRows
            .Select(user => user.SignupDate)
            .Distinct()
            .Count();

        Assert.True(expectedDistinct < db.UserRows.Count);
        Assert.Equal(expectedDistinct, result.Count);
    }

    [Fact]
    public void DistinctOnNumberColumnCollapsesDuplicateValues()
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
            orderBy: null,
            pagination: null,
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int expectedDistinct = db.OrderRows
            .Select(order => order.OrderTotal)
            .Distinct()
            .Count();

        Assert.True(expectedDistinct < db.OrderRows.Count);
        Assert.Equal(expectedDistinct, result.Count);
    }

    [Fact]
    public void DistinctOnUuidColumnCollapsesDuplicateValues()
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
                                SampleDatabase.Orders.UserId
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null,
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int expectedDistinct = db.OrderRows
            .Select(order => order.OrderUserId)
            .Distinct()
            .Count();

        Assert.True(expectedDistinct < db.OrderRows.Count);
        Assert.Equal(expectedDistinct, result.Count);
    }

    [Fact]
    public void DistinctOnTimeColumnCollapsesDuplicateValues()
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
            orderBy: null,
            pagination: null,
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int expectedDistinct = db.UserRows
            .Select(user => user.ShiftStart)
            .Distinct()
            .Count();

        Assert.True(expectedDistinct < db.UserRows.Count);
        Assert.Equal(expectedDistinct, result.Count);
    }

    [Fact]
    public void DistinctOnDateTimeColumnCollapsesDuplicateValues()
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
            orderBy: null,
            pagination: null,
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int expectedDistinct = db.UserRows
            .Select(user => user.LastLogin)
            .Distinct()
            .Count();

        Assert.True(expectedDistinct < db.UserRows.Count);
        Assert.Equal(expectedDistinct, result.Count);
    }
}
