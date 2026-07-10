using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Fills out the each-comparison operator x type matrix: the remaining operators
// (LessThan, LessThanOrEqual, GreaterThanOrEqual) for string/date/time/datetime
// (GreaterThan is covered in EachComparisonTests).
[Trait("Clause", "Where")]
[Trait("Feature", "EachComparisonMore")]
public sealed class EachComparisonMoreTests
{
    [Fact]
    public void EachStringLessThanFiltersRowsBelowThreshold()
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
            new BooleanArrayReturning(
                new EachComparison(
                    new EachStringComparison(
                        EachComparisonOperator.EachLessThan,
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
                            )
                        ),
                        new StringReturning(new StringScalar("pending"))
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

        Assert.Equal(
            db.OrderRows.Count(order =>
                string.CompareOrdinal(order.OrderStatus, "pending") < 0
            ),
            result.Count
        );
    }

    [Fact]
    public void EachDateLessThanOrEqualIncludesThreshold()
    {
        SampleDatabase db = new SampleDatabase();
        DateOnly threshold = new DateOnly(2024, 6, 3);

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
            new BooleanArrayReturning(
                new EachComparison(
                    new EachDateComparison(
                        EachComparisonOperator.EachLessThanOrEqual,
                        new DateArrayReturning(
                            new DateField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.PlacedOn
                            )
                        ),
                        new DateReturning(new DateScalar(threshold))
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

        Assert.Equal(
            db.OrderRows.Count(order => order.PlacedOn <= threshold),
            result.Count
        );
    }

    [Fact]
    public void EachTimeGreaterThanOrEqualIncludesThreshold()
    {
        SampleDatabase db = new SampleDatabase();
        TimeOnly threshold = new TimeOnly(10, 0, 0);

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
            new BooleanArrayReturning(
                new EachComparison(
                    new EachTimeComparison(
                        EachComparisonOperator.EachGreaterThanOrEqual,
                        new TimeArrayReturning(
                            new TimeField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.ShiftStart
                            )
                        ),
                        new TimeReturning(new TimeScalar(threshold))
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

        Assert.Equal(
            db.UserRows.Count(user => user.ShiftStart >= threshold),
            result.Count
        );
    }

    [Fact]
    public void EachDateTimeLessThanFiltersEarlierInstants()
    {
        SampleDatabase db = new SampleDatabase();
        DateTime threshold = new DateTime(2024, 6, 2, 9, 15, 0);

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
            new BooleanArrayReturning(
                new EachComparison(
                    new EachDateTimeComparison(
                        EachComparisonOperator.EachLessThan,
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.LastLogin
                            )
                        ),
                        new DateTimeReturning(new DateTimeScalar(threshold))
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

        Assert.Equal(
            db.UserRows.Count(user => user.LastLogin < threshold),
            result.Count
        );
    }
}
