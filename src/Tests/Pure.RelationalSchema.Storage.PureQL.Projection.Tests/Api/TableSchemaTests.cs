using Pure.Primitives.String;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Samples.Schemas;
using Pure.RelationalSchema.Samples.Tables;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression(new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                                new OrderIdColumn().Name.TextValue
                            )
                        )
                    ),
                    "oid"
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                                new OrderStatusColumn().Name.TextValue
                            )
                        )
                    ),
                    "state"
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                                new OrderTotalColumn().Name.TextValue
                            )
                        )
                    ),
                    "amount"
                ),
            ]
        );

        PureQLProjection projection = new PureQLProjection(datasets, query);

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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression(new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                                            new OrderTotalColumn().Name.TextValue
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
                                            new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                                            new OrderStatusColumn().Name.TextValue
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
                                            new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                                            new PlacedOnColumn().Name.TextValue
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
                                            new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                                            new PlacedAtColumn().Name.TextValue
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
                                            new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]).TextValue,
                                            new ShiftStartColumn().Name.TextValue
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
                    new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]).TextValue,
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                                        new OrderUserIdColumn().Name.TextValue
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]).TextValue,
                                        new UserIdColumn().Name.TextValue
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

        PureQLProjection projection = new PureQLProjection(datasets, query);

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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression(new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                                new OrderIdColumn().Name.TextValue
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                                new OrderStatusColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ]
        );

        PureQLProjection projection = new PureQLProjection(datasets, query);

        IColumn[] columns = [.. projection.TableSchema.Columns];

        Assert.Equal(
            [new OrderIdColumn().Name.TextValue, new OrderStatusColumn().Name.TextValue],
            [.. columns.Select(column => column.Name.TextValue)]
        );

        ProjectionResult result = new ProjectionResult(projection);

        Assert.Equal(
            [.. columns.Select(column => column.Name.TextValue)],
            result.ColumnNames
        );
    }
}
