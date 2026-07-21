# Deliberately-excluded semantics

This suite asserts only behaviour that the PureQL specification and
`PureQL.CSharp.Model` define. The cases below are **spec-ambiguous or
unspecified**; a strict test would have to invent a convention, so they are
documented here instead of asserted. Each cites the relevant entry in
`PureQL-Specification/SPEC-REVIEW.md`. When the spec pins one of these down, add
a category (or promote an existing `KnownGap` test) accordingly.

| Area | Why it is excluded | Spec ref |
| --- | --- | --- |
| String collation | Ordering/`>`/`min`/`max` on strings has no defined locale rule; the translator consistently uses ordinal comparison, which is an implementation choice, not a spec-mandated one. String comparison tests use lowercase ASCII values whose ordinal order is collation-independent. | U-6 |
| Cross-type temporal comparison | `PureQL.CSharp.Model.Comparisons` defines separate `DateComparison`/`DateTimeComparison`/`TimeComparison` types, each pairing same-typed operands — the model has no shape that allows comparing across `date`/`datetime`/`time` at all, so this is structurally impossible rather than merely undefined. Tests keep each temporal comparison within one type. | U-5 |
| Self-joins | `joinItem` has no per-join alias. `EntityReferenceValidator` does not reject `join.Entity == query.From.Entity`, and `CellValueExtractor.GetCell` always prefers the `QualifiedColumn` matching the entity string — since both join sides share the same entity string, every field reference on *either* side resolves to the same (joined) cell. Empirically (self-join `shop.users` to itself on `id == id`, 6 rows) this makes the ON condition tautological and silently returns the full cross product (36 rows), not an error. **This is a translator correctness bug, not just an ambiguity** — tracked separately as a defect, see the Join expansion issue. | P-5 |

## Previously listed here, now confirmed as defined, deterministic behaviour

An earlier version of this README listed the six items below as spec-ambiguous.
Reading the current translator source (and, for the self-join case above,
empirical confirmation) shows each has settled on a concrete, deterministic
rule. They are no longer "excluded" — they are testable behaviour and should
get real assertions (tracked in the relevant #72 sub-issues) rather than being
documented as gaps:

| Area | Actual behaviour | Source |
| --- | --- | --- |
| Outer-join null extension | Unmatched side is padded with a fixed empty cell (`JoinApplicator.Pad`). Reading it back: string columns extract as `""`; every other typed column (`double`/`uuid`/`date`/`datetime`/`time`/`bool`) fails to parse and extracts as `null`. Deterministic, never throws. | `JoinApplicator.cs` (`Pad`), `CellValueExtractor.cs` |
| Empty-group / empty-set aggregates | `count` over an empty group is `0` (`Count(hasValue)` over zero rows). `sum`/`min`/`max`/`average` over an empty group fold to `null`. A whole-set aggregate (no GROUP BY) over zero matching rows still emits exactly one row: count 0, other aggregates null. | `AggregateEvaluator.cs` (`Fold`/`FoldString`/`BuildCount`), `GroupByApplicator.cs` (`WholeSetGroup`) |
| `eachDivide` by zero | Returns `null` for that row; never throws `DivideByZeroException`. | `WhereExpressionBuilder.cs` (`DivideDoubles`) |
| Literal-array each* operand vs. row count | Not actually a "zip mismatch" risk: a literal array operand is never zipped by index at all — every per-row evaluation uses only its first element (`.FirstOrDefault()`), broadcast to every row regardless of the literal's declared length or the table's row count. | `WhereExpressionBuilder.cs` (each `Build*ArrayValuePerRow` literal arm) |
| `count` over a boolean vector | Counts non-null values in the vector (standard SQL `COUNT(column)` semantics) — not vector length, not count-of-`true`s. | `AggregateEvaluator.cs` (`HasValueSelector`/`BuildCount`) |
| `and`/`or` over a bare boolean vector | Fully implemented: a literal `booleanArrayReturning` operand folds with `.All(v => v)`; a field-reference operand does a per-row `field == true` comparison. No ambiguity in practice since predicates are always evaluated per row. | `WhereExpressionBuilder.cs` (`BuildBooleanOperator`, `BuildBoolArrayPerRow`) |

## Separately: remaining translator gaps (fail fast, no tests to enable)

Some behaviour is defined by the spec but still unimplemented; the translator
raises `NotSupportedException` instead of guessing. These have no spec-correct
tests waiting to be enabled (their exact semantics either need a public API or
a spec decision first):

- Parameter binding (`Parameters/ParameterTests` pins the fail-fast contract;
  the public API exposes no way to supply values).
- Computed/expression `select` columns (single-value `Arithmetic`). Note:
  scalar *constants* (`SELECT 5 AS x`) are implemented and covered by
  `Select/ScalarProjectionTests.cs` — only arithmetic/computed expressions in
  `select` still fail fast (`Select/ScalarUnsupportedTests.cs`,
  `Select/SelectExpansionTests.cs:AliasRenamesComputedArithmeticColumn`).
- Temporal `average` aggregates (rounding undefined; still throws
  `NotSupportedException` in `AggregateEvaluator.cs`).
- Aggregates inside `where` (`WhereExpressionBuilder.AggregateNotSupported`).
