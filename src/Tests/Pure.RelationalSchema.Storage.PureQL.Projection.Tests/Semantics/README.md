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

## Separately: remaining translator gaps (fail fast, no tests to enable)

Some behaviour is defined by the spec but still unimplemented; the translator
raises `NotSupportedException` instead of guessing. These have no spec-correct
tests waiting to be enabled (their exact semantics either need a public API or
a spec decision first):

- Parameter binding (`Parameters/ParameterTests` pins the fail-fast contract;
  the public API exposes no way to supply values).
- Computed/scalar `select` columns and single-value `Arithmetic`.
- Temporal `average` aggregates (rounding undefined, see U-4 above).
- Aggregates inside `where`.
