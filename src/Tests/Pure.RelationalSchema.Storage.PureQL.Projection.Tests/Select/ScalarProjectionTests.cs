using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// Scalar select expressions (SELECT 5 AS version FROM t) project a constant
// cell repeated on every output row — standard SQL semantics. Covers all
// seven scalar types, aliasing, mixing with field columns, and interaction
// with WHERE, DISTINCT and pagination.
[Trait("Clause", "Select")]
[Trait("Feature", "ScalarProjection")]
public sealed class ScalarProjectionTests
{
    [Fact]
    public void NumberScalarProjectsConstantOnEveryRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(new NumberScalar(5))
                    ),
                    "version"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(db.UserRows.Count, result.Count);
        Assert.All(result.Rows, row => Assert.Equal(5, row.Double("version")));
    }

    [Fact]
    public void AllSevenScalarTypesProjectTypedConstants()
    {
        SampleDatabase db = new SampleDatabase();

        Guid marker = new Guid("0f8fad5b-d9cb-469f-a165-70867728950e");
        DateOnly release = new DateOnly(2024, 12, 31);
        DateTime builtAt = new DateTime(2024, 12, 31, 23, 59, 58);
        TimeOnly cutoff = new TimeOnly(17, 30, 15);

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new BooleanReturning(new BooleanScalar(true))
                    ),
                    "active"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new DateReturning(new DateScalar(release))
                    ),
                    "release"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new DateTimeReturning(new DateTimeScalar(builtAt))
                    ),
                    "built_at"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(new NumberScalar(42.5))
                    ),
                    "amount"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new StringReturning(new StringScalar("v2"))
                    ),
                    "label"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new TimeReturning(new TimeScalar(cutoff))
                    ),
                    "cutoff"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new UuidReturning(new UuidScalar(marker))
                    ),
                    "marker"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(db.UserRows.Count, result.Count);
        Assert.All(
            result.Rows,
            row =>
            {
                Assert.Equal(true, row.Bool("active"));
                Assert.Equal(release, row.Date("release"));
                Assert.Equal(builtAt, row.DateTime("built_at"));
                Assert.Equal(42.5, row.Double("amount"));
                Assert.Equal("v2", row["label"]);
                Assert.Equal(cutoff, row.Time("cutoff"));
                Assert.Equal(marker, row.Uuid("marker"));
            }
        );
    }

    [Fact]
    public void ScalarAlongsideFieldColumnRepeatsPerRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new StringReturning(new StringScalar("v2"))
                    ),
                    "release"
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
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(db.UserRows.Count, result.Count);

        for (int i = 0; i < db.UserRows.Count; i++)
        {
            Assert.Equal("v2", result.Row(i)["release"]);
            Assert.Equal(
                db.UserRows[i].UserName,
                result.Row(i)[SampleDatabase.Users.Name]
            );
        }
    }

    [Fact]
    public void ScalarWithoutAliasProjectsEmptyNamedColumn()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(new NumberScalar(7))
                    )
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(db.UserRows.Count, result.Count);
        Assert.Contains(string.Empty, result.ColumnNames);
        Assert.All(result.Rows, row => Assert.Equal(7, row.Double(string.Empty)));
    }

    [Fact]
    public void ScalarUnderWhereRepeatsOnlyOnFilteredRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new StringReturning(new StringScalar("active-user"))
                    ),
                    "tag"
                ),
            ],
            new BooleanArrayReturning(
                new BooleanField(
                    SampleDatabase.Users.Entity,
                    SampleDatabase.Users.Active
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

        Assert.Equal(db.UserRows.Count(user => user.UserActive), result.Count);
        Assert.All(result.Rows, row => Assert.Equal("active-user", row["tag"]));
    }

    [Fact]
    public void DistinctCollapsesIdenticalScalarOnlyRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new StringReturning(new StringScalar("tag"))
                    ),
                    "tag"
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

        Assert.Equal(1, result.Count);
        Assert.Equal("tag", result.Row(0)["tag"]);
    }

    [Fact]
    public void ScalarWithPaginationProjectsConstantOnPagedRows()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(new NumberScalar(9))
                    ),
                    "page_marker"
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            new global::PureQL.CSharp.Model.Pagination(1, 2)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(2, result.Count);
        Assert.All(result.Rows, row => Assert.Equal(9, row.Double("page_marker")));
    }

    [Fact]
    public void NegativeFractionalNumberScalarRoundTrips()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Products.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(new NumberScalar(-12.75))
                    ),
                    "adjustment"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(db.ProductRows.Count, result.Count);
        Assert.All(result.Rows, row => Assert.Equal(-12.75, row.Double("adjustment")));
    }

    [Fact]
    public void ScalarProjectsConstantOnEveryJoinedRow()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new StringReturning(new StringScalar("joined"))
                    ),
                    "source"
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
            ],
            where: null,
            [
                new Join(
                    JoinType.Inner,
                    SampleDatabase.Users.Entity,
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        SampleDatabase.Orders.Entity,
                                        SampleDatabase.Orders.UserId
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        SampleDatabase.Users.Entity,
                                        SampleDatabase.Users.Id
                                    )
                                )
                            )
                        )
                    )
                ),
            ],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(db.OrderRows.Count, result.Count);
        Assert.All(result.Rows, row => Assert.Equal("joined", row["source"]));
    }

    [Fact]
    public void FalseBooleanScalarRoundTripsDistinctFromEmpty()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new BooleanReturning(new BooleanScalar(false))
                    ),
                    "flag"
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(db.UserRows.Count, result.Count);
        Assert.All(result.Rows, row => Assert.Equal(false, row.Bool("flag")));
    }
}
