# Changelog

All notable changes to this project will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/).

## [Unreleased]

### Changed

- Tests run the samples over PostgreSQL with
  `Pure.RelationalSchema.Storage.PostgreSQL.Querying` 0.1.0-preview.0.12.0
  and its translator, `Linq.Expressions.Translation.PostgreSql`
  0.1.0-preview.0.10.0 (was 0.1.0-preview.0.10.0 with a direct reference to
  the translator 0.1.0-preview.0.9.0, now dropped). The provider refuses 187
  of the 386 samples (was 214): the 27 it now translates (`string.Join` of
  `concatAll`, an ordered `FirstOrDefault` of `first` / `last`, dates from
  parts, `greatest` / `least`, inline rows, …) return their expected result
  in one SQL statement. `BlankValuesOfEveryTypeAsNullQuery` is no longer
  skipped over PostgreSQL: the provider reads a stored time with its seconds
  (kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Querying#101). A sample
  with parameters, which has no expected result, is compared over PostgreSQL
  with its result over the same fixtures in memory.

## [0.1.0-preview.7.0.1] — 2026-11-25

### Fixed

- A lifted operator over an operand that is itself a lifted operation or an
  `if` held the operand twice, once checked for null and once read, so the
  tree doubled at every level of nesting: thirty nested `round(add(…))` did
  not finish building. The check and the value are now built from the
  operand's parts, and a nested expression costs its length.
- `previous` / `next` of a `boolean` whose operand and `default` are the
  literals `true` and `false` failed to build (`ArgumentException`): the
  conditional was simplified to its test, of type `bool` where the column is
  `bool?`.

### Changed

- Tests run every sample of `PureQL.CSharp.Model.Samples`
  0.1.0-preview.4.1.0, 386 queries (was 151 of 0.1.0-preview.4.0.0). The
  provider refuses 214 of them (was 53) and 14 read no stored table (was 1);
  `BlankValuesOfEveryTypeAsNullQuery` over the PostgreSQL fixtures is skipped
  until the provider reads a stored time with its seconds
  (kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Querying#101).

## [0.1.0-preview.7.0.0] — 2026-11-25

Implements PureQL specification `0.1.0-preview.3.0.0` on
`PureQL.CSharp.Model` `0.1.0-preview.15.0.0` (was specification
`0.1.0-preview.2.0.1` on model `0.1.0-preview.13.0.0`; the model releases
`0.1.0-preview.14.0.0` and `0.1.0-preview.14.1.0` are skipped).

### Added

- **Queries.** Combined queries (`unionAll`, `union`, `intersect`, `except`)
  as the main query, a subquery or an input; inline rows; recursive
  subqueries with a plain or a grouped `next` and `maxDepth`, a row beyond
  which fails the query (`InvalidOperationException`); `orderBy` by a `select`
  column; `pagination` with only `skip` or only `take`.
- **Window functions** in a plain query: every aggregate over a partition, a
  frame of rows or a range of values (`integer`, `decimal`, `date`,
  `datetime` keys), `rowNumber`, `rank`, `denseRank`, `percentRank`,
  `cumulativeDistribution`, `ntile`, `previous` and `next`.
- **Operators.** `abs`, `sign`, `truncate`, `greatest` / `least`, `round`
  with `halfEven`, decimal `integerDivide` and `modulo`, `nullIfEqual`;
  `power`, `sqrt`, `exp`, `ln`, `log10`, `log`, `sin`, `cos`, `tan`, `asin`,
  `acos`, `atan`, `atan2`, `pi`; the string operators, `contains` /
  `startsWith` / `endsWith`, `foldCase`, `normalize`, `removeDiacritics`,
  `matchesRegex` / `extractRegex` / `replaceRegex`; the date and time parts,
  `startOf…`, months (`dateAddMonths`, `dateDiffMonths`, `datetimeAddMonths`,
  `datetimeDiffMonths`), `dateFromParts` / `timeFromParts` /
  `datetimeFromParts`, an optional `zone` wherever the specification has
  one; `as…`, `tryAs…`, `asNullable`.
- **Aggregates.** `countDistinct`, `first` / `last`, `concatAll`, `median`,
  `percentileContinuous` / `percentileDiscrete`, `mode`, the variances,
  standard deviations and covariances, `correlation`, `regressionSlope` /
  `regressionIntercept` / `regressionRSquared`.
- **Public functions and query operators**, which the tree calls where the
  BCL has no member that computes exactly what the specification defines,
  and which a provider recognises by their method: `PureQLMath`,
  `PureQLText`, `PureQLTime`, `PureQLConversions`, `PureQLAggregates`,
  `PureQLWindows` with `PureQLWindow<TRow>`, `PureQLFrame` and
  `PureQLRangeUnit`, and `PureQLRecursion`. The README lists the tree shape
  of every new construct. Called as code, the `IQueryable` forms of
  `PureQLWindows.Over` and `PureQLRecursion.Recursive` compute the operator
  over a LINQ to Objects query and refuse another provider's query with
  `NotSupportedException`, so a provider that evaluates closed calls while
  reading a tree (`Pure.RelationalSchema.Storage.PostgreSQL.Querying`)
  falls back to memory instead of looping.
- **Checks when a projection is built** (`ArgumentException`): the inputs of
  a set operation and the rows of an inline subquery have the same aliases
  and types that meet; the rules of a recursive subquery; an `orderBy`
  column is a `select` alias; a parameter has one type in the document; a
  regular expression, literal or bound, is a pattern of the dialect and has
  the group asked for; a zone, literal or bound, is a time zone name; a bound
  `fraction`, frame count, range distance, `offset`, `buckets` and `maxDepth`
  are in range; a `decimal` parameter has at most 28 digits.

### Changed

- **Breaking:** targets .NET 11 only (`net8.0`, `net9.0` and `net10.0` are
  dropped): `PureQL.CSharp.Model` `0.1.0-preview.15.0.0` is built with C# 15
  unions. Every query is built with the records of that model: the `OneOf`
  wrappers are gone, a paginated query requires `orderBy`, select items and
  group keys are one record each (see the model's changelog).
  `PureQLFilteredSchemaDataSet` takes the model's `BooleanRow` union.
- **Breaking:** results follow the specification's 3.0.0 semantics where
  they differ from what .NET computes. Run in memory, a `decimal` result is
  computed exactly and rounded once, half away from zero, to at most 28
  significant digits and 28 decimal places (`10 / 3` is
  `3.333333333333333333333333333`, where `System.Decimal` gave 29 digits),
  and a magnitude of 10²⁸ or more is an `OverflowException`; `sum` and
  `average` are computed from the exact values in any order, so a partial
  sum beyond 64 bits no longer fails; `add`, `subtract` and `multiply` over a
  list of integers and decimals keep their integer steps, checked; `round`
  to a negative number of digits rounds the exact quotient once.
- **Breaking:** `greaterThanOrEqual` and `lessThanOrEqual` of two nulls are
  `true`, as the specification now defines; in the tree such a comparison of
  two nullable operands is `a >= b || (a == null && b == null)`.
- `dateAddDays`, `timeAddSeconds` and `datetimeAddSeconds` read `operand` and
  `days` / `seconds`; `round` with `digits` keeps the type of its operand;
  `floor`, `ceiling` and `round` without `digits` take a `decimal`.
- The tree of every construct of specification `0.1.0-preview.2.0.1` is
  otherwise unchanged, so a provider that translated it still does. A query
  that holds a function or a query operator of this library is refused by
  `Pure.RelationalSchema.Storage.PostgreSQL.Querying` `0.1.0-preview.0.10.0`
  and runs in memory, until the provider translates it.

### Removed

- Target frameworks `net8.0`, `net9.0` and `net10.0`.

## [0.1.0-preview.6.2.0] — 2026-11-11

### Added

- Target framework `net11.0`; `net8.0`, `net9.0` and `net10.0` are kept.

### Changed

- Updated the Pure.* dependencies (`Pure.Primitives`, `Pure.RelationalSchema`,
  `Pure.RelationalSchema.HashCodes`, `Pure.RelationalSchema.Storage`,
  `Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions`,
  `Pure.Collections.Generic`) to their .NET 11 releases.
- Simplified collection initialization to satisfy the .NET 11 analyzers.

## [0.1.0-preview.6.1.0] — 2026-11-05

### Added

- `PureQLFilteredSchemaDataSet`: an `IStoredSchemaDataSet` over another one
  whose tables hold only the rows a PureQL condition (`BooleanRow`) is true
  for, so a host can restrict what a query reads, per table, to the rows a
  user may read (Elegant-soft/Concentrator#687). Conditions are given per
  table, matched by `TableHash`; a table without one is the origin's table.
  A `PureQLProjection` over it roots every scan of a filtered table, in the
  main query and in subqueries, on `Constant(table).Where(row => condition)`,
  below the joins and aggregates: a row the condition drops does not exist
  for the query (an outer join still gives the preserved side, with nulls),
  and the stored tables' provider still gets the whole query, so the
  condition is part of the one SQL statement, at the table's scan. A row is
  kept only where the condition is true, as with `where`.
- A condition's fields are columns of its table, declared with their type
  and nullability as a query's fields are; a field's source is not read.
  A field that names no column, a field declared with another type, a read
  of a parameter, a group key or a subquery column, or a condition given for
  a table the dataset does not have raises `ArgumentException` when the
  table (or, for an unknown table, the dataset) is read, before any row; a
  table is never read unfiltered.

## [0.1.0-preview.6.0.0] — 2026-11-02

### Changed

- **Breaking:** `datetime` result cells are written in UTC with the `Z`
  designator, the specification's one spelling of a `datetime`:
  `2025-01-01T15:54:04Z`, with fractional seconds only when present
  (`2025-01-01T15:54:04.5Z`). They were written without an offset, so
  readers took them for local time and a cell was not a valid `datetime`
  literal; a `datetime` literal now comes back exactly as it was sent, and a
  result cell can be bound as a parameter value unchanged (#267). Readers
  that append `Z` themselves or compare the old text must change. The public
  API is unchanged.

## [0.1.0-preview.5.1.0] — 2026-10-31

### Added

- Date math parameters: the value of a `date`, `time` or `datetime`
  parameter may be date math such as `now-10d` or `now-1M-5d`, which the
  current and offset parameters of
  `Pure.RelationalSchema.Storage.PureQL.Projection.Parameters` write. It has
  its value at the instant each run starts: every enumeration reads the clock
  once and binds the date math at that UTC instant, truncated to the
  microsecond, before the query goes to the provider or runs in memory.
  Years and months move it by calendar months together, the day clamped,
  then days, hours, minutes and seconds; a time wraps around midnight.
  Malformed date math (a unit the type does not take, units out of order or
  repeated, parts that mix signs) and values out of range fail when the
  projection is built, with `ArgumentException`.
- `PureQLProjection` constructors taking a `TimeProvider`, the clock date
  math reads; the other constructors use `TimeProvider.System`.

## [0.1.0-preview.5.0.0] — 2026-10-30

### Fixed

- A stored field is read by `Pure.RelationalSchema.Storage.PostgreSQL.Querying`
  0.1.0-preview.0.9.0 as its typed column instead of its cell text parsed
  back: a join or filter on a `date`, `datetime`, `time`, `uuid`, integer or
  boolean field compares the column itself, so PostgreSQL hashes or merges
  the join, uses its statistics and indexes, and no longer formats and
  parses every pair of rows (#250). A date range join of a few thousand rows
  a side takes under a second instead of half a minute. A field declared
  non-null is the column's value, which the provider and the translator
  (`Linq.Expressions.Translation.PostgreSql` 0.1.0-preview.0.7.0) take as
  never null, so it still compares with `=`; an empty cell there is now a
  NULL that matches nothing, where the text parse failed the query.
- An aggregate over all rows in a plain `select` is translated by the
  translator 0.1.0-preview.0.7.0 as a window aggregate (`COUNT(*) OVER ()`,
  `SUM(...) OVER ()`, ...) computed in the pass that reads the rows, instead
  of a scalar subquery that ran the whole `from`/`joins`/`where` a second
  time (#248). The tree is unchanged: the aggregate reads the one shared
  subtree of the query's rows, which the translator recognises below the
  projection.

### Changed

- **Breaking:** query parameters are bound to Pure parameters. The
  `PureQLProjection` constructors that take parameters take
  `IReadOnlyDictionary<string, IParameter>` (from
  `Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions`,
  a new dependency) instead of `IReadOnlyDictionary<string, ILiteral>`. A
  parameter's `Type` is checked against the query's: an integer binds a
  decimal, a typed null binds only a nullable parameter, and a list
  (`IListParameter`) binds item by item. A missing parameter, a type
  mismatch or a value that does not parse is an `ArgumentException` when
  the projection is built.
- The translated sample, join and plan tests run with the provider
  0.1.0-preview.0.9.0 and the translator 0.1.0-preview.0.7.0; the plan
  tests of #248 and #250 are no longer skipped.

## [0.1.0-preview.4.6.0] — 2026-10-28

### Fixed

- A join key the query declares nullable that `on` or `where` guards with
  `notEqual(key, null)` is compiled as the key or, where it is null, the
  default of its type, so a translating provider compares it with `=`
  instead of `IS NOT DISTINCT FROM`, and PostgreSQL hashes or merges the
  join instead of running a nested loop (#249). The guard drops every row
  with a null key, so the result is unchanged.
- A parameter given no literal is rejected with `ArgumentException` when
  the projection is built, together with the other parameter checks,
  instead of `NotSupportedException` when the query reads it (#251). A
  query with parameters thus needs the constructor taking the literals.
- A decimal result is written exactly, in the specification's spelling (no
  exponent, no trailing zeros in the fraction), instead of as a double
  (`divide(1, 3)` is `0.3333333333333333333333333333`, not
  `0.33333333333333337`), and a decimal result column is typed by a column
  type named `decimal` that holds it exactly, instead of `double` (#252).

### Changed

- Every sample of `PureQL.CSharp.Model.Samples` is verified to be translated
  by `Pure.RelationalSchema.Storage.PostgreSQL.Querying` 0.1.0-preview.0.8.0
  with `Linq.Expressions.Translation.PostgreSql` 0.1.0-preview.0.6.2 to one
  SQL statement over PostgreSQL and to return its expected result, without
  falling back to memory (`TranslatedSampleTests`, no longer skipped).

## [0.1.0-preview.4.5.1] — 2026-10-28

### Fixed

- A field the query declares non-null is read without a null check, so a
  translating provider joins and filters on it with `=` instead of
  `IS NOT DISTINCT FROM`, and PostgreSQL hashes or merges such a join
  instead of running a nested loop over every pair of rows (#243). An empty
  cell under a non-null declaration fails to parse, as the declaration
  rules it out.

## [0.1.0-preview.4.5.0] — 2026-10-27

### Fixed

- Enumerating a projection hands the query below its result rows to the
  provider of the stored tables it reads, as `provider.CreateQuery(query)`,
  and maps the values it returns to result rows; a translating provider
  thus runs the whole query as one SQL statement, and the tables are no
  longer read in full for every query (#237). The tree runs in memory only
  over tables held in memory (LINQ to Objects' `EnumerableQuery`) or when
  the provider refuses the query with `NotSupportedException` before its
  first value; any other exception of the provider fails the enumeration.

### Added

- `PureQLProjection.Execution` (`QueryExecution.NotRun`, `Translated` or
  `InMemory`) tells where the last enumeration ran the query, and
  `PureQLProjection.FallbackReason` holds the provider's
  `NotSupportedException` when the query fell back to memory.

### Changed

- The projection's tree always ends in the `Select` that builds the result
  rows, which is no longer simplified into the query: it only maps the
  items of the query's row tuple, and pagination ends the query below it.

## [0.1.0-preview.4.4.1] — 2026-10-25

### Changed

- Joins whose condition equates the rows so far with the joined source are
  hash joins instead of nested loops: `Join` for inner joins, `GroupJoin`
  with `DefaultIfEmpty` for left and right joins, and a left join plus the
  unmatched joined rows for full joins. Null keys still match, as `=` says.
  A chain of joins over thousands of rows no longer takes minutes.
- Running a projection in memory compiles its tree once per run, reads each
  stored table once, and computes each uncorrelated subquery once; `in`
  over a subquery is a hash-set lookup.

### Fixed

- A comparison of two string literals folded at build time uses code-point
  order, as the specification requires, instead of UTF-16 ordinal order.

## [0.1.0-preview.4.4.0] — 2026-10-23

### Changed

- Updated `Pure.RelationalSchema` dependency from `2.0.3` to `2.1.0`.

## [0.1.0-preview.4.3.0] — 2026-10-22

### Added

- `PureQLProjection` constructors taking an
  `IReadOnlyDictionary<string, ILiteral>` bind query parameters to literals
  by name. Each literal is checked against its parameter's type (an integer
  literal binds a decimal parameter; a typed null binds only a nullable
  one); a mismatch, or a bound pagination value out of range, throws
  `ArgumentException`. Reading a parameter that is not bound still throws
  `NotSupportedException`.

## [0.1.0-preview.4.2.0] — 2026-10-22

### Changed

- Updated `Pure.Primitives` dependency from `3.6.5` to `3.7.0`.

## [0.1.0-preview.4.1.0] — 2026-10-15

### Changed

- The projection compiles its query to one LINQ expression tree, exposed by
  `Expression`, for a LINQ provider to translate to SQL: standard query
  operators over one constant root per stored table, value tuples, BCL
  members, constants and captured parameters, with the result rows built
  only by the last `Select`. Enumerating the projection runs the tree in
  memory, as before, reading each stored table once per enumeration.
- The tree is optimized when it is built: constants fold, conditions
  simplify, `where` conditions move to the sources (or joins, or subqueries)
  they read, adjacent `Where`s merge, redundant `Select`, `OrderBy` and
  `distinctBy` are dropped, only the fields a query reads are read, and
  pagination comes last.
- `add`, `subtract`, `multiply`, `divide`, `integerDivide` and `modulo`
  evaluate every operand, so an operand that fails (an overflow, a division
  by zero) fails the query even when another operand is `null`, as in SQL.
- Without `orderBy`, a `right` join returns its rows in the order of the
  joined source.

## [0.1.0-preview.4.0.0] — 2026-10-14

### Changed

- **Breaking:** updated for `PureQL.CSharp.Model` `0.1.0-preview.13.0.0`,
  which models PureQL specification `0.1.0-preview.2.0.0`. The interpreter
  follows `0.1.0-preview.2.0.1`, which adds microsecond precision.
- **Breaking:** `distinctBy` replaces `distinct`. After `orderBy` it keeps,
  of the rows equal on the listed columns, the first in order, and
  pagination comes after it. `distinctBy` may list only select aliases.
- **Breaking:** group keys have aliases and are referenced by them; key
  aliases must be unique.
- A plain query always returns one row per row left by `where`.
- Seconds in time and datetime arithmetic, and time and datetime averages,
  round half away from zero to the microsecond.

### Added

- Total queries (`MainTotalQuery` / `TotalQuery`): `aggregate` columns and
  exactly one row, even over no rows, also as a one-row subquery source.

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
