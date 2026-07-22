using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
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
// Every construct below is a defined failure or a documented KnownGap, never
// a silent wrong answer, per the epic's stated principle. Tests that pin
// today's actual (correct) fail-fast behaviour run normally; tests that
// would need to fail fast but currently produce a silently wrong result
// instead are skipped with [Fact(Skip = "KnownGap: ...")], pinning the
// exception that *should* be thrown once the gap is closed.
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
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression("shop.nonexistent_table"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField("shop.nonexistent_table", "whatever")
                        )
                    )
                ),
            ]
        );

        InvalidOperationException exception = Assert.Throws<InvalidOperationException>(
            () => new PureQLProjection(db.Datasets, query)
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
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Id
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Inner,
                    "shop.nonexistent_join_table",
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        SampleDatabase.Users.Entity,
                                        SampleDatabase.Users.Id
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        "shop.nonexistent_join_table",
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
            () => new PureQLProjection(db.Datasets, query)
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
    // itself resolves fine here (shop.users); only the field name is bad.
    [Trait("Clause", "Select")]
    [Fact]
    public void SelectFieldNotOnResolvedTableThrowsKeyNotFoundException()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Users.Entity,
                                "user_nickname"
                            )
                        )
                    )
                ),
            ]
        );

        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(
            () => new ProjectionResult(new PureQLProjection(db.Datasets, query))
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
                    ),
                    "status"
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
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
                        SampleDatabase.Orders.Entity,
                        SampleDatabase.Orders.Status
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(
            () => new ProjectionResult(new PureQLProjection(db.Datasets, query))
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
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThan,
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
                                    new UuidArrayReturning(
                                        new UuidField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Id
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

        _ = Assert.Throws<NotSupportedException>(() => new PureQLProjection(db.Datasets, query));
    }

    // ===== KnownGap: missing column outside SELECT is silently absorbed =====

    // Unlike the required SELECT path (CellValueExtractor.GetRequiredCell),
    // WHERE field resolution goes through CellValueExtractor.GetCell, which
    // returns null when no column matches instead of throwing. A nonexistent
    // column referenced in WHERE therefore silently compares as "no match"
    // for every row (0 results) instead of failing fast the way the same
    // typo would in SELECT. Pinning the SELECT path's KeyNotFoundException as
    // the spec-correct expectation.
    [Fact(
        Skip = "KnownGap: CellValueExtractor.GetCell returns null for an "
            + "unresolved column instead of throwing, so a nonexistent "
            + "column in WHERE silently excludes every row rather than "
            + "failing fast the way the same typo does in SELECT."
    )]
    [Trait("Status", "KnownGap")]
    [Trait("Clause", "Where")]
    public void WhereFieldNotOnResolvedTableSilentlyExcludesRowsInsteadOfFailing()
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
            ],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachStringEquality(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
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
            new PureQLProjection(db.Datasets, query)
        ));
    }

    // ===== KnownGap: type mismatch is silently absorbed, not rejected =====

    // A NumberField pointing at order_status (a StringColumnType column) is
    // legal to construct - Field references are untyped strings, not checked
    // against the schema's declared column type. CellValueExtractor.
    // GetDoubleValue parses the cell's text with double.TryParse; for a
    // string column's text ("shipped", ...) that always fails and yields
    // null, so every per-row numeric comparison against it is silently
    // false instead of failing. Pinning FormatException (what a non-Try
    // double.Parse on the same malformed text would throw) as the
    // spec-correct expectation.
    [Fact(
        Skip = "KnownGap: CellValueExtractor.GetDoubleValue silently returns "
            + "null for text that cannot parse as a number (e.g. a "
            + "NumberField pointing at a string column), so the mismatched "
            + "comparison silently excludes every row instead of failing."
    )]
    [Trait("Status", "KnownGap")]
    [Trait("Clause", "Where")]
    public void TypeMismatchNumberFieldAgainstStringColumnSilentlyExcludesRows()
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
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
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
            new PureQLProjection(db.Datasets, query)
        ));
    }

    // ===== KnownGap: malformed cell text is silently absorbed =====

    // A stored uuid cell whose text is not a valid Guid (corrupted/foreign
    // data, as opposed to a schema/reference type mismatch) is parsed by
    // CellValueExtractor.GetGuidValue via Guid.TryParse, which likewise
    // yields null on failure instead of surfacing the malformed text.
    // Pinning FormatException (what Guid.Parse would throw on the same
    // text) as the spec-correct expectation.
    [Fact(
        Skip = "KnownGap: CellValueExtractor.GetGuidValue silently returns "
            + "null for a cell whose stored text is not a valid uuid, so a "
            + "malformed cell is treated as an absent value instead of "
            + "failing fast."
    )]
    [Trait("Status", "KnownGap")]
    [Trait("Clause", "Where")]
    public void MalformedUuidCellTextSilentlyTreatedAsAbsentValue()
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
                _ => new Cell(new String("not-a-valid-uuid")),
                column => new ColumnHash(column)
            )
        );

        IStoredTableDataSet tableDataset = new SampleTableDataset(table, [malformedRow]);

        ISchema schema = new Schema.Schema(new String("shop"), [table], []);

        IReadOnlyDictionary<ITable, IStoredTableDataSet> byTable =
            new Collections.Generic.Dictionary<
                IStoredTableDataSet,
                ITable,
                IStoredTableDataSet
            >(
                [tableDataset],
                dataset => dataset.TableSchema,
                dataset => dataset,
                t => new TableHash(t)
            );

        IStoredSchemaDataSet[] datasets = [new StoredSchemaDataset(schema, byTable)];

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

    // ===== KnownGap: eachDivide by zero is silently absorbed =====

    // WhereExpressionBuilder.DivideDoubles returns null when the divisor is
    // zero instead of raising, so a row whose divisor is zero silently drops
    // out of the predicate (never matches) instead of the query failing the
    // way SQL division-by-zero does. Pinning DivideByZeroException as the
    // spec-correct expectation.
    [Fact(
        Skip = "KnownGap: WhereExpressionBuilder.DivideDoubles returns null "
            + "for a zero divisor instead of raising, so eachDivide by zero "
            + "silently excludes the row instead of failing the way SQL "
            + "division-by-zero does."
    )]
    [Trait("Status", "KnownGap")]
    [Trait("Clause", "Where")]
    public void EachDivideByZeroSilentlyExcludesRowInsteadOfFailing()
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
                    new EachNumberComparison(
                        EachComparisonOperator.EachGreaterThan,
                        new NumberArrayReturning(
                            new EachArithmetic(
                                new EachDivide(
                                    [
                                        new NumberArrayReturning(
                                            new NumberField(
                                                SampleDatabase.Orders.Entity,
                                                SampleDatabase.Orders.Total
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
            new PureQLProjection(db.Datasets, query)
        ));
    }

    // ===== KnownGap: missing column in ORDER BY is silently absorbed =====

    // OrderByApplicator sorts by CellValueExtractor.GetTextValue directly (no
    // GetRequiredCell), so a nonexistent ORDER BY field silently sorts every
    // row as null (a stable no-op order) instead of failing fast the way the
    // same typo does in SELECT.
    [Fact(
        Skip = "KnownGap: OrderByApplicator resolves fields via "
            + "CellValueExtractor's null-returning getters, so a "
            + "nonexistent ORDER BY column silently orders every row as "
            + "null instead of failing fast the way the same typo does in "
            + "SELECT."
    )]
    [Trait("Status", "KnownGap")]
    [Trait("Clause", "OrderBy")]
    public void OrderByFieldNotOnResolvedTableSilentlyOrdersAsNull()
    {
        SampleDatabase db = new SampleDatabase();

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
            where: null,
            join: null,
            groupBy: null,
            having: null,
            orderBy:
            [
                new OrderByItem(
                    new Field(
                        new StringField(SampleDatabase.Users.Entity, "user_nickname")
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        _ = Assert.Throws<KeyNotFoundException>(() => new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        ));
    }
}
