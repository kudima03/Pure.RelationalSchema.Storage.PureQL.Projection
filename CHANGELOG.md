# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

## [0.1.0-preview.3.0.0] — 2026-10-10

### Changed

- **Breaking:** rewritten for `PureQL.CSharp.Model` `0.1.0-preview.12.0.0`,
  which models PureQL specification `0.1.0-preview.1.0.0`. The constructor
  now takes a `PureQLQuery` (or a subquery-less `Query`); the previous
  expression model is gone.
- The interpreter follows the specification's semantics: `null` equals
  `null`, exact `decimal` and checked 64-bit `integer` arithmetic,
  code-point string ordering, datetimes as UTC instants, lazy `if`,
  `coalesce`, `and` and `or`.
- Integer result columns are `LongColumnType`; decimal result columns are
  `DoubleColumnType`.
- The NuGet package now ships `README.md`.

### Added

- Every clause and operator of the specification: aliases and self-joins,
  computed group keys, `having`, `orderBy` over any expression, aggregates
  with `predicate` and `over`, `any`/`all`, `in` over lists and subquery
  columns, `if`, `coalesce`, `concat`, rounding, integer division and all
  date/time arithmetic.
- Subqueries read through `from`, `join` and `in`.
- Query validation against the datasets' schemas when the projection is
  built (`ArgumentException`).

### Known gaps

- Parameters cannot be bound; evaluating one raises
  `NotSupportedException`.

## [0.1.0-preview.2.0.0] — 2026-05-28

### Changed

- Updated `PureQL.CSharp.Model` dependency from `0.1.0-preview.10.0.0` to
  `0.1.0-preview.11.0.0`, which tracks PureQL specification
  `0.1.0-preview.0.5.0`.
- `Query.Where` is now `OneOf<BooleanReturning, BooleanArrayReturning>?`
  — `RowsFromDatasets` dispatches on both arms via the new
  `WhereExpressionBuilder.BuildPredicate` entry point.
- `Query.OrderBy` is now `IEnumerable<OrderByItem>?` — `OrderByApplicator`
  honours the per-item `SortDirection` (`Asc`/`Desc`), using
  `OrderByDescending`/`ThenByDescending` for descending keys.
- `Join.On` is now `OneOf<BooleanReturning, BooleanArrayReturning>` —
  `JoinApplicator` routes the union through `BuildPredicate` so each\*
  predicates work as join conditions.
- `Field` union widened with the new `NullField` arm. `OrderByApplicator`
  and `GroupByApplicator` accept it (treated as a constant-null sort /
  group key).
- `NumberReturning` extended with `Arithmetic`, `NumberAggregate`,
  `Count`; `DateReturning` with `DateAggregate`; `StringReturning` with
  `StringAggregate`; `TimeReturning` with `TimeAggregate`;
  `DateTimeReturning` with `DateTimeAggregate`. The `WhereExpressionBuilder`
  match arms are exhaustive; aggregates / single-value arithmetic in
  row-by-row contexts raise `NotSupportedException` and are documented
  in `EXECUTION_GAPS.md`.

### Added

- Execution support for the per-row predicate family
  (`EachBooleanEquality`, `EachNumberEquality`, `EachStringEquality`,
  `EachDateEquality`, `EachTimeEquality`, `EachDateTimeEquality`,
  `EachUuidEquality`, `EachNumberComparison`, `EachStringComparison`,
  `EachDateComparison`, `EachTimeComparison`, `EachDateTimeComparison`,
  `EachAndOperator`, `EachOrOperator`, `EachNotOperator`) inside `Where`
  and `Join.On` predicates. Supports both broadcast (`*Returning`
  right-hand side) and zip (`*ArrayReturning` right-hand side) modes.
- Execution support for the per-row arithmetic family
  (`EachAdd`, `EachSubtract`, `EachMultiply`, `EachDivide`) and the
  per-row date / time / datetime math operators
  (`EachDateAddDays`, `EachDateDiffDays`, `EachTimeAddSeconds`,
  `EachTimeDiffSeconds`, `EachDateTimeAddSeconds`,
  `EachDateTimeDiffSeconds`) as operands of each\*-comparisons inside
  filters. Numeric operations propagate `null` cleanly; division by
  zero yields `null`.
- `Query.Distinct` is honoured by a new `DistinctApplicator` that
  deduplicates by ordinal-compared text cell values.
- New test class `PureQLProjectionEachTests` covering each\* equality,
  comparison, boolean composition, field-to-field comparison, empty
  inputs, descending order, and `Distinct`.
- `EXECUTION_GAPS.md` documenting the remaining executor gaps.
- `CHANGELOG.md` (this file).

### Removed

- The `WhereExpressionBuilder.BuildBoolArrayReturningAsSingleBool` helper
  is no longer reachable as a separate method — its responsibility has
  been folded into `BuildBoolArrayPerRow`, which now handles all eight
  arms of `BooleanArrayReturning`.
