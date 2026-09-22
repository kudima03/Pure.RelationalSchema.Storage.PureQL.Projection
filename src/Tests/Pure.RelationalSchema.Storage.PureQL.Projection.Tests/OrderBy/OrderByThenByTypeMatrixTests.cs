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
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.OrderBy;

// Closes coverage gaps on OrderByApplicator's ApplyThenBy (per-type secondary
// key arms for Date, DateTime, Time and Uuid were previously untested) and on
// its NullField arm (issue #133).
//
// NullField decision: NullField is PureQL's constant-NULL field reference
// (Entity/Field carried but ignored; Type is NullType). SQL's ORDER BY on a
// constant expression is a stable no-op - every row's key compares equal, so
// a stable sort preserves input order regardless of ASC/DESC. .NET's
// OrderBy/OrderByDescending/ThenBy/ThenByDescending are all documented as
// stable sorts. OrderByApplicator's NullField arm is
// `descending ? source.OrderByDescending(_ => (string?)null)
//             : source.OrderBy(_ => (string?)null)` (and the ThenBy
// equivalent) - a constant-key stable sort, which is exactly the SQL-correct
// behaviour. Running the tests below against the current translator confirms
// it already produces that SQL-correct, order-preserving result in both
// directions and at both primary- and secondary-key position, so they are
// written and asserted as ordinary (non-skipped) characterization tests, not
// gated behind KnownGap: there is no divergence between "SQL-correct" and
// "current behaviour" to characterize as a gap here. Issue #133's title
// ("silently no-ops") is accurate in mechanism but the resulting semantics
// happen to already match SQL's ORDER BY-by-constant contract.
[Trait("Clause", "OrderBy")]
[Trait("Feature", "OrderByThenByTypeMatrix")]
public sealed class OrderByThenByTypeMatrixTests
{
    [Fact]
    public void OrderByStatusAscThenPlacedOnAscOrdersShippedOrdersByDate()
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
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new OrdersTable().Name,
                                    ]
                                ).TextValue,
                                new OrderStatusColumn().Name.TextValue
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new DateArrayReturning(
                            new DateField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new OrdersTable().Name,
                                    ]
                                ).TextValue,
                                new PlacedOnColumn().Name.TextValue
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
                        new StringField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new OrdersTable().Name,
                                ]
                            ).TextValue,
                            new OrderStatusColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Asc
                ),
                new OrderByItem(
                    new Field(
                        new DateField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new OrdersTable().Name,
                                ]
                            ).TextValue,
                            new PlacedOnColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (string?, DateOnly?)[] expected =
        [
            .. orderRows.OrderBy(order => order.OrderStatus)
                .ThenBy(order => order.PlacedOn)
                .Select(order => ((string?)order.OrderStatus, (DateOnly?)order.PlacedOn)),
        ];

        (string?, DateOnly?)[] actual =
        [
            .. result.Rows.Select(row =>
                (
                    row[new OrderStatusColumn().Name.TextValue],
                    row.Date(new PlacedOnColumn().Name.TextValue)
                )
            ),
        ];

        Assert.Equal(expected, actual);
    }

    // Mixed-direction companion: primary (Status) ascending, secondary
    // (PlacedOn, Date) descending.
    [Fact]
    public void OrderByStatusAscThenPlacedOnDescOrdersShippedOrdersByDateDescending()
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
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new OrdersTable().Name,
                                    ]
                                ).TextValue,
                                new OrderStatusColumn().Name.TextValue
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new DateArrayReturning(
                            new DateField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new OrdersTable().Name,
                                    ]
                                ).TextValue,
                                new PlacedOnColumn().Name.TextValue
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
                        new StringField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new OrdersTable().Name,
                                ]
                            ).TextValue,
                            new OrderStatusColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Asc
                ),
                new OrderByItem(
                    new Field(
                        new DateField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new OrdersTable().Name,
                                ]
                            ).TextValue,
                            new PlacedOnColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Desc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (string?, DateOnly?)[] expected =
        [
            .. orderRows.OrderBy(order => order.OrderStatus)
                .ThenByDescending(order => order.PlacedOn)
                .Select(order => ((string?)order.OrderStatus, (DateOnly?)order.PlacedOn)),
        ];

        (string?, DateOnly?)[] actual =
        [
            .. result.Rows.Select(row =>
                (
                    row[new OrderStatusColumn().Name.TextValue],
                    row.Date(new PlacedOnColumn().Name.TextValue)
                )
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OrderByStatusAscThenPlacedAtAscOrdersShippedOrdersByDateTime()
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
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new OrdersTable().Name,
                                    ]
                                ).TextValue,
                                new OrderStatusColumn().Name.TextValue
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new OrdersTable().Name,
                                    ]
                                ).TextValue,
                                new PlacedAtColumn().Name.TextValue
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
                        new StringField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new OrdersTable().Name,
                                ]
                            ).TextValue,
                            new OrderStatusColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Asc
                ),
                new OrderByItem(
                    new Field(
                        new DateTimeField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new OrdersTable().Name,
                                ]
                            ).TextValue,
                            new PlacedAtColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (string?, DateTime?)[] expected =
        [
            .. orderRows.OrderBy(order => order.OrderStatus)
                .ThenBy(order => order.PlacedAt)
                .Select(order => ((string?)order.OrderStatus, (DateTime?)order.PlacedAt)),
        ];

        (string?, DateTime?)[] actual =
        [
            .. result.Rows.Select(row =>
                (
                    row[new OrderStatusColumn().Name.TextValue],
                    row.DateTime(new PlacedAtColumn().Name.TextValue)
                )
            ),
        ];

        Assert.Equal(expected, actual);
    }

    // Mixed-direction companion: primary (Status) ascending, secondary
    // (PlacedAt, DateTime) descending.
    [Fact]
    public void OrderByStatusAscThenPlacedAtDescOrdersShippedOrdersByDateTimeDescending()
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
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new OrdersTable().Name,
                                    ]
                                ).TextValue,
                                new OrderStatusColumn().Name.TextValue
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new OrdersTable().Name,
                                    ]
                                ).TextValue,
                                new PlacedAtColumn().Name.TextValue
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
                        new StringField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new OrdersTable().Name,
                                ]
                            ).TextValue,
                            new OrderStatusColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Asc
                ),
                new OrderByItem(
                    new Field(
                        new DateTimeField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new OrdersTable().Name,
                                ]
                            ).TextValue,
                            new PlacedAtColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Desc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (string?, DateTime?)[] expected =
        [
            .. orderRows.OrderBy(order => order.OrderStatus)
                .ThenByDescending(order => order.PlacedAt)
                .Select(order => ((string?)order.OrderStatus, (DateTime?)order.PlacedAt)),
        ];

        (string?, DateTime?)[] actual =
        [
            .. result.Rows.Select(row =>
                (
                    row[new OrderStatusColumn().Name.TextValue],
                    row.DateTime(new PlacedAtColumn().Name.TextValue)
                )
            ),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OrderByActiveAscThenShiftStartAscOrdersUsersByTime()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

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
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue,
                                new UserNameColumn().Name.TextValue
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
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new UsersTable().Name,
                                ]
                            ).TextValue,
                            new UserActiveColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Asc
                ),
                new OrderByItem(
                    new Field(
                        new TimeField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new UsersTable().Name,
                                ]
                            ).TextValue,
                            new ShiftStartColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        // Active=true group ties on ShiftStart among Ann/Cara/Fay (all
        // 09:00); Dan (11:30) sorts after them. A stable ThenBy keeps
        // Ann/Cara/Fay in their original relative order within the tie.
        string[] expected =
        [
            .. userRows.OrderBy(user => user.UserActive)
                .ThenBy(user => user.ShiftStart)
                .Select(user => user.UserName),
        ];

        string?[] actual = [.. result.Column(new UserNameColumn().Name.TextValue)];

        Assert.Equal(expected, actual);
    }

    // Mixed-direction companion: primary (Active) descending, secondary
    // (ShiftStart, Time) ascending.
    [Fact]
    public void OrderByActiveDescThenShiftStartAscOrdersUsersByTimeMixedDirection()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

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
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue,
                                new UserNameColumn().Name.TextValue
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
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new UsersTable().Name,
                                ]
                            ).TextValue,
                            new UserActiveColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Desc
                ),
                new OrderByItem(
                    new Field(
                        new TimeField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new UsersTable().Name,
                                ]
                            ).TextValue,
                            new ShiftStartColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows.OrderByDescending(user => user.UserActive)
                .ThenBy(user => user.ShiftStart)
                .Select(user => user.UserName),
        ];

        string?[] actual = [.. result.Column(new UserNameColumn().Name.TextValue)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OrderByStatusAscThenIdAscOrdersShippedOrdersByUuidComparer()
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
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new OrdersTable().Name,
                                    ]
                                ).TextValue,
                                new OrderStatusColumn().Name.TextValue
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new OrdersTable().Name,
                                    ]
                                ).TextValue,
                                new OrderIdColumn().Name.TextValue
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
                        new StringField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new OrdersTable().Name,
                                ]
                            ).TextValue,
                            new OrderStatusColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Asc
                ),
                new OrderByItem(
                    new Field(
                        new UuidField(new JoinedString(
                            new DotString(),
                            [
                                new RelationalSchemaWithForeignKeys().Name,
                                new OrdersTable().Name,
                            ]
                        ).TextValue, new OrderIdColumn().Name.TextValue)
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        // "shipped" ties across Id101/Id103/Id105; secondary Id ordering
        // must match Guid's default IComparable ordering (see
        // OrderByUuidAscendingMatchesGuidComparerOrdering).
        (string?, Guid?)[] expected =
        [
            .. orderRows.OrderBy(order => order.OrderStatus)
                .ThenBy(order => order.OrderId)
                .Select(order => ((string?)order.OrderStatus, (Guid?)order.OrderId)),
        ];

        (string?, Guid?)[] actual =
        [
            .. result.Rows.Select(row =>
                (
                    row[new OrderStatusColumn().Name.TextValue],
                    row.Uuid(new OrderIdColumn().Name.TextValue)
                )
            ),
        ];

        Assert.Equal(expected, actual);
    }

    // Mixed-direction companion: primary (Status) ascending, secondary
    // (Id, Uuid) descending.
    [Fact]
    public void OrderByStatusAscThenIdDescOrdersShippedOrdersByUuidComparerDescending()
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
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new OrdersTable().Name,
                                    ]
                                ).TextValue,
                                new OrderStatusColumn().Name.TextValue
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new OrdersTable().Name,
                                    ]
                                ).TextValue,
                                new OrderIdColumn().Name.TextValue
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
                        new StringField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new OrdersTable().Name,
                                ]
                            ).TextValue,
                            new OrderStatusColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Asc
                ),
                new OrderByItem(
                    new Field(
                        new UuidField(new JoinedString(
                            new DotString(),
                            [
                                new RelationalSchemaWithForeignKeys().Name,
                                new OrdersTable().Name,
                            ]
                        ).TextValue, new OrderIdColumn().Name.TextValue)
                    ),
                    SortDirection.Desc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        (string?, Guid?)[] expected =
        [
            .. orderRows.OrderBy(order => order.OrderStatus)
                .ThenByDescending(order => order.OrderId)
                .Select(order => ((string?)order.OrderStatus, (Guid?)order.OrderId)),
        ];

        (string?, Guid?)[] actual =
        [
            .. result.Rows.Select(row =>
                (
                    row[new OrderStatusColumn().Name.TextValue],
                    row.Uuid(new OrderIdColumn().Name.TextValue)
                )
            ),
        ];

        Assert.Equal(expected, actual);
    }

    // Characterization of the NullField ApplyOrderBy (primary-key) arm: a
    // constant-null key is a stable no-op, so ascending order-by leaves rows
    // in their original input order. See file header for the SQL-vs-#133
    // reasoning.
    [Fact]
    public void OrderByNullFieldAscendingPreservesInputOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

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
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue,
                                new UserNameColumn().Name.TextValue
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
                        new NullField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new UsersTable().Name,
                                ]
                            ).TextValue,
                            new UserNameColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected = [.. userRows.Select(user => user.UserName)];

        string?[] actual = [.. result.Column(new UserNameColumn().Name.TextValue)];

        Assert.Equal(expected, actual);
    }

    // Descending companion: a constant-null key is a no-op regardless of
    // direction, since every key compares equal either way.
    [Fact]
    public void OrderByNullFieldDescendingPreservesInputOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

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
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue,
                                new UserNameColumn().Name.TextValue
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
                        new NullField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new UsersTable().Name,
                                ]
                            ).TextValue,
                            new UserNameColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Desc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected = [.. userRows.Select(user => user.UserName)];

        string?[] actual = [.. result.Column(new UserNameColumn().Name.TextValue)];

        Assert.Equal(expected, actual);
    }

    // Characterization of the NullField ApplyThenBy (secondary-key) arm: a
    // real primary key (Active) followed by a constant-null secondary key
    // orders solely by the primary, keeping original relative order within
    // ties, exactly as if the NullField item were absent.
    [Fact]
    public void NullFieldAsSecondaryKeyLeavesPrimaryOrderingIntactAscending()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

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
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue,
                                new UserNameColumn().Name.TextValue
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
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new UsersTable().Name,
                                ]
                            ).TextValue,
                            new UserActiveColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Asc
                ),
                new OrderByItem(
                    new Field(
                        new NullField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new UsersTable().Name,
                                ]
                            ).TextValue,
                            new UserNameColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows.OrderBy(user => user.UserActive).Select(user => user.UserName),
        ];

        string?[] actual = [.. result.Column(new UserNameColumn().Name.TextValue)];

        Assert.Equal(expected, actual);
    }

    // Descending companion for the NullField secondary-key arm: still a
    // no-op, keeping the primary's ordering intact.
    [Fact]
    public void NullFieldAsSecondaryKeyLeavesPrimaryOrderingIntactDescending()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

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
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new UsersTable().Name,
                                    ]
                                ).TextValue,
                                new UserNameColumn().Name.TextValue
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
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new UsersTable().Name,
                                ]
                            ).TextValue,
                            new UserActiveColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Asc
                ),
                new OrderByItem(
                    new Field(
                        new NullField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new UsersTable().Name,
                                ]
                            ).TextValue,
                            new UserNameColumn().Name.TextValue
                        )
                    ),
                    SortDirection.Desc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows.OrderBy(user => user.UserActive).Select(user => user.UserName),
        ];

        string?[] actual = [.. result.Column(new UserNameColumn().Name.TextValue)];

        Assert.Equal(expected, actual);
    }
}
