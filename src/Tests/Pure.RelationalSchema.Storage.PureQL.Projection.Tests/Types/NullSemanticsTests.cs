using System.Globalization;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
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
// empty text (SampleDatabase.Users.Score, NULL for Bob and Dan; see
// SampleRecords.cs and CellText.From(double?)). This suite pins how the
// translator actually treats NULL today in WHERE, each*, GROUP BY, aggregates,
// DISTINCT and ORDER BY, plus separate coverage for numeric-extreme,
// calendar-edge and UUID-casing round-trips (SampleDatabase.Users
// PrecisionValue/EdgeDate/EdgeDateTime/EdgeTime columns).
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
            new BooleanReturning(
                new Equality(
                    new ArrayEquality(
                        new NumberArrayEquality(
                            new NumberArrayReturning(
                                new NumberField(
                                    SampleDatabase.Users.Entity,
                                    SampleDatabase.Users.Age
                                )
                            ),
                            new NumberArrayReturning(
                                new NumberField(
                                    SampleDatabase.Users.Entity,
                                    SampleDatabase.Users.Score
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
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.UserRows
                .Where(user => user.Score == user.UserAge)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Ann", "Cara", "Fay"], expected);
        Assert.Equal(
            expected,
            result.Column(SampleDatabase.Users.Name)
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
            new BooleanArrayReturning(
                new EachEquality(
                    new EachNumberEquality(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Score
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
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.UserRows
                .Where(user => user.Score == 30)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Ann", "Cara"], expected);
        Assert.Equal(
            expected,
            result.Column(SampleDatabase.Users.Name)
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
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Score
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
                                            SampleDatabase.Users.Entity,
                                            SampleDatabase.Users.Id
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
                        SampleDatabase.Users.Entity,
                        SampleDatabase.Users.Score
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int expectedGroupCount = db.UserRows
            .Select(user => user.Score)
            .Distinct()
            .Count();

        Assert.Equal(4, expectedGroupCount);
        Assert.Equal(expectedGroupCount, result.Count);

        ResultRow nullGroup = Assert.Single(
            result.Rows,
            row => row[SampleDatabase.Users.Score] == string.Empty
        );
        Assert.Equal(2.0, nullGroup.Double("n"));
    }

    // sum/avg/min/max over user_score: SQL-standard aggregates ignore NULL
    // cells rather than letting them poison the fold (AggregateEvaluator.Fold
    // filters with `.OfType<T>()`, dropping the two NULLs from Bob and Dan).
    [Fact]
    public void NumericAggregatesIgnoreNullCellsWhenFoldingTheWholeSet()
    {
        SampleDatabase db = new SampleDatabase();

        double[] nonNullScores =
        [
            .. db.UserRows.Select(user => user.Score).OfType<double>(),
        ];

        Assert.Equal(4, nonNullScores.Length);

        Query SumAvgMinMaxQuery(string aggregateAlias, SelectExpression expression)
        {
            return new Query(
                new FromExpression(SampleDatabase.Users.Entity),
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
            new NumberField(SampleDatabase.Users.Entity, SampleDatabase.Users.Score)
        );

        ProjectionResult sumResult = new ProjectionResult(
            new PureQLProjection(
                db.Datasets,
                SumAvgMinMaxQuery(
                    "sum_score",
                    SelectOf(new NumberAggregate(new SumNumber(scoreField)), "sum_score")
                )
            )
        );
        ProjectionResult avgResult = new ProjectionResult(
            new PureQLProjection(
                db.Datasets,
                SumAvgMinMaxQuery(
                    "avg_score",
                    SelectOf(new NumberAggregate(new AverageNumber(scoreField)), "avg_score")
                )
            )
        );
        ProjectionResult minResult = new ProjectionResult(
            new PureQLProjection(
                db.Datasets,
                SumAvgMinMaxQuery(
                    "min_score",
                    SelectOf(new NumberAggregate(new MinNumber(scoreField)), "min_score")
                )
            )
        );
        ProjectionResult maxResult = new ProjectionResult(
            new PureQLProjection(
                db.Datasets,
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
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Score
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
            new PureQLProjection(db.Datasets, query)
        );

        int expectedDistinctCount = db.UserRows
            .Select(user => user.Score)
            .Distinct()
            .Count();

        Assert.Equal(4, expectedDistinctCount);
        Assert.Equal(expectedDistinctCount, result.Count);
        _ = Assert.Single(
            result.Rows,
            row => row[SampleDatabase.Users.Score] == string.Empty
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
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Score
                        )
                    ),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.UserRows
                .Where(user => user.Score.HasValue)
                .OrderBy(user => user.Score)
                .Select(user => user.UserName),
            .. db.UserRows
                .Where(user => !user.Score.HasValue)
                .Select(user => user.UserName),
        ];

        Assert.Equal(["Bob", "Dan"], expected[^2..]);
        Assert.Equal(expected, result.Column(SampleDatabase.Users.Name).ToArray());
    }

    // Descending companion: proves NULLS LAST holds in the direction where
    // the old default Nullable<T> comparer already happened to agree, so
    // this test alone cannot distinguish the old and new behavior - it is
    // the ascending test above (now renamed to ...PlacesNullsLast) that
    // pins the actual behavior change.
    [Fact]
    public void OrderByDescendingPlacesNullScoreCellsLast()
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
            [
                new OrderByItem(
                    new Field(
                        new NumberField(
                            SampleDatabase.Users.Entity,
                            SampleDatabase.Users.Score
                        )
                    ),
                    SortDirection.Desc
                ),
            ],
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        // For a descending sort, NULLS LAST happens to coincide with what
        // .NET's default Nullable<T> comparer already produces (null
        // compares as the smallest value, so it sorts last when
        // descending), so a plain OrderByDescending over the ground-truth
        // records still reflects the intentional contract here.
        string[] expected =
        [
            .. db.UserRows
                .OrderByDescending(user => user.Score)
                .Select(user => user.UserName),
        ];

        Assert.Equal(["Bob", "Dan"], expected[^2..]);
        Assert.Equal(expected, result.Column(SampleDatabase.Users.Name).ToArray());
    }

    // Numeric precision/extremes: double.MaxValue/MinValue, the smallest
    // representable positive/negative subnormal (double.Epsilon), a value
    // near the exponent limit (1e308) and a value with many significant
    // digits that would suffer rounding if formatted with anything less
    // than a round-trippable format. CellText.From(double) uses plain
    // ToString(InvariantCulture) - round-trippable by default since
    // .NET Core 3.0 - and CellValueExtractor.GetDoubleValue parses it back
    // with the same invariant culture, so every value below must survive
    // the storage-text round trip exactly.
    [Fact]
    public void ExtremeAndPrecisionSensitiveDoublesRoundTripExactly()
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
                                SampleDatabase.Users.PrecisionValue
                            )
                        )
                    )
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            [.. db.UserRows.Select(user => (double?)user.PrecisionValue)],
            [.. result.Rows.Select(row => row.Double(SampleDatabase.Users.PrecisionValue))]
        );
        Assert.Contains(double.MaxValue, db.UserRows.Select(user => user.PrecisionValue));
        Assert.Contains(double.MinValue, db.UserRows.Select(user => user.PrecisionValue));
        Assert.Contains(
            double.Epsilon,
            db.UserRows.Select(user => user.PrecisionValue)
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
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new DateArrayReturning(
                            new DateField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.EdgeDate
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new DateTimeArrayReturning(
                            new DateTimeField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.EdgeDateTime
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new TimeArrayReturning(
                            new TimeField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.EdgeTime
                            )
                        )
                    )
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        Assert.Equal(
            [.. db.UserRows.Select(user => (DateOnly?)user.EdgeDate)],
            [.. result.Rows.Select(row => row.Date(SampleDatabase.Users.EdgeDate))]
        );
        Assert.Equal(
            [.. db.UserRows.Select(user => (DateTime?)user.EdgeDateTime)],
            [.. result.Rows.Select(row => row.DateTime(SampleDatabase.Users.EdgeDateTime))]
        );
        Assert.Equal(
            [.. db.UserRows.Select(user => (TimeOnly?)user.EdgeTime)],
            [.. result.Rows.Select(row => row.Time(SampleDatabase.Users.EdgeTime))]
        );
        Assert.Contains(
            new DateOnly(2024, 2, 29),
            db.UserRows.Select(user => user.EdgeDate)
        );
        Assert.Contains(new TimeOnly(0, 0, 0), db.UserRows.Select(user => user.EdgeTime));
        Assert.Contains(
            new TimeOnly(23, 59, 59),
            db.UserRows.Select(user => user.EdgeTime)
        );
    }

    // Confirms the round trip above has no ambient-locale dependency: run
    // the same query with the current thread's culture switched to one
    // whose date/number formatting differs sharply from invariant (comma
    // decimal separator, day-first dates) and assert identical results.
    // CellText/CellValueExtractor both fix CultureInfo.InvariantCulture
    // explicitly, so ambient culture must have no effect.
    [Fact]
    public void CalendarAndNumericRoundTripsAreUnaffectedByAmbientCulture()
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
                                SampleDatabase.Users.EdgeDateTime
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.PrecisionValue
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
            result = new ProjectionResult(new PureQLProjection(db.Datasets, query));
        }
        finally
        {
            CultureInfo.CurrentCulture = original;
        }

        Assert.Equal(
            [.. db.UserRows.Select(user => (DateTime?)user.EdgeDateTime)],
            [.. result.Rows.Select(row => row.DateTime(SampleDatabase.Users.EdgeDateTime))]
        );
        Assert.Equal(
            [.. db.UserRows.Select(user => (double?)user.PrecisionValue)],
            [.. result.Rows.Select(row => row.Double(SampleDatabase.Users.PrecisionValue))]
        );
    }

    // UUID casing: a stored cell text in uppercase hex must parse to the
    // same logical Guid as the equivalent lowercase text, and the two must
    // compare equal under the translator's own field-vs-field equality path
    // (Guid.TryParse is case-insensitive; CellValueExtractor.GetGuidValue
    // relies on exactly that). A tiny bespoke one-table dataset is built
    // here (not through SampleDatabase, which always formats UUIDs
    // lowercase via Guid.ToString()) so the stored text itself differs only
    // in casing between the two rows.
    [Fact]
    public void UppercaseAndLowercaseUuidTextCompareEqual()
    {
        UuidCasingDatabase db = new UuidCasingDatabase();

        Query query = new Query(
            new FromExpression(UuidCasingDatabase.Things.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                UuidCasingDatabase.Things.Entity,
                                UuidCasingDatabase.Things.Label
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
                                UuidCasingDatabase.Things.Entity,
                                UuidCasingDatabase.Things.Id
                            )
                        ),
                        new UuidReturning(new UuidScalar(UuidCasingDatabase.SharedId))
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
            new PureQLProjection(db.Datasets, query)
        );

        string?[] expectedLabels = ["lowercase", "uppercase"];

        Assert.Equal(2, result.Count);
        Assert.Equal(
            expectedLabels,
            result.Column(UuidCasingDatabase.Things.Label)
                .OrderBy(label => label, StringComparer.Ordinal)
                .ToArray()
        );
    }
}
