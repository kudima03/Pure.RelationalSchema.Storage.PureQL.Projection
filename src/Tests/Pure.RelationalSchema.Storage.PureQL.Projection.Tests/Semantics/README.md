# Deliberately-excluded semantics

This suite asserts only behaviour that the PureQL specification and
`PureQL.CSharp.Model` define. The cases below are **spec-ambiguous or
unspecified**; a strict test would have to invent a convention, so they are
documented here instead of asserted. Each cites the relevant entry in
`PureQL-Specification/SPEC-REVIEW.md`. When the spec pins one of these down, add
a category (or promote an existing `KnownGap` test) accordingly.

| Area | Why it is excluded | Spec ref |
| --- | --- | --- |
| Outer-join null extension | On the unmatched side of a LEFT/RIGHT/FULL join, the columns of the other table are absent. Whether they should read as `null`, exclude the row from later `each*`/aggregate evaluation, or error is undefined. `OuterJoinTests` assert only row counts and preserved-side values, never a null-extended column. | P-1 |
| Empty-group / empty-set aggregates | `count` over no rows is presumably 0, but `sum`/`min`/`max`/`average` over an empty group has no defined result (error vs null vs zero). | U-3 |
| `eachDivide` by zero | Division by zero in per-row arithmetic is undefined. Arithmetic tests use non-zero divisors only. | P-1 |
| Zip-length mismatch | An array literal/parameter is not guaranteed to align with the row count `N`; `eachEqual(field, [a, b])` over `N != 2` rows is schema-valid but semantically undefined. Each* tests broadcast a single scalar instead. | P-7 |
| `count` over a boolean vector | Ambiguous between "length of the vector" and "count of `true`s". | P-12 |
| String collation | Ordering/`>`/`min`/`max` on strings has no defined locale rule. String comparison tests use lowercase ASCII values whose ordinal order is collation-independent. | U-6 |
| Temporal averages / rounding | `average_time` around midnight and `average_date`/`average_datetime` rounding are undefined. | U-4 |
| Cross-type temporal comparison | There is no cast between `date`/`datetime`/`time`; comparing across them is undefined. Tests keep each temporal comparison within one type. | U-5 |
| `and`/`or` over a bare boolean vector | The schema permits a `booleanArrayReturning` directly inside a single-value `and`/`or`, but whether this means an ALL/ANY fold is undocumented. | P-2 |
| Self-joins | `joinItem` has no per-join alias, so joining an entity to itself is field-reference-ambiguous. | P-5 |

## Separately: known translator gaps (written spec-correct, skipped)

Some behaviour **is** defined by the spec/SQL semantics but not yet implemented
by the translator. Those tests are written to assert the correct result and are
disabled with `[Fact(Skip = "KnownGap: ...")]` (tagged `[Trait("Status",
"KnownGap")]`) so the build stays green while the gap is documented. Enable each
one when the translator implements the feature:

- Aggregates in `having` (`GroupBy/HavingTests`).
- Aggregate projections in `select` (`GroupBy/AggregateTests`).
- Select aliases renaming the projected column (`Select/SelectAliasTests`).
- `distinct` applied to the projected result rather than the pre-projection
  source rows (`Select/DistinctTests`).

List the gaps: `dotnet test --filter "Status=KnownGap"` (they report as skipped).
The full suite is green; no test fails.
