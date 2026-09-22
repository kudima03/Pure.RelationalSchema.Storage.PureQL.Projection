using Pure.RelationalSchema.Abstractions.Column;
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
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.orders",
                                "order_id"
                            )
                        )
                    ),
                    "oid"
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    ),
                    "state"
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.orders",
                                "order_total"
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
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            "schema_with_foreign_keys.orders",
                                            "order_total"
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
                                            "schema_with_foreign_keys.orders",
                                            "order_status"
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
                                            "schema_with_foreign_keys.orders",
                                            "placed_on"
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
                                            "schema_with_foreign_keys.orders",
                                            "placed_at"
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
                                            "schema_with_foreign_keys.users",
                                            "shift_start"
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
                    "schema_with_foreign_keys.users",
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        "schema_with_foreign_keys.orders",
                                        "order_user_id"
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        "schema_with_foreign_keys.users",
                                        "user_id"
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
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_with_foreign_keys.orders",
                                "order_id"
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    )
                ),
            ]
        );

        PureQLProjection projection = new PureQLProjection(datasets, query);

        IColumn[] columns = [.. projection.TableSchema.Columns];

        Assert.Equal(
            ["order_id", "order_status"],
            [.. columns.Select(column => column.Name.TextValue)]
        );

        ProjectionResult result = new ProjectionResult(projection);

        Assert.Equal(
            [.. columns.Select(column => column.Name.TextValue)],
            result.ColumnNames
        );
    }
}
