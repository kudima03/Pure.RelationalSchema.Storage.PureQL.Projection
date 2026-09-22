using System.Globalization;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayEqualities;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Types;

// Three-valued (NULL) semantics across every clause: a NULL cell is stored as
// empty text (the "user_score" column, NULL for Bob and Dan; see
// Pure.RelationalSchema.Storage.Samples.Records.UserRecord). This suite pins
// how the translator actually treats NULL today in WHERE, each*, GROUP BY,
// aggregates, DISTINCT and ORDER BY, plus separate coverage for
// numeric-extreme, calendar-edge and UUID-casing round-trips (the
// user_precision_value/user_edge_date/user_edge_datetime/user_edge_time
// columns).
[Trait("Clause", "Types")]
[Trait("Feature", "NullSemantics")]
public sealed class NullSemanticsTests
{
    // WHERE user_age = user_score (Equality -> ArrayEquality -> field vs
    // field, evaluated per row): this is the only way a non-each ("scalar"
    // family, BooleanReturning) predicate can reference a row's cells at all
    // - see WhereExpressionBuilder.BuildContainmentEquality's left&&right
    // branch. Ann/Cara/Fay's Score equals their own Age (real matches);
    // Eve's does not (a real mismatch); Bob/Dan's Score is NULL, so the
    // comparison is SQL's three-valued "unknown", not true - those rows must
    // be excluded exactly like Eve's real mismatch, never kept.
    [Fact]
    public void ScalarFieldEqualityExcludesRowsWhoseComparedCellIsNull()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField("schema_with_foreign_keys.users", "user_name")
                        )
                    )
                ),
            ],
            new BooleanReturning(
                new Equality(
                    new ArrayEquality(
                        new NumberArrayEquality(
                            new NumberArrayReturning(
                                new NumberField(
                                    "schema_with_foreign_keys.users",
                                    "user_age"
                                )
                            ),
                            new NumberArrayReturning(
                                new NumberField(
                                    "schema_with_foreign_keys.users",
                                    "user_score"
                                )
                            )
                        )
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

        string[] expected =
        [
            .. userRows
                .Where(user => user.UserScore == user.UserAge)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Ann", "Cara", "Fay"], expected);
        Assert.Equal(
            expected,
            result.Column("user_name")
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // WHERE each user_score = 30 (per-row eachEqual against a literal): a
    // NULL Score cell must drop the row from the result, not error and not
    // be silently treated as a false-negative match. Bob and Dan (both
    // NULL) are excluded for the same reason Eve (Score = 10) is - the
    // comparison never becomes true.
    [Fact]
    public void EachEqualityExcludesRowsWhoseFieldCellIsNull()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField("schema_with_foreign_keys.users", "user_name")
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachNumberEquality(
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.users",
                                "user_score"
                            )
                        ),
                        new NumberReturning(new NumberScalar(30))
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

        string[] expected =
        [
            .. userRows
                .Where(user => user.UserScore == 30)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Ann", "Cara"], expected);
        Assert.Equal(
            expected,
            result.Column("user_name")
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // GROUP BY user_score: SQL groups all NULL keys into a single group
    // (distinct from every non-NULL group). GroupByApplicator.BuildGroupKey
    // maps a NULL cell to string.Empty for every field type, so Bob and Dan
    // (both NULL) land in the same group. Count(Users.Id) - never NULL -
    // confirms that group holds exactly the two of them.
    [Fact]
    public void GroupByNullKeyCollapsesAllNullRowsIntoOneGroup()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.users",
                                "user_score"
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new Count(
                                new ArrayReturning(
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
                    "n"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new NumberField(
                        "schema_with_foreign_keys.users",
                        "user_score"
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

        int expectedGroupCount = userRows
            .Select(user => user.UserScore)
            .Distinct()
            .Count();

        Assert.Equal(4, expectedGroupCount);
        Assert.Equal(expectedGroupCount, result.Count);

        ResultRow nullGroup = Assert.Single(
            result.Rows,
            row => row["user_score"] == string.Empty
        );
        Assert.Equal(2.0, nullGroup.Double("n"));
    }

    // sum/avg/min/max over user_score: SQL-standard aggregates ignore NULL
    // cells rather than letting them poison the fold (AggregateEvaluator.Fold
    // filters with `.OfType<T>()`, dropping the two NULLs from Bob and Dan).
    [Fact]
    public void NumericAggregatesIgnoreNullCellsWhenFoldingTheWholeSet()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        double[] nonNullScores =
        [
            .. userRows.Select(user => user.UserScore).OfType<double>(),
        ];

        Assert.Equal(4, nonNullScores.Length);

        Query SumAvgMinMaxQuery(string aggregateAlias, SelectExpression expression)
        {
            return new Query(
                new FromExpression("schema_with_foreign_keys.users"),
                [expression]
            );
        }

        SelectExpression SelectOf(NumberAggregate aggregate, string alias)
        {
            return new SelectExpression(
                new SingleValueReturning(new NumberReturning(aggregate)),
                alias
            );
        }

        NumberArrayReturning scoreField = new NumberArrayReturning(
            new NumberField("schema_with_foreign_keys.users", "user_score")
        );

        ProjectionResult sumResult = new ProjectionResult(
            new PureQLProjection(
                datasets,
                SumAvgMinMaxQuery(
                    "sum_score",
                    SelectOf(new NumberAggregate(new SumNumber(scoreField)), "sum_score")
                )
            )
        );
        ProjectionResult avgResult = new ProjectionResult(
            new PureQLProjection(
                datasets,
                SumAvgMinMaxQuery(
                    "avg_score",
                    SelectOf(new NumberAggregate(new AverageNumber(scoreField)), "avg_score")
                )
            )
        );
        ProjectionResult minResult = new ProjectionResult(
            new PureQLProjection(
                datasets,
                SumAvgMinMaxQuery(
                    "min_score",
                    SelectOf(new NumberAggregate(new MinNumber(scoreField)), "min_score")
                )
            )
        );
        ProjectionResult maxResult = new ProjectionResult(
            new PureQLProjection(
                datasets,
                SumAvgMinMaxQuery(
                    "max_score",
                    SelectOf(new NumberAggregate(new MaxNumber(scoreField)), "max_score")
                )
            )
        );

        Assert.Equal(nonNullScores.Sum(), sumResult.Row(0).Double("sum_score"));
        Assert.Equal(nonNullScores.Average(), avgResult.Row(0).Double("avg_score"));
        Assert.Equal(nonNullScores.Min(), minResult.Row(0).Double("min_score"));
        Assert.Equal(nonNullScores.Max(), maxResult.Row(0).Double("max_score"));
    }

    // SELECT DISTINCT user_score: SQL treats every NULL as equal to every
    // other NULL for dedup purposes, so Bob and Dan's two NULL rows collapse
    // into a single output row (DistinctApplicator.BuildKey renders a NULL
    // cell's text as "" for every row, giving both the same dedup key).
    [Fact]
    public void DistinctCollapsesMultipleNullRowsIntoOne()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.users",
                                "user_score"
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null,
            distinct: true
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expectedDistinctCount = userRows
            .Select(user => user.UserScore)
            .Distinct()
            .Count();

        Assert.Equal(4, expectedDistinctCount);
        Assert.Equal(expectedDistinctCount, result.Count);
        _ = Assert.Single(
            result.Rows,
            row => row["user_score"] == string.Empty
        );
    }

    // ORDER BY user_score ASC/DESC: OrderByApplicator implements an
    // intentional NULLS LAST contract, regardless of sort direction
    // (matching PostgreSQL/Oracle/SQL Server's default) - see issue #125.
    // Bob and Dan's NULL Score cells must always sort after every non-NULL
    // Score, whether ascending or descending. The expected sequence below
    // is built explicitly as non-NULL rows (sorted by score in the
    // requested direction) followed by NULL rows in their original
    // relative order, mirroring that contract rather than relying on
    // .NET's default Nullable<T> comparer (which would place NULLs first
    // for ascending).
    [Fact]
    public void OrderByAscendingPlacesNullScoreCellsLast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField("schema_with_foreign_keys.users", "user_name")
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
                        new NumberField(
                            "schema_with_foreign_keys.users",
                            "user_score"
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
            .. userRows
                .Where(user => user.UserScore.HasValue)
                .OrderBy(user => user.UserScore)
                .Select(user => user.UserName),
            .. userRows
                .Where(user => !user.UserScore.HasValue)
                .Select(user => user.UserName),
        ];

        Assert.Equal(["Bob", "Dan"], expected[^2..]);
        Assert.Equal(expected, result.Column("user_name").ToArray());
    }

    // Descending companion: proves NULLS LAST holds in the direction where
    // the old default Nullable<T> comparer already happened to agree, so
    // this test alone cannot distinguish the old and new behavior - it is
    // the ascending test above (now renamed to ...PlacesNullsLast) that
    // pins the actual behavior change.
    [Fact]
    public void OrderByDescendingPlacesNullScoreCellsLast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField("schema_with_foreign_keys.users", "user_name")
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
                        new NumberField(
                            "schema_with_foreign_keys.users",
                            "user_score"
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

        // For a descending sort, NULLS LAST happens to coincide with what
        // .NET's default Nullable<T> comparer already produces (null
        // compares as the smallest value, so it sorts last when
        // descending), so a plain OrderByDescending over the ground-truth
        // records still reflects the intentional contract here.
        string[] expected =
        [
            .. userRows
                .OrderByDescending(user => user.UserScore)
                .Select(user => user.UserName),
        ];

        Assert.Equal(["Bob", "Dan"], expected[^2..]);
        Assert.Equal(expected, result.Column("user_name").ToArray());
    }

    // Numeric precision/extremes: double.MaxValue/MinValue, the smallest
    // representable positive/negative subnormal (double.Epsilon), a value
    // near the exponent limit (1e308) and a value with many significant
    // digits that would suffer rounding if formatted with anything less
    // than a round-trippable format. InvariantCellText's double formatting
    // is round-trippable by default since .NET Core 3.0, and
    // CellValueExtractor.GetDoubleValue parses it back with the same
    // invariant culture, so every value below must survive the storage-text
    // round trip exactly.
    [Fact]
    public void ExtremeAndPrecisionSensitiveDoublesRoundTripExactly()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.users",
                                "user_precision_value"
                            )
                        )
                    )
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            [.. userRows.Select(user => (double?)user.UserPrecisionValue)],
            [.. result.Rows.Select(row => row.Double("user_precision_value"))]
        );
        Assert.Contains(
            double.MaxValue,
            userRows.Select(user => user.UserPrecisionValue)
        );
        Assert.Contains(
            double.MinValue,
            userRows.Select(user => user.UserPrecisionValue)
        );
        Assert.Contains(
            double.Epsilon,
            userRows.Select(user => user.UserPrecisionValue)
        );
    }

    // Date/DateTime/Time edge-value round trips: midnight and end-of-day
    // times, a leap-year date (2024-02-29), and two DST-adjacent-looking
    // instants (2024-03-10 02:30 / 2024-11-03 01:30 - the US spring-forward
    // gap and fall-back-ambiguous hour). The library's model is UTC/
    // offset-naive, so nothing here should be affected by DST at all - that
    // is exactly what this test confirms by round-tripping the raw values
    // with no timezone conversion applied anywhere in the pipeline.
    [Fact]
    public void CalendarEdgeValuesRoundTripAcrossDateDateTimeAndTime()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new DateArrayReturning(
                            new DateField(
                                "schema_with_foreign_keys.users",
                                "user_edge_date"
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                "schema_with_foreign_keys.users",
                                "user_edge_datetime"
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new TimeArrayReturning(
                            new TimeField(
                                "schema_with_foreign_keys.users",
                                "user_edge_time"
                            )
                        )
                    )
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            [.. userRows.Select(user => (DateOnly?)user.UserEdgeDate)],
            [.. result.Rows.Select(row => row.Date("user_edge_date"))]
        );
        Assert.Equal(
            [.. userRows.Select(user => (DateTime?)user.UserEdgeDateTime)],
            [.. result.Rows.Select(row => row.DateTime("user_edge_datetime"))]
        );
        Assert.Equal(
            [.. userRows.Select(user => (TimeOnly?)user.UserEdgeTime)],
            [.. result.Rows.Select(row => row.Time("user_edge_time"))]
        );
        Assert.Contains(
            new DateOnly(2024, 2, 29),
            userRows.Select(user => user.UserEdgeDate)
        );
        Assert.Contains(
            new TimeOnly(0, 0, 0),
            userRows.Select(user => user.UserEdgeTime)
        );
        Assert.Contains(
            new TimeOnly(23, 59, 59),
            userRows.Select(user => user.UserEdgeTime)
        );
    }

    // Confirms the round trip above has no ambient-locale dependency: run
    // the same query with the current thread's culture switched to one
    // whose date/number formatting differs sharply from invariant (comma
    // decimal separator, day-first dates) and assert identical results.
    // The package's InvariantCellText and this translator's
    // CellValueExtractor both fix CultureInfo.InvariantCulture explicitly,
    // so ambient culture must have no effect.
    [Fact]
    public void CalendarAndNumericRoundTripsAreUnaffectedByAmbientCulture()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                "schema_with_foreign_keys.users",
                                "user_edge_datetime"
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                "schema_with_foreign_keys.users",
                                "user_precision_value"
                            )
                        )
                    )
                ),
            ]
        );

        CultureInfo original = CultureInfo.CurrentCulture;
        ProjectionResult result;

        try
        {
            CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo("de-DE");
            result = new ProjectionResult(new PureQLProjection(datasets, query));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }

        Assert.Equal(
            [.. userRows.Select(user => (DateTime?)user.UserEdgeDateTime)],
            [.. result.Rows.Select(row => row.DateTime("user_edge_datetime"))]
        );
        Assert.Equal(
            [.. userRows.Select(user => (double?)user.UserPrecisionValue)],
            [.. result.Rows.Select(row => row.Double("user_precision_value"))]
        );
    }

    // UUID casing: a stored cell text in uppercase hex must parse to the
    // same logical Guid as the equivalent lowercase text, and the two must
    // compare equal under the translator's own field-vs-field equality path
    // (Guid.TryParse is case-insensitive; CellValueExtractor.GetGuidValue
    // relies on exactly that). UuidCasingSchemaDataSet stores the same
    // logical Guid once with lowercase hex text and once with uppercase hex
    // text, so the stored text itself differs only in casing between the
    // two rows.
    [Fact]
    public void UppercaseAndLowercaseUuidTextCompareEqual()
    {
        IEnumerable<IStoredSchemaDataSet> datasets = [new UuidCasingSchemaDataSet()];

        Query query = new Query(
            new FromExpression("schema_without_foreign_keys.table_without_indexes"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_without_foreign_keys.table_without_indexes",
                                "name"
                            )
                        )
                    )
                ),
            ],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                "schema_without_foreign_keys.table_without_indexes",
                                "id"
                            )
                        ),
                        new UuidReturning(
                            new UuidScalar(
                                new Guid("0f9e8d7c-6b5a-4938-8271-605f4e3d2c1b")
                            )
                        )
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

        string?[] expectedLabels = ["lowercase", "uppercase"];

        Assert.Equal(2, result.Count);
        Assert.Equal(
            expectedLabels,
            result.Column("name")
                .OrderBy(label => label, StringComparer.Ordinal)
                .ToArray()
        );
    }
}
