# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Commands

All `dotnet` commands must be run from the `./src` directory.

```bash
dotnet restore
dotnet build --no-restore -warnaserror
dotnet format --verify-no-changes             # check code style (CI enforces this)
dotnet format                                  # auto-fix code style
dotnet test --no-build --verbosity normal      # run xUnit tests
dotnet pack --configuration Release -p:PackageVersion=<version> --output .
```

Mutation testing (run by CI, slow locally):

```bash
dotnet tool install -g dotnet-stryker
dotnet stryker --mutation-level Complete --break-at 32
```

## Architecture

`PureQLProjection` is the single public entry point — a `sealed record` that implements `IStoredTableDataSet`, `IAsyncEnumerable<IRow>`, and `IQueryable<IRow>`. Its constructor takes an `IEnumerable<IStoredSchemaDataSet>` (the data source) and a `Query` (the PureQL AST), and produces a fully enumerable result set.

All translation work is done by internal classes:

- **`TableFromQuery`** — derives the virtual `ITable` schema from the query's SELECT expressions
- **`RowsFromDatasets`** — orchestrates the full query pipeline: locates the source table dataset by `schema.table` path, then applies each clause in order
- **`JoinApplicator`** — materializes joined datasets into lists and applies join conditions (supports INNER, LEFT, RIGHT, FULL). Tags the joined table's columns with their entity path (`QualifiedColumn`) so same-named columns from both sides stay distinct in merged rows, and pads unmatched outer-join rows with empty cells for the missing side
- **`QualifiedColumn`** — an `IColumn` wrapper carrying the "schema.table" entity a joined column came from; deliberately a class (not a record) because the wrapped column types' `Equals`/`GetHashCode` throw by design
- **`WhereExpressionBuilder`** — compiles a `BooleanReturning` *or* per-row `BooleanArrayReturning` AST node from `PureQL.CSharp.Model` into a `Func<IRow, bool>` LINQ predicate (entry point: `BuildPredicate(OneOf<BooleanReturning, BooleanArrayReturning>)`). Implements the per-row `each*` family — `EachEquality`, `EachComparison`, `EachAnd`/`EachOr`/`EachNot`, plus per-row arithmetic (`EachArithmetic`, `EachDateAddDays`/`EachDateDiffDays`, `EachTimeAddSeconds`/`EachTimeDiffSeconds`, `EachDateTimeAddSeconds`/`EachDateTimeDiffSeconds`)
- **`OrderByApplicator`** — applies `IEnumerable<OrderByItem>` ordering, honouring per-item `SortDirection` (`Asc`/`Desc`)
- **`GroupByApplicator`** — groups rows by the GROUP BY fields (or the whole set when only aggregates are selected), filters groups with HAVING, and emits one projected row per group (aggregate select expressions fold the group; field select expressions take the group key value; scalar select expressions repeat their constant)
- **`ScalarCell`** — builds the constant output cell of a scalar select expression (`SELECT 5 AS version`), repeated on every output row in both the per-row and group projection paths; text formatted via `ValueText` so it round-trips through `CellValueExtractor`
- **`AggregateEvaluator`** — evaluates single-value expressions over a group of rows: `Count`, `NumberAggregate` (sum/min/max/avg), `StringAggregate` (min/max, ordinal), `Date`/`DateTime`/`TimeAggregate` (min/max), plus HAVING boolean composites (equality/comparison/and/or/not over constants and aggregates)
- **`DistinctApplicator`** — deduplicates the *projected* row set when `Query.Distinct == true` (SQL `SELECT DISTINCT` semantics)
- **`CellValueExtractor`** — resolves a field reference (entity + field name) against a row and extracts typed .NET values from `ICell` for the seven supported column types: bool, date, datetime, double, string, time, uuid. Resolution prefers a `QualifiedColumn` matching the reference's entity, then the base table's (untagged) same-named column, then any same-named column
- **`SelectColumns`** — the single source of truth for output columns: one per `SelectExpression`, named by the alias (falling back to the field name) and typed by the value type; used by both the schema and the row projection
- **`ValueText`** — formats computed (aggregate) values as canonical invariant cell text that `CellValueExtractor` round-trips

The pipeline order in `RowsFromDatasets.Build` is: locate table → JOIN → WHERE →
(GROUP BY + HAVING + projection → ORDER BY | ORDER BY → per-row projection) →
DISTINCT → pagination. ORDER BY runs after GROUP BY/HAVING/projection when the
query is in group mode (so it can order by an aggregate alias against the
post-aggregation row), and before projection otherwise (so it can order by a
column that isn't in the SELECT list, against the raw joined/filtered rows).

The library is **not AOT-compatible** (`IsAotCompatible = false`) because the query translation relies on LINQ expression trees and reflection-based `IQueryable` composition.

**Package validation:** `EnablePackageValidation = true` with `PackageValidationBaselineVersion = 0.1.0-preview.1.0.0`. Breaking API changes fail the build.

**Multi-targeting:** net8.0, net9.0, net10.0.

**Publishing:** triggered by pushing a semver tag matching `*.*.*`. The tag value becomes the `PackageVersion`.

**CI thresholds:** code coverage warning at 99%, failure below 52%; mutation score failure below 32%.

**Known execution gaps:** parameter binding (no public binding API), computed `select` columns, single-value `Arithmetic`, temporal `Average` aggregates (undefined rounding semantics), and aggregates inside WHERE are not implemented. These constructs raise `NotSupportedException` rather than silently producing wrong results.

## Code Style

Enforced by `.editorconfig` and `dotnet format --verify-no-changes` in CI:

- No `var` — always use explicit types
- No expression-bodied methods, constructors, or operators; expression-bodied properties and accessors are required
- Private fields: `_camelCase` (underscore prefix)
- File-scoped namespaces
- `using` directives outside the namespace
- Allman brace style — opening braces always on a new line
- Max line length: 90 characters
- Pattern matching preferred over `is`-with-cast and `as`-with-null-check

## Commit Messages

Do not mention Claude or AI assistance in commit messages.
