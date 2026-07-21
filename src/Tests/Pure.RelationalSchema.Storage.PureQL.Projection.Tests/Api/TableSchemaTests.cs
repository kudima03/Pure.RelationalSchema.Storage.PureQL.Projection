using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates.Date;
using PureQL.CSharp.Model.Aggregates.DateTime;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.Aggregates.String;
using PureQL.CSharp.Model.Aggregates.Time;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Api;

// PureQLProjection.TableSchema is the derived output schema. Its columns follow
// the select expressions: one column per expression, named by the select alias
// and typed by the expression's value type.
[Trait("Clause", "Select")]
[Trait("Feature", "TableSchema")]
public sealed class TableSchemaTests
{
    [Fact]
    public void TableSchemaColumnsFollowTheAliasedSelectExpressions()
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
                                SampleDatabase.Orders.Id
                            )
                        )
                    ),
                    "oid"
                ),
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
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Total
                            )
                        )
                    ),
                    "amount"
                ),
            ]
        );

        PureQLProjection projection = new PureQLProjection(db.Datasets, query);

        IColumn[] columns = [.. projection.TableSchema.Columns];

        Assert.Equal(
            ["oid", "state", "amount"],
            [.. columns.Select(column => column.Name.TextValue)]
        );
        Assert.Equal(
            ["uuid", "string", "double"],
            [.. columns.Select(column => column.Type.Name.TextValue)]
        );
    }

    [Fact]
    public void TableSchemaColumnForAggregateFollowsAliasAndType()
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
                    "totalSum"
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
                new SelectExpression(
                    new SingleValueReturning(
                        new DateReturning(
                            new DateAggregate(
                                new MaxDate(
                                    new DateArrayReturning(
                                        new DateField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.PlacedOn
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "maxPlacedOn"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new DateTimeReturning(
                            new DateTimeAggregate(
                                new MinDateTime(
                                    new DateTimeArrayReturning(
                                        new DateTimeField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.PlacedAt
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "minPlacedAt"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new TimeReturning(
                            new TimeAggregate(
                                new MaxTime(
                                    new TimeArrayReturning(
                                        new TimeField(
                                            SampleDatabase.Users.Entity,
                                            SampleDatabase.Users.ShiftStart
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "maxShiftStart"
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

        PureQLProjection projection = new PureQLProjection(db.Datasets, query);

        IColumn[] columns = [.. projection.TableSchema.Columns];

        Assert.Equal(
            ["totalSum", "maxStatus", "maxPlacedOn", "minPlacedAt", "maxShiftStart"],
            [.. columns.Select(column => column.Name.TextValue)]
        );
        Assert.Equal(
            ["double", "string", "date", "datetime", "time"],
            [.. columns.Select(column => column.Type.Name.TextValue)]
        );
    }

    [Fact]
    public void TableSchemaWithoutAliasesFallsBackToFieldNames()
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
                                SampleDatabase.Orders.Id
                            )
                        )
                    )
                ),
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
            ]
        );

        PureQLProjection projection = new PureQLProjection(db.Datasets, query);

        IColumn[] columns = [.. projection.TableSchema.Columns];

        Assert.Equal(
            [SampleDatabase.Orders.Id, SampleDatabase.Orders.Status],
            [.. columns.Select(column => column.Name.TextValue)]
        );

        ProjectionResult result = new ProjectionResult(projection);

        Assert.Equal(
            [.. columns.Select(column => column.Name.TextValue)],
            result.ColumnNames
        );
    }
}
