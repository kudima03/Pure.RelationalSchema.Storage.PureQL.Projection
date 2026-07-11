using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// GROUP BY on the remaining key types (number, date, datetime, time),
// complementing GroupByTests (string / bool / uuid / composite). Each projects
// its grouping key and yields one row per distinct value.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "GroupByTypes")]
public sealed class GroupByTypesTests
{
    [Fact]
    public void GroupByNumberKeyYieldsOneRowPerDistinctValue()
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
            ],
            where: null,
            join: null,
            [new Field(new NumberField(SampleDatabase.Users.Entity, SampleDatabase.Users.Age))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double[] expected =
        [
            .. db.UserRows.Select(user => user.UserAge).Distinct().OrderBy(v => v),
        ];

        double[] actual =
        [
            .. result.Rows.Select(row => row.Double(SampleDatabase.Users.Age)!.Value)
                .OrderBy(v => v),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GroupByDateKeyYieldsOneRowPerDistinctValue()
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
            [new Field(new DateField(SampleDatabase.Users.Entity, SampleDatabase.Users.SignupDate))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.UserRows.Select(user => user.SignupDate).Distinct().Count(),
            result.Count
        );
    }

    [Fact]
    public void GroupByDateTimeKeyYieldsOneRowPerDistinctValue()
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
            [new Field(new DateTimeField(SampleDatabase.Users.Entity, SampleDatabase.Users.LastLogin))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.UserRows.Select(user => user.LastLogin).Distinct().Count(),
            result.Count
        );
    }

    [Fact]
    public void GroupByTimeKeyYieldsOneRowPerDistinctValue()
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
            [new Field(new TimeField(SampleDatabase.Users.Entity, SampleDatabase.Users.ShiftStart))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            db.UserRows.Select(user => user.ShiftStart).Distinct().Count(),
            result.Count
        );
    }
}
