using Pure.Primitives.String;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Samples.Schemas;
using Pure.RelationalSchema.Samples.Tables;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
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
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderStatusColumn().Name.TextValue
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
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderStatusColumn().Name.TextValue
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order =>
                string.CompareOrdinal(order.OrderStatus, "pending") < 0
            ),
            result.Count
        );
    }

    [Fact]
    public void EachDateLessThanOrEqualIncludesThreshold()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly threshold = new DateOnly(2024, 6, 3);

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new OrderStatusColumn().Name.TextValue
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
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
).TextValue,
                                new PlacedOnColumn().Name.TextValue
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(order => order.PlacedOn <= threshold),
            result.Count
        );
    }

    [Fact]
    public void EachTimeGreaterThanOrEqualIncludesThreshold()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly threshold = new TimeOnly(10, 0, 0);

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
).TextValue,
                                new UserNameColumn().Name.TextValue
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
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
).TextValue,
                                new ShiftStartColumn().Name.TextValue
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Count(user => user.ShiftStart >= threshold),
            result.Count
        );
    }

    [Fact]
    public void EachDateTimeLessThanFiltersEarlierInstants()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        DateTime threshold = new DateTime(2024, 6, 2, 9, 15, 0);

        Query query = new Query(
            new FromExpression(new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
).TextValue,
                                new UserNameColumn().Name.TextValue
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
                                new JoinedString(
new DotString(),
[new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
).TextValue,
                                new LastLoginColumn().Name.TextValue
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
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            userRows.Count(user => user.LastLogin < threshold),
            result.Count
        );
    }
}
