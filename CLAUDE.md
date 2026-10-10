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
dotnet pack --configuration Release -p:Version=<version> --output .
```

CI additionally passes `-p:AssemblyVersion` (pinned to the major) and `-p:FileVersion`; see `.github/workflows/publish-nuget.yml`.

Mutation testing (run by CI, slow locally):

```bash
dotnet tool install -g dotnet-stryker
dotnet stryker --mutation-level Complete --break-at 48
```

## Architecture

`PureQLProjection` is the single public entry point — a `sealed record` that implements `IStoredTableDataSet`, `IAsyncEnumerable<IRow>`, and `IQueryable<IRow>`. Its constructor takes an `IEnumerable<IStoredSchemaDataSet>` (the data source) and a `PureQLQuery` (or a subquery-less `Query`) from `PureQL.CSharp.Model`, which models PureQL specification `0.1.0-preview.1.0.0`. The query is validated when the projection is built and runs on every enumeration.

The interpreter works on runtime values: `null`, `long` (integer), `decimal`, `string`, `bool`, `DateOnly`, `TimeOnly`, `DateTime` (a UTC instant) and `Guid`.

- **Query shape** — `QueryProgram` (main query + named subqueries) and `QueryClauses` normalize the four query records (`MainPlainQuery`, `MainGroupedQuery`, `PlainQuery`, `GroupedQuery`); `Source`, `JoinClause`, `TypedExpression` (select column / group key) and `OrderKey` are its parts
- **`ModelNode`** — structural access to model nodes: unwraps `OneOf` unions, reads operands by property name, and walks a tree (`Nodes`) for validation and analysis
- **`Operator`** — dispatch. The model names every operator record after its operator, result type and context (`AddIntegerRow`, `SumDecimalGroup`, …), and an operator has the same operands in every context, so the longest matching type-name prefix selects the handler. Where integer and decimal results differ (`add`, `subtract`, `multiply`, `sum`, `round`) the prefix carries the result type
- **Operator families** — `Arithmetic` (incl. `concat`), `Temporal`, `Logic` (and/or/not/if/coalesce, lazy), `Comparison` (incl. `in`), `Aggregates`; `Lifted` holds the null-propagating unary/binary helpers, `Lists` the list operand of `in`, `Literals` literal values
- **`Scope`** — what an expression is evaluated against: a joined row (row context), a row plus all rows (projection), or a group's rows and key values (group). Evaluates fields, keys, literals and operators; parameters raise `NotSupportedException` (`UnboundParameter`)
- **`QueryRun`** — runs one query in spec order: from/joins (`JoinedRow`: one `IRecord` per source, none for an unmatched outer side) → where → groupBy/having or plain projection → distinct → orderBy → pagination. A plain query yields one row per row only when a select column reads a field outside an aggregate, otherwise exactly one row. Aggregates over all rows are computed once per query
- **`Execution`** — one run of a document; runs each subquery once, on first read. **`QueryResult`** holds a query's rows as values
- **Records** — `StoredRecord` (a stored `IRow`, cells parsed by column type on read) and `ValueRecord` (a subquery row)
- **`Values` / `ValueKey`** — the spec's equality, ordering and hashing (code-point strings, uuid by hex digits, null first); `ValueKey` keys groups and distinct rows
- **`CellText` / `ValueTypes`** — parse stored cell text and format result cells; map column types to PureQL types and back (`integer` → `LongColumnType`, `decimal` → `DoubleColumnType`)
- **`QueryValidator`** — the interpreter-side checks of the spec (entities, sources, fields and their types, outer-join nullability, group keys, unique names, subquery order, pagination range); throws `ArgumentException`
- **`Catalog`** — finds `schema.table` across all datasets; **`ResultTable`** / **`ResultRows`** — the output schema and rows

The library is **not AOT-compatible** (`IsAotCompatible = false`): operands are read by reflection and results are exposed through `IQueryable` composition.

**Package validation:** `EnablePackageValidation = true` with `PackageValidationBaselineVersion` set in the csproj. Breaking API changes fail `dotnet pack`.

**Multi-targeting:** net8.0, net9.0, net10.0.

**Publishing:** triggered by pushing a semver tag matching `*.*.*`. The tag value becomes the `PackageVersion`.

**CI thresholds:** code coverage warning at 99%, failure below 94%; mutation score failure below 48%.

**Tests:** `Samples/SampleCatalogueTests` runs every query of `PureQL.CSharp.Model.Samples` over the `Pure.RelationalSchema.Storage.Samples` fixtures and compares it with the sample's expected `Result`. A sample whose expected result contradicts the specification is excluded there and asserted against the spec by its own test. The other tests build their own small tables with `Data/InlineTable`.

**Known execution gaps:** parameter binding (no public binding API) — evaluating a parameter raises `NotSupportedException`. Times and datetimes have 100 ns precision.

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
