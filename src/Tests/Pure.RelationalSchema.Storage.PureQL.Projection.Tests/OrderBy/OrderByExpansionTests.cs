using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.OrderBy;

// Deeper ORDER BY coverage: mixed-direction multi-key sorts with 3+ keys,
// many-key (4-5) composites, stability at full-tie depth, and ordering by an
// aliased select column. Expected sequences are produced by the equivalent
// stable LINQ ordering over the ground-truth records.
[Trait("Clause", "OrderBy")]
[Trait("Feature", "OrderByExpansion")]
public sealed class OrderByExpansionTests
{
    [Fact]
    public void OrderByActiveAscAgeDescNameAscOrdersThreeMixedDirectionKeys()
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
                        new BooleanField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Active
                        )
                    ),
                    SortDirection.Asc
                ),
                new OrderByItem(
                    new Field(
                        new NumberField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Age
                        )
                    ),
                    SortDirection.Desc
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

        string[] expected =
        [
            .. db.UserRows.OrderBy(user => user.UserActive)
                .ThenByDescending(user => user.UserAge)
                .ThenBy(user => user.UserName)
                .Select(user => user.UserName),
        ];

        string?[] actual = [.. result.Column(SampleDatabase.Users.Name)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OrderByFiveKeysAppliesFullCompositeOrdering()
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
                        new BooleanField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Active
                        )
                    ),
                    SortDirection.Asc
                ),
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
                        new DateField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.SignupDate
                        )
                    ),
                    SortDirection.Asc
                ),
                new OrderByItem(
                    new Field(
                        new DateTimeField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.LastLogin
                        )
                    ),
                    SortDirection.Desc
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

        string[] expected =
        [
            .. db.UserRows.OrderBy(user => user.UserActive)
                .ThenBy(user => user.UserAge)
                .ThenBy(user => user.SignupDate)
                .ThenByDescending(user => user.LastLogin)
                .ThenBy(user => user.UserName)
                .Select(user => user.UserName),
        ];

        string?[] actual = [.. result.Column(SampleDatabase.Users.Name)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OrderByFullTieOnEveryKeyPreservesOriginalRelativeOrder()
    {
        SampleDatabase db = new SampleDatabase();

        // Ann and Fay share SignupDate, LastLogin and ShiftStart exactly, so
        // ordering by that triple leaves zero distinguishing keys between
        // them: a stable sort must keep Ann (declared first) ahead of Fay
        // (declared last) in the output.
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
                        new DateField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.SignupDate
                        )
                    ),
                    SortDirection.Asc
                ),
                new OrderByItem(
                    new Field(
                        new DateTimeField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.LastLogin
                        )
                    ),
                    SortDirection.Asc
                ),
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

        string[] expected =
        [
            .. db.UserRows.OrderBy(user => user.SignupDate)
                .ThenBy(user => user.LastLogin)
                .ThenBy(user => user.ShiftStart)
                .Select(user => user.UserName),
        ];

        string?[] actual = [.. result.Column(SampleDatabase.Users.Name)];

        Assert.Equal(expected, actual);
        Assert.Contains("Ann", expected);
        Assert.Contains("Fay", expected);
        Assert.True(
            Array.IndexOf(expected, "Ann") < Array.IndexOf(expected, "Fay"),
            "Ground truth must exercise a full tie with Ann before Fay."
        );
    }

    [Fact]
    public void OrderByAliasedSelectColumnStillOrdersByUnderlyingField()
    {
        SampleDatabase db = new SampleDatabase();

        // The select expression renames order_total to "grandTotal", but the
        // ORDER BY item still refers to the underlying field (there is no
        // alias reference in the model) and must keep sorting by it.
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
                    ),
                    "grandTotal"
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

        double?[] expected =
        [
            .. db.OrderRows.OrderBy(order => order.OrderTotal)
                .Select(order => (double?)order.OrderTotal),
        ];

        double?[] actual = [.. result.Rows.Select(row => row.Double("grandTotal"))];

        Assert.Equal(expected, actual);
    }

    // KnownGap: rows produced by an unmatched LEFT JOIN side carry an empty
    // (NULL-equivalent) cell for the joined column. SQL engines commonly
    // default ascending ORDER BY to NULLS LAST (e.g. PostgreSQL), but
    // OrderByApplicator sorts via C#'s Nullable<T> comparison, where null
    // compares less than every value, so unmatched rows land first instead.
    // The query language has no NULLS FIRST/LAST clause to override this, so
    // NULL placement is undefined/unspecified by the library today.
    [Fact(
        Skip = "KnownGap: ascending ORDER BY over a nullable (unmatched "
            + "LEFT JOIN) column places NULLs first, following C#'s "
            + "Nullable<T> comparer, instead of NULLS LAST as many SQL "
            + "engines default; the query model has no NULLS FIRST/LAST "
            + "control to pin the intended behavior either way."
    )]
    [Trait("Status", "KnownGap")]
    public void OrderByAscendingWithUnmatchedLeftJoinRowsPlacesNullsLast()
    {
        SampleDatabase db = new SampleDatabase();

        Join usersToOrders = new Join(
            JoinType.Left,
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
            [usersToOrders],
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

        // Eve and Fay place no orders, so their joined Total is NULL.
        // NULLS LAST: matched rows sorted ascending first, then the
        // unmatched users trailing in their original relative order.
        string[] expected =
        [
            "Ann",
            "Cara",
            "Ann",
            "Dan",
            "Bob",
            "Cara",
            "Eve",
            "Fay",
        ];

        string?[] actual = [.. result.Column(SampleDatabase.Users.Name)];

        Assert.Equal(expected, actual);
    }

    // KnownGap: the pipeline applies ORDER BY before GROUP BY (see
    // RowsFromDatasets.Build), so an OrderByItem is evaluated against raw,
    // pre-aggregation rows. An aggregate result (e.g. SUM(total) AS
    // totalSum) only exists on the projected, post-group row - there is no
    // raw column named "totalSum" for CellValueExtractor to resolve - so
    // ordering by it is a silent no-op today instead of ordering the
    // emitted groups by their aggregate value, as SQL's ORDER BY-after-
    // GROUP BY semantics require.
    [Fact(
        Skip = "KnownGap: ORDER BY runs before GROUP BY in the pipeline, so "
            + "an OrderByItem referencing an aggregate's alias resolves "
            + "against pre-aggregation rows (no such column exists there) "
            + "and silently no-ops instead of ordering the emitted groups "
            + "by their aggregate result."
    )]
    [Trait("Status", "KnownGap")]
    public void OrderByAggregateResultOrdersEmittedGroupsByItsValue()
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
                    "totalSum"
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
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(SampleDatabase.Orders.Entity, "totalSum")
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        // Ascending by summed total per status: cancelled (75.25) <
        // pending (150.50) < shipped (600.50).
        string[] expected = ["cancelled", "pending", "shipped"];

        string?[] actual = [.. result.Column(SampleDatabase.Orders.Status)];

        Assert.Equal(expected, actual);
    }
}
