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

Mutation testing (currently disabled in CI; slow locally):

```bash
dotnet tool install -g dotnet-stryker
dotnet stryker --mutation-level Complete --break-at 48
```

## Architecture

`PureQLProjection` is the single public entry point — a `sealed record` that implements `IStoredTableDataSet`, `IAsyncEnumerable<IRow>`, and `IQueryable<IRow>`. Its constructor takes an `IEnumerable<IStoredSchemaDataSet>` (the data source) and a `PureQLQuery` (or a subquery-less `Query`) from `PureQL.CSharp.Model`, which models PureQL specification `0.1.0-preview.2.0.0` (the interpreter follows `0.1.0-preview.2.0.1`, which adds microsecond precision). The query is validated and **compiled to one LINQ expression tree** when the projection is built; `Expression` is that tree, for a LINQ provider in another module to translate to PostgreSQL SQL (possibly across several databases). Enumerating the projection runs the tree in memory.

The tree may hold only what a SQL provider translates: `Queryable` operators (`Enumerable` ones only over a group or a list), BCL value-type/string/`Math` members, value tuples, constants and captured parameters — no compiled delegates, invocations or client-side `AsEnumerable`/`ToList`. The result rows (`ResultRow`) are built only by the last `Select`. `Tree/TranslatableTreeTests` enforces this over the whole sample catalogue.

Values in the tree: integer `long?`, decimal `decimal?`, string `string`, boolean `bool?`, date `DateOnly?`, time `TimeOnly?`, datetime `DateTime?` (a UTC instant), uuid `Guid?`. Semantics are C#'s nullable semantics, which match the spec.

- **Query shape** — `QueryProgram` (main query + named subqueries) and `QueryClauses` normalize the six query records (`MainPlainQuery`, `MainGroupedQuery`, `MainTotalQuery`, `PlainQuery`, `GroupedQuery`, `TotalQuery`; a total query's `aggregate` columns become `Select`); `Source`, `JoinClause`, `TypedExpression` (select / aggregate column or group key, each with its alias) and `OrderKey` are its parts
- **`ModelNode`** — structural access to model nodes: unwraps `OneOf` unions, reads operands by property name, and walks a tree (`Nodes`) for validation and analysis
- **`DocumentTree`** — the tree of a document: the main query plus the `ResultRow` materializing `Select`; builds each subquery where it is read, per `QueryUse` (rows / aggregated / membership), and inlines it
- **`QueryTree`** — the tree of one query, in spec order: per-source scans (`SourceScan`: `Constant(dataset).Select(row => (fields))`, only the fields read; `StoredColumn` is the cell read) → where conditions split and pushed to sources/joins unless an outer join makes the source optional → joins (correlated `SelectMany`, `DefaultIfEmpty`, full = left join `Concat` anti-join) → rest of where → plain / grouped (`GroupBy`, having) / total (one-row constant source) projection with orderBy → distinctBy → pagination. Drops repeated/row-independent order keys, distinctBy over all group keys, and ordering of subqueries read only by `in` or by aggregates. Rows are value tuples (`Tuples`); the query's rows (`AllRows`) are one shared subtree that aggregates over all rows read
- **`Scope`** — what an expression compiles against: a row parameter (row context), a row plus all rows (projection), all rows (total), or a group parameter (group). Compiles fields, keys, literals, parameters (`UnboundParameter.Value`: a captured `QueryParameter<T>.Value`, which throws `NotSupportedException` when read) and operators
- **`Operator`** — dispatch. The model names every operator record after its operator, result type and context (`AddIntegerRow`, `SumDecimalGroup`, …), and an operator has the same operands in every context, so the longest matching type-name prefix selects the compiler. Where integer and decimal results differ (`add`, `subtract`, `multiply`, `sum`, `round`) the prefix carries the result type
- **Operator families** — `Arithmetic` (incl. `concat`), `Temporal`, `Logic`, `Comparison` (incl. `in`; strings via `string.CompareOrdinal`), `Aggregates` (`StringComparer.Ordinal` for string min/max) each return an `Expression`; `Lifted` builds null-propagating operations, `Typed` the typing/coercion helpers, `Lists` the list operand of `in`, `Literals` literal constants, `Linq` the `Queryable`/`Enumerable` calls
- **`Simplifier`** — build-time optimizer (bottom up): constant folding (a failing fold is kept), boolean simplification, null/unlifting rules, tuple member-of-new, `Where` merge, `Where`-below-`Select` (uncorrelated only), `Select` fusion, identity `Select` removal, `Select` below `Skip`/`Take`. A stored table's scan `Select` is never fused, so every source keeps one shape
- **`LocalProvider` / `LocalQuery`** — run the tree in memory (LINQ to Objects) on a rewritten copy: each stored table read once per run, uncorrelated queries nested in lambdas computed once, `CompareOrdinal`/`StringComparer.Ordinal` replaced by `CodePoints` (the spec's code-point order; UTF-16 ordinal differs only for surrogates)
- **`CellText` / `ValueTypes`** — format result cells; map column types to PureQL types and CLR types, and back (`integer` → `LongColumnType`, `decimal` → `DoubleColumnType`)
- **`QueryValidator`** — the interpreter-side checks of the spec (entities, sources, fields and their types, outer-join nullability, group key references and aliases, unique names, `distinctBy` aliases, subquery order, pagination range); throws `ArgumentException`
- **`Catalog`** — finds `schema.table` across all datasets; **`ResultTable`** / **`ResultRow`** — the output schema and rows

The library is **not AOT-compatible** (`IsAotCompatible = false`): operands are read by reflection and the query is an expression tree.

**Package validation:** `EnablePackageValidation = true` with `PackageValidationBaselineVersion` set in the csproj. Breaking API changes fail `dotnet pack`.

**Multi-targeting:** net8.0, net9.0, net10.0.

**Publishing:** triggered by pushing a semver tag matching `*.*.*`. The tag value becomes the `PackageVersion`.

**CI thresholds:** code coverage warning at 99%, failure below 94%. The mutation step is commented out in `build-and-test.yml`; its threshold (48%) is unused.

**Tests:** `Samples/SampleCatalogueTests` runs every query of `PureQL.CSharp.Model.Samples` over the `Pure.RelationalSchema.Storage.Samples` fixtures and compares it with the sample's expected `Result`. `Tree/ExpressionTreeTests` asserts the tree shape with `Data/TreeText` (C#-like text; a stored cell read prints as `row[column]`, `TreeText.Full` keeps it), and `Tree/TranslatableTreeTests` checks every sample's tree with `Data/Untranslatable`. The other tests build their own small tables with `Data/InlineTable`. Note: `dotnet format` code fixes for IDE0010/IDE0072 ("populate switch") insert `throw new NotImplementedException()` arms — avoid partial `switch`es over `ExpressionType`.

**Known execution gaps:** parameter binding (no public binding API) — reading a parameter raises `NotSupportedException` when the query runs. Stored times and datetimes are read to 100 ns; arithmetic and averages round to the microsecond, as the spec requires.

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
