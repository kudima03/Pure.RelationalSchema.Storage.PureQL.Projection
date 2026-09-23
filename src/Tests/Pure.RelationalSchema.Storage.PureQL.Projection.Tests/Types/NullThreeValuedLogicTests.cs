using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Types;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Types;

// SQL three-valued logic (3VL) threaded through operator combinations that
// Types/NullSemanticsTests.cs does not already cover: each-comparison and
// each-arithmetic dropping NULL rows, negation of a NULL-cell equality,
// HAVING over a group whose members mix NULL and non-NULL Score, and
// string/temporal aggregate NULL-ignoring sourced from LEFT JOIN padding
// (JoinApplicator.Pad pads the unmatched side with an empty cell; every
// typed column - string included, as of issue #167 - reads that back as
// null via CellValueExtractor; see Semantics/README.md "Outer-join null
// extension"). Every expectation below is computed independently from the
// ground-truth record lists under SQL semantics: a predicate comparison
// against NULL is unknown (row excluded); NOT(unknown) stays unknown
// (issue #166); arithmetic with a NULL operand is NULL (excluded);
// aggregates ignore NULL inputs. This file originally shipped four of
// these as KnownGap-skipped, SQL-correct-but-then-unimplemented
// expectations (issues #166/#167); all four now pass unskipped.
[Trait("Clause", "Types")]
[Trait("Feature", "NullThreeValuedLogic")]
public sealed class NullThreeValuedLogicTests
{
    // ===== each-comparison over Users.Score (all 4 range operators) =====

    // WHERE each user_score > 20: Bob/Dan's NULL Score makes the comparison
    // unknown, so they are excluded exactly like Eve (Score = 10, a real
    // mismatch), never treated as satisfying or as an error.
    [Fact]
    public void EachNumberGreaterThanExcludesNullScoreRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new EachNullableScoreGreaterThanQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows
                .Where(user => user.UserScore.HasValue && user.UserScore > 20)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Ann", "Cara", "Fay"], expected);
        Assert.Equal(
            expected,
            result.Column(new UserNameColumn().Name.TextValue)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // WHERE each user_score >= 28: same NULL-exclusion contract as above,
    // exercised against the inclusive operator.
    [Fact]
    public void EachNumberGreaterThanOrEqualExcludesNullScoreRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new EachNullableScoreGreaterThanOrEqualQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows
                .Where(user => user.UserScore.HasValue && user.UserScore >= 28)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Ann", "Cara", "Fay"], expected);
        Assert.Equal(
            expected,
            result.Column(new UserNameColumn().Name.TextValue)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // WHERE each user_score < 29: Ann/Cara (Score = 30) fail the real
    // comparison; Bob/Dan fail because NULL is unknown, not because 30 was
    // ever evaluated. Both reasons must produce the same exclusion.
    [Fact]
    public void EachNumberLessThanExcludesNullScoreRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new EachNullableScoreLessThanQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows
                .Where(user => user.UserScore.HasValue && user.UserScore < 29)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Eve", "Fay"], expected);
        Assert.Equal(
            expected,
            result.Column(new UserNameColumn().Name.TextValue)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // WHERE each user_score <= 10: only Eve's real Score qualifies; Bob/Dan
    // never qualify via their NULL cell however permissive the threshold.
    [Fact]
    public void EachNumberLessThanOrEqualExcludesNullScoreRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new EachNullableScoreLessThanOrEqualQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows
                .Where(user => user.UserScore.HasValue && user.UserScore <= 10)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Eve"], expected);
        Assert.Equal(
            expected,
            result.Column(new UserNameColumn().Name.TextValue)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // ===== each-arithmetic with a NULL Score operand =====

    // WHERE each (user_score + 1) > -1000: the threshold is satisfied by
    // every real Score value, so only NULL propagation through eachAdd (not
    // a real comparison failure) can remove a row. Bob/Dan's NULL Score
    // makes the sum NULL, which is excluded rather than treated as
    // satisfying an always-true-looking threshold.
    [Fact]
    public void EachAddWithNullScoreOperandExcludesRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new EachAddWithNullScoreOperandQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows
                .Where(user => user.UserScore.HasValue)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Ann", "Cara", "Eve", "Fay"], expected);
        Assert.Equal(
            expected,
            result.Column(new UserNameColumn().Name.TextValue)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // WHERE each (user_score - 10) > 15: Score - 10 > 15 <=> Score > 25, a
    // real partial match (Ann/Cara/Fay, not Eve); Bob/Dan's NULL Score
    // yields a NULL difference, excluded regardless of the threshold.
    [Fact]
    public void EachSubtractWithNullScoreOperandExcludesRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new EachSubtractWithNullScoreOperandQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows
                .Where(user => user.UserScore.HasValue && user.UserScore - 10 > 15)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Ann", "Cara", "Fay"], expected);
        Assert.Equal(
            expected,
            result.Column(new UserNameColumn().Name.TextValue)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // WHERE each (user_score * 2) > 50: Score * 2 > 50 <=> Score > 25 (same
    // real partial match as above via a different operator); Bob/Dan's NULL
    // Score makes the product NULL, excluded.
    [Fact]
    public void EachMultiplyWithNullScoreOperandExcludesRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new EachMultiplyWithNullScoreOperandQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows
                .Where(user => user.UserScore.HasValue && user.UserScore * 2 > 50)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Ann", "Cara", "Fay"], expected);
        Assert.Equal(
            expected,
            result.Column(new UserNameColumn().Name.TextValue)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // WHERE each (user_score / 2) > -1000: an always-true-for-reals
    // threshold, isolating NULL propagation through eachDivide the same way
    // as the eachAdd case above. Bob/Dan's NULL Score divided by 2 is NULL,
    // excluded rather than satisfying the permissive threshold.
    [Fact]
    public void EachDivideWithNullScoreOperandExcludesRow()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new EachDivideWithNullScoreOperandQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows
                .Where(user => user.UserScore.HasValue)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Ann", "Cara", "Eve", "Fay"], expected);
        Assert.Equal(
            expected,
            result.Column(new UserNameColumn().Name.TextValue)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // ===== Negating a NULL-cell equality (scalar and each family) =====

    // WHERE NOT(user_age = user_score): field-vs-field equality is the only
    // shape through which the scalar (non-each) BooleanReturning family can
    // reference a row's cells at all (Comparisons.NumberComparison etc. take
    // only single-value operands, never a field - see WhereExpressionBuilder
    // .BuildNumberReturningAsExpr). Under SQL 3VL, NOT(unknown) is still
    // unknown, so Bob/Dan's NULL-Score comparison must stay excluded after
    // negation, exactly as it was before negation; only Eve's real mismatch
    // (Age 25 != Score 10, a genuine false) should flip to true and appear.
    // The translator instead compiles the field-vs-field equality with C#'s
    // lifted nullable `==`, which yields `false` (not "unknown") for a NULL
    // operand, so `Expression.Not` flips that `false` to `true` and Bob/Dan
    // incorrectly reappear in the negated result. KnownGap: candidate bug -
    // NOT() over a NULL-cell scalar equality does not preserve SQL 3VL.
    [Fact]
    public void NotOfScalarFieldEqualityStillExcludesNullRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new NotOfScalarFieldEqualityOverNullableScoreQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        // SQL-correct: NOT(unknown) stays excluded for Bob/Dan; only Eve's
        // genuine mismatch (25 != 10) flips from false to true.
        string[] expected =
        [
            .. userRows
                .Where(user => user.UserScore.HasValue && user.UserScore != user.UserAge)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Eve"], expected);
        Assert.Equal(
            expected,
            result.Column(new UserNameColumn().Name.TextValue)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // WHERE eachNot(each user_score = 30): the each-family analogue of the
    // scalar NOT bug above. SQL 3VL says NOT(unknown) is still unknown, so
    // Bob/Dan (NULL Score) must stay excluded; only Eve/Fay's genuine
    // mismatches (10 != 30, 28 != 30) should flip from false to true.
    // Empirically the translator's eachNot re-admits Bob/Dan the same way
    // NotOperator does for the scalar family. KnownGap: candidate bug.
    [Fact]
    public void EachNotOfEachEqualityStillExcludesNullScoreRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new EachNotOfEachEqualityOverNullableScoreQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        // SQL-correct: NOT(unknown) stays excluded for Bob/Dan; Eve/Fay's
        // genuine mismatches (10 != 30, 28 != 30) flip from false to true.
        string[] expected =
        [
            .. userRows
                .Where(user => user.UserScore.HasValue && user.UserScore != 30)
                .Select(user => user.UserName)
                .OrderBy(name => name, StringComparer.Ordinal),
        ];

        Assert.Equal(["Eve", "Fay"], expected);
        Assert.Equal(
            expected,
            result.Column(new UserNameColumn().Name.TextValue)
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray()
        );
    }

    // ===== HAVING over a group mixing NULL and non-NULL Score =====

    // GROUP BY user_active HAVING avg(user_score) > 15: the true-group
    // (Ann 30, Cara 30, Dan NULL, Fay 28) mixes a NULL Score with three real
    // ones; avg must ignore Dan's NULL and fold only the three real values,
    // clearing the HAVING threshold. The false-group (Bob NULL, Eve 10) has
    // only one real Score (10), averaging to 10 and failing the threshold,
    // so exactly one group survives.
    [Fact]
    public void HavingAverageIgnoresNullScoreAcrossMixedGroup()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new HavingAverageQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double expectedAverage = userRows
            .Where(user => user.UserActive)
            .Select(user => user.UserScore)
            .OfType<double>()
            .Average();

        Assert.Equal(1, result.Count);
        Assert.Equal("True", result.Row(0)[new UserActiveColumn().Name.TextValue]);
        Assert.Equal(expectedAverage, result.Row(0).Double("avg_score"));
    }

    // ===== Other-type aggregate NULL-ignoring via LEFT JOIN padding =====

    // SELECT min(order_placed_on) after users LEFT JOIN orders: Eve and Fay
    // have no orders, so their joined row is padded with a NULL PlacedOn
    // (JoinApplicator.Pad -> empty cell -> CellValueExtractor.GetDateOnly
    // Value returns null for empty text). min() must ignore those two
    // padded NULLs and fold only the six real order dates.
    [Fact]
    public void LeftJoinMinPlacedOnIgnoresPaddedNullRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new LeftJoinMinPlacedOnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        DateOnly expected = orderRows.Min(order => order.PlacedOn);

        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0).Date("min_placed_on"));
    }

    // SELECT max(placed_at) after users LEFT JOIN orders: same padded-NULL
    // source as above but for the datetime-typed PlacedAt column and the
    // max direction, so both aggregate directions are covered for a
    // temporal type sourced purely from join padding.
    [Fact]
    public void LeftJoinMaxPlacedAtIgnoresPaddedNullRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new LeftJoinMaxPlacedAtQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        DateTime expected = orderRows.Max(order => order.PlacedAt);

        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0).DateTime("max_placed_at"));
    }

    // SELECT max(order_status) after users LEFT JOIN orders: string columns
    // pad to null (CellValueExtractor.GetTextValue maps the pad's empty
    // text to null, same as every other typed getter - issue #167), so the
    // padded rows are excluded from the fold outright. Before that fix the
    // pad read back as a real, non-null "" - which still never won a MAX
    // fold (it ordinally precedes every real status) - so this direction
    // already matched the SQL-correct answer even under the old, wrong
    // extraction; MIN below is the direction that only the fix corrects.
    [Fact]
    public void LeftJoinMaxStatusIgnoresPaddedRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new LeftJoinMaxStatusQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string expected = orderRows.Max(order => order.OrderStatus)!;

        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0)["max_status"]);
    }

    // SELECT min(order_status) after users LEFT JOIN orders: unlike max
    // above, an empty-string pad always wins an ordinal MIN fold, so the
    // padded rows for Eve/Fay's unmatched join are not harmless here - SQL
    // (ignoring the two NULL/padded rows) would give "cancelled" (the
    // ordinal minimum of the six real statuses), but the translator's
    // FoldString only filters true C# nulls via `.OfType<string>()`, and a
    // padded "" is a real (non-null) empty string, so it wrongly survives
    // and wins the fold. KnownGap: candidate bug - the empty-string pad
    // participates in string MIN instead of being ignored like a NULL.
    [Fact]
    public void LeftJoinMinStatusIgnoresPaddedNullRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new LeftJoinMinStatusQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string expected = orderRows.Min(order => order.OrderStatus)!;

        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0)["min_status"]);
    }

    // count(order_total) after users LEFT JOIN orders: Total is a double
    // column, so a padded row's cell reads back as a true null (unlike the
    // string case above), and count() correctly counts only the six
    // matched rows, ignoring Eve/Fay's two padded rows entirely.
    [Fact]
    public void LeftJoinCountOfNumericColumnIgnoresPaddedNullRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new LeftJoinCountOfNumericColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(orderRows.Count, result.Row(0).Double("total_count"));
    }

    // count(order_status) after users LEFT JOIN orders: the string-family
    // counterpart of the numeric count above. SQL's count(column) ignores
    // NULL, so it should count only the six matched rows (complementing
    // issue #140's NULL-exclusion test with a join-sourced NULL) - but
    // AggregateEvaluator.HasValueSelector's string branch treats "selector
    // is not null" as presence, and a padded row's string selector returns
    // a real (non-null) "", so both padded rows are wrongly counted as
    // present. KnownGap: candidate bug, the same root cause (empty-string
    // pad vs. true null) as the string MIN divergence above.
    [Fact]
    public void LeftJoinCountOfStringColumnIgnoresPaddedNullRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new LeftJoinCountOfStringColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(1, result.Count);
        Assert.Equal(orderRows.Count, result.Row(0).Double("status_count"));
    }
}
