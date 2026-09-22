using Pure.Primitives.String;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Samples.Schemas;
using Pure.RelationalSchema.Samples.Tables;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Cells;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using Pure.RelationalSchema.Storage.Samples.TableDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.EachArithmetics;
using PureQL.CSharp.Model.EachComparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Errors;

// Part of the #72 roadmap (issue #104): dedicated negative-path coverage.
// Every construct below is a defined failure, never a silent wrong answer,
// per the epic's stated principle (see issue #104's follow-up fix: the
// CellValueExtractor getters and WhereExpressionBuilder.DivideDoubles now
// throw on an unresolved field, a type-mismatched/malformed cell text, or a
// zero divisor, instead of silently returning null).
[Trait("Feature", "Negative")]
public sealed class NegativePathTests
{
    // ===== Table/entity not present in the supplied datasets =====

    // EntityReferenceValidator only checks that every referenced entity
    // string matches the from entity/alias or a join entity - it never
    // checks that a matching IStoredSchemaDataSet/table actually exists. A
    // from entity that is syntactically self-consistent but absent from the
    // supplied datasets reaches RowsFromDatasets.Build's table lookup, whose
    // LINQ First(predicate) throws InvalidOperationException when nothing
    // matches.
    [Trait("Clause", "From")]
    [Fact]
    public void FromEntityNotInSuppliedDatasetsThrowsInvalidOperationException()
    {
        // The stored rows are irrelevant here - the lookup fails before any
        // cell is read - so this uses the minimal shape fixture rather than
        // SchemaDataSetWithForeignKeys, which makes that independence
        // explicit.
        IStoredSchemaDataSet dataset = new SingleTableSchemaDataSet();

        Query query = new Query(
            new FromExpression(
                new JoinedString(
                    new DotString(),
                    [new SingleTableRelationalSchema().Name, new String("nonexistent_table")]
                ).TextValue
            ),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new SingleTableRelationalSchema().Name,
                                        new String("nonexistent_table"),
                                    ]
                                ).TextValue,
                                "whatever"
                            )
                        )
                    )
                ),
            ]
        );

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new PureQLProjection([dataset], query)
        );

        Assert.Contains(
            "Sequence contains no matching element",
            exception.Message,
            System.StringComparison.Ordinal
        );
    }

    // Mirrors the from-entity case for a join: JoinApplicator.Apply resolves
    // the join's "schema.table" path with the identical LINQ First(predicate)
    // pattern, so an otherwise well-formed join entity absent from the
    // supplied datasets fails the same way.
    [Trait("Clause", "Join")]
    [Fact]
    public void JoinEntityNotInSuppliedDatasetsThrowsInvalidOperationException()
    {
        IStoredSchemaDataSet dataset = new SingleTableSchemaDataSet();

        Query query = new Query(
            new FromExpression(
                new JoinedString(
                    new DotString(),
                    [new SingleTableRelationalSchema().Name, new SingleColumnTable().Name]
                ).TextValue
            ),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new SingleTableRelationalSchema().Name,
                                        new SingleColumnTable().Name,
                                    ]
                                ).TextValue,
                                new IdColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Inner,
                    new JoinedString(
                        new DotString(),
                        [
                            new SingleTableRelationalSchema().Name,
                            new String("nonexistent_join_table"),
                        ]
                    ).TextValue,
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        new JoinedString(
                                            new DotString(),
                                            [
                                                new SingleTableRelationalSchema().Name,
                                                new SingleColumnTable().Name,
                                            ]
                                        ).TextValue,
                                        new IdColumn().Name.TextValue
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        new JoinedString(
                                            new DotString(),
                                            [
                                                new SingleTableRelationalSchema().Name,
                                                new String("nonexistent_join_table"),
                                            ]
                                        ).TextValue,
                                        "whatever_id"
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

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new PureQLProjection([dataset], query)
        );

        Assert.Contains(
            "Sequence contains no matching element",
            exception.Message,
            System.StringComparison.Ordinal
        );
    }

    // ===== Column not present on the resolved table =====

    // The per-row projection path (RowsFromDatasets.ApplyRowProjection) uses
    // CellValueExtractor.GetRequiredCell, which throws KeyNotFoundException
    // when no column on the row matches the requested field name. The table
    // itself resolves fine here (schema_with_foreign_keys.users); only the
    // field name is bad.
    [Trait("Clause", "Select")]
    [Fact]
    public void SelectFieldNotOnResolvedTableThrowsKeyNotFoundException()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression(
                new JoinedString(
                    new DotString(),
                    [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
                ).TextValue
            ),
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
                                "user_nickname"
                            )
                        )
                    )
                ),
            ]
        );

        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(
            () => new ProjectionResult(new PureQLProjection(datasets, query))
        );

        Assert.Contains("user_nickname", exception.Message, System.StringComparison.Ordinal);
    }

    // The group-by projection path (GroupByApplicator.ProjectionItemOf) has
    // its own call to CellValueExtractor.GetRequiredCell for plain field
    // select expressions, so the same defined failure holds once GROUP BY is
    // engaged (here, by grouping on a real field while selecting a bad one).
    [Trait("Clause", "GroupBy")]
    [Fact]
    public void GroupBySelectFieldNotOnResolvedTableThrowsKeyNotFoundException()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression(
                new JoinedString(
                    new DotString(),
                    [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
                ).TextValue
            ),
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
                    ),
                    "status"
                ),
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
                                "order_notes"
                            )
                        )
                    ),
                    "notes"
                ),
            ],
            where: null,
            join: null,
            groupBy:
            [
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
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(
            () => new ProjectionResult(new PureQLProjection(datasets, query))
        );

        Assert.Contains("order_notes", exception.Message, System.StringComparison.Ordinal);
    }

    // ===== Aggregate inside WHERE (documented known execution gap) =====

    // CLAUDE.md lists "aggregates inside WHERE" as a known execution gap that
    // must raise NotSupportedException rather than silently produce a wrong
    // answer. WhereExpressionBuilder.BuildNumberReturningAsExpr has no case
    // for NumberAggregate/Count outside group-by evaluation, so it fails fast
    // at query construction, before any row is ever read.
    [Trait("Clause", "Where")]
    [Fact]
    public void AggregateInsideWhereComparisonThrowsNotSupportedException()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression(
                new JoinedString(
                    new DotString(),
                    [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
                ).TextValue
            ),
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
            ],
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThan,
                        new NumberReturning(
                            new Count(
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
                            )
                        ),
                        new NumberReturning(new NumberScalar(0))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        _ = Assert.Throws<NotSupportedException>(
            () => new PureQLProjection(datasets, query)
        );
    }

    // ===== Missing column outside SELECT fails fast =====

    // WHERE field resolution goes through CellValueExtractor.GetTextValue,
    // which now requires the cell (GetRequiredCell) the same way the SELECT
    // path always did, so a nonexistent column referenced in WHERE throws
    // the same KeyNotFoundException a typo would in SELECT instead of
    // silently excluding every row.
    [Fact]
    [Trait("Clause", "Where")]
    public void WhereFieldNotOnResolvedTableFailsFast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression(
                new JoinedString(
                    new DotString(),
                    [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
                ).TextValue
            ),
            [
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
            new BooleanArrayReturning(
                new EachEquality(
                    new EachStringEquality(
                        new StringArrayReturning(
                            new StringField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithForeignKeys().Name,
                                        new OrdersTable().Name,
                                    ]
                                ).TextValue,
                                "order_notes"
                            )
                        ),
                        new StringReturning(new StringScalar("anything"))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        _ = Assert.Throws<KeyNotFoundException>(() => new ProjectionResult(
            new PureQLProjection(datasets, query)
        ));
    }

    // ===== Type mismatch fails fast =====

    // A NumberField pointing at order_status (a StringColumnType column) is
    // legal to construct - Field references are untyped strings, not checked
    // against the schema's declared column type. CellValueExtractor.
    // GetDoubleValue now throws FormatException for non-empty text that
    // cannot parse as a number (e.g. a string column's "shipped"), instead
    // of silently returning null and excluding every row from the
    // comparison.
    [Fact]
    [Trait("Clause", "Where")]
    public void TypeMismatchNumberFieldAgainstStringColumnFailsFast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression(
                new JoinedString(
                    new DotString(),
                    [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
                ).TextValue
            ),
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
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new NumberField(
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
                        new NumberReturning(new NumberScalar(0))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        _ = Assert.Throws<FormatException>(() => new ProjectionResult(
            new PureQLProjection(datasets, query)
        ));
    }

    // ===== Malformed cell text fails fast =====

    // A stored uuid cell whose text is not a valid Guid (corrupted/foreign
    // data, as opposed to a schema/reference type mismatch) is parsed by
    // CellValueExtractor.GetGuidValue via Guid.TryParse; non-empty text that
    // fails to parse now throws FormatException instead of being treated as
    // an absent value. This table is deliberately hand-built (not from the
    // package) so its uuid cell can hold malformed text.
    [Fact]
    [Trait("Clause", "Where")]
    public void MalformedUuidCellTextFailsFast()
    {
        ITable table = new Table.Table(
            new String("widgets"),
            [new Column.Column(new String("widget_id"), new UuidColumnType())],
            []
        );

        IRow malformedRow = new Row(
            new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                table.Columns,
                column => column,
                _ => new InvariantCell(new String("not-a-valid-uuid")),
                column => new ColumnHash(column)
            )
        );

        IStoredTableDataSet tableDataset = new StoredTableDataSet(table, [malformedRow]);

        ISchema schema = new Schema.Schema(new String("shop"), [table], []);

        IStoredSchemaDataSet[] datasets =
            [new StoredSchemaDataSet(schema, [tableDataset])];

        Query query = new Query(
            new FromExpression("shop.widgets"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField("shop.widgets", "widget_id")
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField("shop.widgets", "widget_id")
                        ),
                        new UuidReturning(new UuidScalar(Guid.NewGuid()))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        _ = Assert.Throws<FormatException>(() => new ProjectionResult(
            new PureQLProjection(datasets, query)
        ));
    }

    // ===== eachDivide by zero fails fast =====

    // WhereExpressionBuilder.DivideDoubles now raises DivideByZeroException
    // for a zero divisor instead of returning null, so eachDivide by zero
    // fails the query the way SQL division-by-zero does instead of silently
    // excluding the row.
    [Fact]
    [Trait("Clause", "Where")]
    public void EachDivideByZeroFailsFast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression(
                new JoinedString(
                    new DotString(),
                    [new RelationalSchemaWithForeignKeys().Name, new OrdersTable().Name]
                ).TextValue
            ),
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
            ],
            new BooleanArrayReturning(
                new EachComparison(
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new EachArithmetic(
                                new EachDivide(
                                    [
                                        new NumberArrayReturning(
                                            new NumberField(
                                                new JoinedString(
                                                    new DotString(),
                                                    [
                                                        new RelationalSchemaWithForeignKeys()
                                                            .Name,
                                                        new OrdersTable().Name,
                                                    ]
                                                ).TextValue,
                                                new OrderTotalColumn().Name.TextValue
                                            )
                                        ),
                                        new NumberReturning(new NumberScalar(0)),
                                    ]
                                )
                            )
                        ),
                        new NumberReturning(new NumberScalar(0))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        _ = Assert.Throws<DivideByZeroException>(() => new ProjectionResult(
            new PureQLProjection(datasets, query)
        ));
    }

    // ===== Missing column in ORDER BY fails fast =====

    // OrderByApplicator resolves fields via CellValueExtractor's getters,
    // which now require the cell to exist, so a nonexistent ORDER BY column
    // throws the same KeyNotFoundException a typo would in SELECT instead
    // of silently sorting every row as null.
    [Fact]
    [Trait("Clause", "OrderBy")]
    public void OrderByFieldNotOnResolvedTableFailsFast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression(
                new JoinedString(
                    new DotString(),
                    [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
                ).TextValue
            ),
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
            orderBy:
            [
                new OrderByItem(
                    new Field(
                        new StringField(
                            new JoinedString(
                                new DotString(),
                                [
                                    new RelationalSchemaWithForeignKeys().Name,
                                    new UsersTable().Name,
                                ]
                            ).TextValue,
                            "user_nickname"
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        _ = Assert.Throws<KeyNotFoundException>(() => new ProjectionResult(
            new PureQLProjection(datasets, query)
        ));
    }
}
