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
using PureQL.CSharp.Model.EachBooleanOperations;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Combined;

// End-to-end queries that combine several clauses, checking they compose into
// one correct result set.
[Trait("Clause", "Combined")]
[Trait("Feature", "CombinedClauses")]
public sealed class CombinedClauseTests
{
    [Fact]
    public void WhereThenOrderByThenPaginateReturnsCorrectWindow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                                new OrderTotalColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new NumberField(
                                new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                                new OrderTotalColumn().Name.TextValue
                            )
                        ),
                        new NumberReturning(new NumberScalar(50))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                            new OrderTotalColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            new global::PureQL.CSharp.Model.Pagination(1, 2)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double?[] expected =
        [
            .. orderRows.Where(order => order.OrderTotal > 50)
                .OrderBy(order => order.OrderTotal)
                .Skip(1)
                .Take(2)
                .Select(order => (double?)order.OrderTotal),
        ];

        double?[] actual =
        [
            .. result.Rows.Select(row => row.Double(new OrderTotalColumn().Name.TextValue)),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void WhereThenGroupByYieldsGroupsOfTheFilteredRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                                new OrderUserIdColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachNotOperator(
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachStringEquality(
                                new StringArrayReturning(
                                    new StringField(
                                        new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                                        new OrderStatusColumn().Name.TextValue
                                    )
                                ),
                                new StringReturning(new StringScalar("cancelled"))
                            )
                        )
                    )
                )
            ),
            join: null,
            [
                new Field(
                    new UuidField(
                        new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                        new OrderUserIdColumn().Name.TextValue
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows
            .Where(order => order.OrderStatus != "cancelled")
            .Select(order => order.OrderUserId)
            .Distinct()
            .Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    public void JoinThenWhereThenOrderByThenPaginateReturnsCorrectWindow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new Query(
            new FromExpression(new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                                new OrderTotalColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new NumberField(
                                new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                                new OrderTotalColumn().Name.TextValue
                            )
                        ),
                        new NumberReturning(new NumberScalar(75))
                    )
                )
            ),
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
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            new JoinedString(new DotString(), [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]).TextValue,
                            new OrderTotalColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Desc
                ),
            ],
            new global::PureQL.CSharp.Model.Pagination(0, 2)
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double?[] expected =
        [
            .. orderRows.Where(order => order.OrderTotal > 75)
                .OrderByDescending(order => order.OrderTotal)
                .Take(2)
                .Select(order => (double?)order.OrderTotal),
        ];

        double?[] actual =
        [
            .. result.Rows.Select(row => row.Double(new OrderTotalColumn().Name.TextValue)),
        ];

        Assert.Equal(expected, actual);
    }
}
