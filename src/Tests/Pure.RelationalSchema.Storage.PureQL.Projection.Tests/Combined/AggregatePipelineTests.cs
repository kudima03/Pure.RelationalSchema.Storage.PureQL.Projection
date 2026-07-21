using System.Globalization;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.Aggregates.String;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;
using ModelPagination = PureQL.CSharp.Model.Pagination;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Combined;

// Aggregates combined with the rest of the pipeline: WHERE narrows the rows
// folded by GROUP BY, joins (same-schema and cross-schema) feed aggregates,
// and whole-set aggregates (no groupBy) compose with a preceding WHERE.
[Trait("Clause", "Combined")]
[Trait("Feature", "AggregatePipeline")]
public sealed class AggregatePipelineTests
{
    private static Join UsersToOrdersJoin()
    {
        return new Join(
            JoinType.Inner,
            SampleDatabase.Orders.Entity,
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Id
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.UserId
                            )
                        )
                    )
                )
            )
        );
    }

    private static Join UsersToLoginsJoin()
    {
        return new Join(
            JoinType.Inner,
            SampleDatabase.Logins.Entity,
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Id
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Logins.Entity,
                                SampleDatabase.Logins.UserId
                            )
                        )
                    )
                )
            )
        );
    }

    [Fact]
    public void WhereThenGroupByAggregatesOnlyFilteredRows()
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
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Total
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "filteredSum"
                ),
            ],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachStringEquality(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        ),
                        new StringReturning(new StringScalar("shipped"))
                    )
                )
            ),
            join: null,
            [
                new Field(
                    new UuidField(
                        SampleDatabase.Orders.Entity,
                        SampleDatabase.Orders.UserId
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Dictionary<Guid, double> expected = db.OrderRows
            .Where(order => order.OrderStatus == "shipped")
            .GroupBy(order => order.OrderUserId)
            .ToDictionary(group => group.Key, group => group.Sum(order => order.OrderTotal));

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(SampleDatabase.Orders.UserId)!.Value,
            row => row.Double("filteredSum")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void JoinThenGroupByProjectsAggregatePerJoinedGroup()
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
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new UuidArrayReturning(
                                        new UuidField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Id
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "orderCount"
                ),
            ],
            where: null,
            [UsersToOrdersJoin()],
            [
                new Field(
                    new StringField(
                        SampleDatabase.Users.Entity,
                        SampleDatabase.Users.Name
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Dictionary<string, double> expected = db.OrderRows
            .Join(
                db.UserRows,
                order => order.OrderUserId,
                user => user.UserId,
                (order, user) => user.UserName
            )
            .GroupBy(name => name)
            .ToDictionary(group => group.Key, group => (double)group.Count());

        Dictionary<string, double> actual = result.Rows.ToDictionary(
            row => row[SampleDatabase.Users.Name]!,
            row => row.Double("orderCount")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void CrossSchemaJoinThenGroupByCountsPerUser()
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
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new UuidArrayReturning(
                                        new UuidField(
                                            SampleDatabase.Logins.Entity,
                                            SampleDatabase.Logins.Id
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "loginCount"
                ),
            ],
            where: null,
            [UsersToLoginsJoin()],
            [
                new Field(
                    new UuidField(
                        SampleDatabase.Users.Entity,
                        SampleDatabase.Users.Id
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Dictionary<Guid, double> expected = db.LoginRows
            .GroupBy(login => login.LoginUserId)
            .ToDictionary(group => group.Key, group => (double)group.Count());

        Dictionary<Guid, double> actual = result.Rows.ToDictionary(
            row => row.Uuid(SampleDatabase.Users.Id)!.Value,
            row => row.Double("loginCount")!.Value
        );

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GroupByHavingOrderByPaginationComposeInOrder()
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
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Total
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "statusSum"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new StringField(
                        SampleDatabase.Orders.Entity,
                        SampleDatabase.Orders.Status
                    )
                ),
            ],
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThan,
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Total
                                        )
                                    )
                                )
                            )
                        ),
                        new NumberReturning(new NumberScalar(0))
                    )
                )
            ),
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
            new ModelPagination(1, 1)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        (string Status, double Sum)[] expected =
        [
            .. db.OrderRows
                .GroupBy(order => order.OrderStatus)
                .Where(group => group.Sum(order => order.OrderTotal) > 0)
                .OrderBy(group => group.Key, StringComparer.Ordinal)
                .Select(group => (group.Key, group.Sum(order => order.OrderTotal)))
                .Skip(1)
                .Take(1),
        ];

        (string Status, double Sum)[] actual =
        [
            .. result.Rows.Select(row =>
                (row[SampleDatabase.Orders.Status]!, row.Double("statusSum")!.Value)
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WholeSetAggregateOverFilteredRowsProjectsSingleRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Total
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "filteredSum"
                ),
            ],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachStringEquality(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        ),
                        new StringReturning(new StringScalar("shipped"))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double expected = db.OrderRows
            .Where(order => order.OrderStatus == "shipped")
            .Sum(order => order.OrderTotal);

        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0).Double("filteredSum"));
    }

    [Fact]
    public void WholeSetCountProjectsSingleRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new UuidArrayReturning(
                                        new UuidField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Id
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "total"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(db.OrderRows.Count, result.Row(0).Double("total"));
    }

    [Fact]
    public void WholeSetMinAndMaxStringProjectInOneRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new StringReturning(
                            new StringAggregate(
                                new MinString(
                                    new StringArrayReturning(
                                        new StringField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Status
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "minStatus"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new StringReturning(
                            new StringAggregate(
                                new MaxString(
                                    new StringArrayReturning(
                                        new StringField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Status
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "maxStatus"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string expectedMin = db.OrderRows
            .Select(order => order.OrderStatus)
            .Min(StringComparer.Ordinal)!;
        string expectedMax = db.OrderRows
            .Select(order => order.OrderStatus)
            .Max(StringComparer.Ordinal)!;

        Assert.Equal(1, result.Count);
        Assert.Equal(expectedMin, result.Row(0)["minStatus"]);
        Assert.Equal(expectedMax, result.Row(0)["maxStatus"]);
    }

    [Fact]
    public async Task AsyncEnumerationYieldsGroupedAggregateRows()
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
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Total
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "userTotal"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new UuidField(
                        SampleDatabase.Orders.Entity,
                        SampleDatabase.Orders.UserId
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        PureQLProjection projection = new PureQLProjection(db.Datasets, query);

        Dictionary<Guid, double> actual = [];
        await foreach (IRow row in projection)
        {
            Guid userId = default;
            double total = 0;
            foreach (KeyValuePair<IColumn, ICell> cell in row.Cells)
            {
                if (cell.Key.Name.TextValue == SampleDatabase.Orders.UserId)
                {
                    userId = Guid.Parse(cell.Value.Value.TextValue!);
                }
                else if (cell.Key.Name.TextValue == "userTotal")
                {
                    total = double.Parse(
                        cell.Value.Value.TextValue!,
                        CultureInfo.InvariantCulture
                    );
                }
            }

            actual[userId] = total;
        }

        Dictionary<Guid, double> expected = db.OrderRows
            .GroupBy(order => order.OrderUserId)
            .ToDictionary(group => group.Key, group => group.Sum(order => order.OrderTotal));

        Assert.Equal(expected, actual);
    }
}
