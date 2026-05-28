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
- **`JoinApplicator`** — materializes joined datasets into lists and applies join conditions (supports INNER, LEFT, RIGHT, FULL)
- **`WhereExpressionBuilder`** — compiles a `BooleanReturning` *or* per-row `BooleanArrayReturning` AST node from `PureQL.CSharp.Model` into a `Func<IRow, bool>` LINQ predicate (entry point: `BuildPredicate(OneOf<BooleanReturning, BooleanArrayReturning>)`). Implements the per-row `each*` family — `EachEquality`, `EachComparison`, `EachAnd`/`EachOr`/`EachNot`, plus per-row arithmetic (`EachArithmetic`, `EachDateAddDays`/`EachDateDiffDays`, `EachTimeAddSeconds`/`EachTimeDiffSeconds`, `EachDateTimeAddSeconds`/`EachDateTimeDiffSeconds`)
- **`OrderByApplicator`** — applies `IEnumerable<OrderByItem>` ordering, honouring per-item `SortDirection` (`Asc`/`Desc`)
- **`GroupByApplicator`** — applies GROUP BY fields and an optional HAVING predicate
- **`DistinctApplicator`** — deduplicates the row set when `Query.Distinct == true`
- **`CellValueExtractor`** — extracts typed .NET values from `ICell` for the seven supported column types: bool, date, datetime, double, string, time, uuid
- **`ColumnsFromQuery`** — maps `SelectExpression` nodes to `IColumn` instances for the result schema

The library is **not AOT-compatible** (`IsAotCompatible = false`) because the query translation relies on LINQ expression trees and reflection-based `IQueryable` composition.

**Package validation:** `EnablePackageValidation = true` with `PackageValidationBaselineVersion = 0.1.0-preview.1.0.0`. Breaking API changes fail the build.

**Multi-targeting:** net8.0, net9.0, net10.0.

**Publishing:** triggered by pushing a semver tag matching `*.*.*`. The tag value becomes the `PackageVersion`.

**CI thresholds:** code coverage warning at 99%, failure below 52%; mutation score failure below 32%.

**Known execution gaps:** parameters, aggregates outside the `groupBy` projection, computed `select` columns, and single-value `Arithmetic` are not yet implemented — see [`EXECUTION_GAPS.md`](EXECUTION_GAPS.md). These constructs raise `NotSupportedException` rather than silently producing wrong results.

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
