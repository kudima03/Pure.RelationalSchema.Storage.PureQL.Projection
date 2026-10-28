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

`PureQLProjection` is the single public entry point — a `sealed record` that implements `IStoredTableDataSet`, `IAsyncEnumerable<IRow>`, and `IQueryable<IRow>`; `QueryExecution` is the only other public type. Its constructor takes an `IEnumerable<IStoredSchemaDataSet>` (the data source) and a `PureQLQuery` (or a subquery-less `Query`) from `PureQL.CSharp.Model`, which models PureQL specification `0.1.0-preview.2.0.0` (the interpreter follows `0.1.0-preview.2.0.1`, which adds microsecond precision). The query is validated and **compiled to one LINQ expression tree** when the projection is built; `Expression` is that tree. **The library is the medium between PureQL and a translating LINQ provider**: enumerating the projection hands the query below the result rows to the provider of the stored tables it reads (`Pure.RelationalSchema.Storage.PostgreSQL.Querying` translates it to one SQL statement), maps its values to result rows, and runs the tree in memory only when the tables are held in memory (`EnumerableQuery` provider) or the provider refuses the query with `NotSupportedException` before its first value; `Execution` / `FallbackReason` say which happened (issue #237).

The tree may hold only what a SQL provider translates: `Queryable` operators (`Enumerable` ones only over a group or a list), BCL value-type/string/`Math` members, value tuples, constants and captured parameters — no compiled delegates, invocations or client-side `AsEnumerable`/`ToList`. The result rows (`ResultRow`) are built only by the last `Select`, which is never simplified into the query and only maps a tuple's items. `Tree/TranslatableTreeTests` enforces this over the whole sample catalogue.

Values in the tree: integer `long?`, decimal `decimal?`, string `string`, boolean `bool?`, date `DateOnly?`, time `TimeOnly?`, datetime `DateTime?` (a UTC instant), uuid `Guid?`. Semantics are C#'s nullable semantics, which match the spec.

- **Query shape** — `QueryProgram` (main query + named subqueries) and `QueryClauses` normalize the six query records (`MainPlainQuery`, `MainGroupedQuery`, `MainTotalQuery`, `PlainQuery`, `GroupedQuery`, `TotalQuery`; a total query's `aggregate` columns become `Select`); `Source`, `JoinClause`, `TypedExpression` (select / aggregate column or group key, each with its alias) and `OrderKey` are its parts
- **`ModelNode`** — structural access to model nodes: unwraps `OneOf` unions, reads operands by property name, and walks a tree (`Nodes`) for validation and analysis
- **`DocumentTree`** — the tree of a document: the main query plus the `ResultRow` materializing `Select`; builds each subquery where it is read, per `QueryUse` (rows / aggregated / membership), and inlines it
- **`QueryTree`** — the tree of one query, in spec order: per-source scans (`SourceScan`: `Constant(dataset).Select(row => (fields))`, only the fields read; `StoredColumn` is the cell read: `IsNullOrEmpty(text) ? null : Parse(text)` for a field the query declares nullable, the bare `Parse(text)` for one it declares non-null in a reference where its source has a row of its own and nullable in none (`Required`), so a provider sees a non-null key and SQL joins with `=`, a hash/merge join, instead of `IS NOT DISTINCT FROM`, a nested loop — issue #243; an empty cell under a non-null declaration fails the parse) → where conditions split and pushed to sources/joins unless an outer join makes the source optional → joins → rest of where → plain / grouped (`GroupBy`, having) / total (one-row constant source) projection with orderBy → distinctBy → pagination. A join is a hash join when an and-ed condition of `on` (or a where condition moved into an inner join) equates an expression over the rows so far with one over the joined source (`KeySides`): `Join` on the tuple of keys (+ `Where` for the other conditions) for inner, `GroupJoin` + `SelectMany(DefaultIfEmpty)` for left/right (the other conditions filter the matches), full = left join `Concat` the joined rows whose group is empty; a tuple key is never null, so null keys match as `=` says. Without a key it is the correlated `SelectMany` nested loop (`NestedLoopJoin`). Drops repeated/row-independent order keys, distinctBy over all group keys, and ordering of subqueries read only by `in` or by aggregates. Rows are value tuples (`Tuples`); the query's rows (`AllRows`) are one shared subtree that aggregates over all rows read
- **`Scope`** — what an expression compiles against: a row parameter (row context), a row plus all rows (projection), all rows (total), or a group parameter (group). Compiles fields, keys, literals, parameters (`ParameterValues.Value`: a captured `QueryParameter<T>.Value`, the bound value, or `NotSupportedException` when read unbound) and operators
- **`Operator`** — dispatch. The model names every operator record after its operator, result type and context (`AddIntegerRow`, `SumDecimalGroup`, …), and an operator has the same operands in every context, so the longest matching type-name prefix selects the compiler. Where integer and decimal results differ (`add`, `subtract`, `multiply`, `sum`, `round`) the prefix carries the result type
- **Operator families** — `Arithmetic` (incl. `concat`), `Temporal`, `Logic`, `Comparison` (incl. `in`; strings via `string.CompareOrdinal`), `Aggregates` (`StringComparer.Ordinal` for string min/max) each return an `Expression`; `Lifted` builds null-propagating operations, `Typed` the typing/coercion helpers, `Lists` the list operand of `in`, `Literals` literal constants, `Linq` the `Queryable`/`Enumerable` calls
- **`Simplifier`** — build-time optimizer (bottom up): constant folding (a failing fold is kept; two constant strings fold through `CodePoints`, not UTF-16 ordinal), boolean simplification, null/unlifting rules, tuple member-of-new, `Where` merge, `Where`-below-`Select` (uncorrelated only), `Select` fusion, identity `Select` removal, `Select` below `Skip`/`Take`. A stored table's scan `Select` is never fused, so every source keeps one shape
- **`Execution`** — runs a projection's tree: splits it into the query and the `ResultRow` mapping, picks the provider (the first stored table's that is not an `EnumerableQuery`), `provider.CreateQuery(query)` enumerated with the first value read eagerly, each value mapped by the compiled mapping; `NotSupportedException` there falls back to `LocalProvider` and is kept as `FallbackReason`; any other exception propagates. **`ProjectionProvider` / `ProjectionQuery`** — the projection's `IQueryProvider`: its own tree runs through `Execution`; LINQ composed over it runs in `LocalProvider` with the tree replaced by the projection's rows
- **`LocalProvider`** — runs a tree in memory (LINQ to Objects) on a rewritten copy, compiled once per run: each stored table read once (a `Memo` constant typed `IEnumerable<IRow>`), uncorrelated queries nested in lambdas computed once into a `Memo` that is an `ICollection<T>` (so `Enumerable.Contains`, i.e. `in` over a subquery, is a hash-set lookup), every `Queryable` call whose source became a plain sequence turned into its `Enumerable` counterpart (`Linq.OverEnumerable`; a `Queryable` call over an `EnumerableQuery` constant would be re-executed, rewritten and compiled, for every row that reads it), `CompareOrdinal`/`StringComparer.Ordinal` replaced by `CodePoints` (the spec's code-point order; UTF-16 ordinal differs only for surrogates)
- **`CellText` / `ValueTypes`** — format result cells; map column types to PureQL types and CLR types, and back (`integer` → `LongColumnType`, `decimal` → `DoubleColumnType`)
- **`QueryValidator`** — the interpreter-side checks of the spec (entities, sources, fields and their types, outer-join nullability, group key references and aliases, unique names, `distinctBy` aliases, subquery order, pagination range, bound parameter types); throws `ArgumentException`
- **`Catalog`** — finds `schema.table` across all datasets; **`ResultTable`** / **`ResultRow`** — the output schema and rows

The library is **not AOT-compatible** (`IsAotCompatible = false`): operands are read by reflection and the query is an expression tree.

**Package validation:** `EnablePackageValidation = true` with `PackageValidationBaselineVersion` set in the csproj. Breaking API changes fail `dotnet pack`.

**Multi-targeting:** net8.0, net9.0, net10.0.

**Publishing:** triggered by pushing a semver tag matching `*.*.*`. The tag value becomes the `PackageVersion`.

**CI thresholds:** code coverage warning at 99%, failure below 94%. The mutation step is commented out in `build-and-test.yml`; its threshold (48%) is unused.

**Tests:** `Api/ExecutionTests` and the provider theories of `Samples/SampleCatalogueTests` are the guard against running in memory: `Data/ProvidedTable` / `Data/RecordingProvider` fake a provider-backed table (the provider records the queries it is given and runs them with LINQ to Objects, or refuses them at a chosen stage), and the tests assert the query is handed over whole, the tables are never enumerated, and the fallback happens only on `NotSupportedException`. `Semantics/JoinTests` checks every join kind against a nested-loop oracle (null keys, repeated keys, residual conditions, integer/decimal keys, no-key fallback); `Semantics/ScaleTests` bounds the time of a join chain and an `in` over thousands of rows (a quadratic join fails them). `Samples/SampleCatalogueTests` runs every query of `PureQL.CSharp.Model.Samples` over the `Pure.RelationalSchema.Storage.Samples` fixtures and compares it with the sample's expected `Result`. `Tree/ExpressionTreeTests` asserts the tree shape with `Data/TreeText` (C#-like text; a stored cell read prints as `row[column]`, `TreeText.Full` keeps it), and `Tree/TranslatableTreeTests` checks every sample's tree with `Data/Untranslatable`. `Samples/TranslatedSampleTests` runs every sample over the fixtures stored in PostgreSQL (`Data/PostgreSqlDatabase`: Testcontainers, tables created and filled by the fixture itself, since the Creation/Insertion packages cannot store NULLs) through `Pure.RelationalSchema.Storage.PostgreSQL.Querying` and asserts `Execution == Translated`; it is skipped (and starts no container) until the provider translates every sample — remove the `Skip` to run it (needs Docker). The other tests build their own small tables with `Data/InlineTable`. Note: `dotnet format` code fixes for IDE0010/IDE0072 ("populate switch") insert `throw new NotImplementedException()` arms — avoid partial `switch`es over `ExpressionType`.

**Parameters:** bound by name to the model's literals (`ILiteral`) through the `PureQLProjection` constructors taking `IReadOnlyDictionary<string, ILiteral>`; `ParameterValues` checks each literal's type against the parameter's (an integer literal binds a decimal; a typed null only a nullable parameter) and reads its value with `Literals` (a mismatch, or a bound pagination out of range, is an `ArgumentException` from `QueryValidator`); an unbound parameter raises `NotSupportedException` when read.

**Known execution gaps:** stored times and datetimes are read to 100 ns; arithmetic and averages round to the microsecond, as the spec requires. A join or filter on a field declared nullable translates to `IS NOT DISTINCT FROM`, which PostgreSQL runs as a nested loop; `Samples/TranslatedJoinTests` (Docker; the fixture logs every plan with `auto_explain` and disables nested loops for the session) checks a join on non-null keys is a hash or merge join.

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
