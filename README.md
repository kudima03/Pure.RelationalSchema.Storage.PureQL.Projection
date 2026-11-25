# Pure.RelationalSchema.Storage.PureQL.Projection

Compiles PureQL queries over relational schema datasets into one LINQ expression tree: a `PureQLQuery` from `PureQL.CSharp.Model` becomes an `IStoredTableDataSet` whose `IQueryable<IRow>` tree is handed, when enumerated, to the LINQ provider of the stored tables it reads (which translates it to SQL), and runs in memory when the provider cannot run it or the tables are held in memory.

[![.NET build & test](https://github.com/kudima03/Pure.RelationalSchema.Storage.PureQL.Projection/actions/workflows/build-and-test.yml/badge.svg?branch=main)](https://github.com/kudima03/Pure.RelationalSchema.Storage.PureQL.Projection/actions/workflows/build-and-test.yml)
[![Build and Deploy](https://github.com/kudima03/Pure.RelationalSchema.Storage.PureQL.Projection/actions/workflows/publish-nuget.yml/badge.svg?branch=main)](https://github.com/kudima03/Pure.RelationalSchema.Storage.PureQL.Projection/actions/workflows/publish-nuget.yml)
[![NuGet](https://img.shields.io/nuget/v/Pure.RelationalSchema.Storage.PureQL.Projection)](https://www.nuget.org/packages/Pure.RelationalSchema.Storage.PureQL.Projection)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Overview

`Pure.RelationalSchema.Storage.PureQL.Projection` is an interpreter for the [PureQL specification](https://github.com/kudima03/PureQL-Specification) `0.1.0-preview.3.0.0` over the Pure relational storage model. Given a collection of `IStoredSchemaDataSet` objects and a `PureQLQuery`, it produces an `IStoredTableDataSet` whose rows are the query's result under the specification's semantics.

The query is compiled when the projection is built to a single `IQueryable<IRow>` expression tree made of standard query operators over one root per stored table, ending in the `Select` that builds the result rows (see [Expression tree](#expression-tree)). Enumerating the projection hands the query below the result rows to the provider of the stored tables it reads, so that a provider that translates it (to PostgreSQL) runs it as one SQL statement and the projection only maps its values to result rows (see [Running a query](#running-a-query)). The tree runs in memory, with LINQ to Objects, only over tables held in memory, or when the provider refuses the query; run in memory, every value is computed exactly as the specification's **Semantics** define it (see [How values are computed](#how-values-are-computed)).

## Public API

### `PureQLProjection`

The entry point. Implements `IStoredTableDataSet`, `IAsyncEnumerable<IRow>` and `IQueryable<IRow>`.

| Member | Description |
|--------|-------------|
| `PureQLProjection(IEnumerable<IStoredSchemaDataSet> datasets, PureQLQuery query)` | Validates the query against the datasets' schemas and builds the projection. |
| `PureQLProjection(IEnumerable<IStoredSchemaDataSet> datasets, Query query)` | The same for a query that declares no subqueries (the model's `Query`: what a subquery, a set operation's input or a recursive seed holds). |
| `PureQLProjection(..., IReadOnlyDictionary<string, IParameter> parameters)` | Either of the above with parameters bound by name to Pure parameters (see [Parameters](#parameters)). |
| `PureQLProjection(..., IReadOnlyDictionary<string, IParameter> parameters, TimeProvider clock)` | The same, with the clock that date math parameters such as `now-10d` read (see [Date math](#date-math)); the constructors without it use `TimeProvider.System`. |
| `ITable TableSchema` | An unnamed table with one column per `select` item, named by its alias and typed by its declared type. |
| `IEnumerator<IRow> GetEnumerator()` | Runs the query, through the stored tables' provider or in memory, and enumerates the result rows. Every enumeration runs the query again. |
| `IAsyncEnumerator<IRow> GetAsyncEnumerator(...)` | Async enumeration over the same rows. |
| `QueryExecution Execution` | Where the last enumeration ran the query: `NotRun`, `Translated` (the stored tables' provider ran it) or `InMemory`. |
| `NotSupportedException? FallbackReason` | The exception the provider refused the last query with, when the query ran in memory for that reason; `null` otherwise. |
| `Expression Expression` | The query's expression tree (see below). |
| `Type ElementType / IQueryProvider Provider` | `IQueryable<IRow>` plumbing: LINQ composed over the projection runs in memory over the projection's rows. |

### What is supported

Every clause, query and operator of the specification:

- **Queries.** Plain, grouped and total queries with `from` and `joins` (`inner`, `left`, `right`, `full`, aliases and self-joins), `where`, `groupBy` over any row expression with keys referenced by alias, `having`, `select`, `aggregate`, `orderBy` by an expression or by a `select` column, `distinctBy` and `pagination` (`skip`, `take` or both, numbers or parameters); combined queries (`unionAll`, `union`, `intersect`, `except` over two or more queries of any kind, nested or not), as the main query, a subquery or an input.
- **Subqueries** read through `from`, `join` and `in`: a query, inline rows (literal and parameter cells) and a recursive subquery (`seed`, a plain or grouped `next`, `maxDepth` as a number or a parameter).
- **Operators** in the row, projection, group and total contexts: boolean and comparison operators over every type, `in`, `contains` / `startsWith` / `endsWith`, the arithmetic operators, rounding (`round` with `halfEven`, `truncate`, `floor`, `ceiling`, with and without `digits`), `abs`, `sign`, `greatest` / `least`, powers, roots, logarithms, trigonometry and `pi`, the string operators, `foldCase`, `normalize`, `removeDiacritics`, the regular expression operators, date and time arithmetic in days, seconds and months, the parts, `startOf…`, `…FromParts`, time zones, the conversions (`as…`, `tryAs…`, `asNullable`), `if`, `coalesce`, `nullIfEqual`.
- **Aggregates** with `predicate` and `over`: `count`, `countDistinct`, `sum`, `average`, `min`, `max`, `any`, `all`, `first`, `last`, `concatAll`, `median`, the percentiles, `mode`, variances, standard deviations, covariances, `correlation` and the regression aggregates.
- **Window functions** in the `select` and `orderBy` of a plain query: every aggregate over a partition, a frame of rows or a range of values (`integer`, `decimal`, `date` and `datetime` keys with their distances), the ranking functions and `previous` / `next`.

A plain query returns one row per row left by `where`, a total query exactly one row (even over no rows), a grouped query one row per group and a combined query the rows of its inputs put together. `distinctBy` runs after `orderBy` and keeps, of the rows equal on the listed columns, the first in order.

The checks the specification leaves to the interpreter run when the projection is built, and a failing query raises `ArgumentException`:

- entities, subqueries, sources and fields exist; each field declares its column's type and, after outer joins, its nullability; group key references name a key and repeat its type; source names, group key aliases, column aliases and subquery names are unique; `distinctBy` and the columns of `orderBy` are column aliases; a subquery reads only subqueries declared before it;
- the inputs of a set operation and the rows of an inline subquery have the same column aliases, each once, and types that meet (`integer` and `decimal` give `decimal`; a column is nullable if it is in any input);
- the `next` of a recursive subquery reads the subquery exactly once, in `from` or a join and not on the optional side of an outer join, its `seed` does not read it, and `next` has the aliases of `seed` with types the seed's columns accept;
- a parameter is bound to a value of its type (see [Parameters](#parameters)) and has one type in the whole document; a bound `pagination`, a `fraction` (0 to 1), a frame count and a range distance (0 or more), an `offset`, `buckets` and `maxDepth` (1 or more) are in range;
- a regular expression, literal or bound, is a pattern of the dialect (its parentheses balance, its ranges and counted quantifiers are in order, it holds no construct outside the dialect, a line feed is written `\n`) and has the `group` asked for;
- a `zone`, literal or bound, is a name of the specification's list of time zones, or `UTC`.

An execution error fails the enumeration with the .NET exception of what failed, before or while rows are read:

| Error | Exception |
|---|---|
| an `integer` outside 64 bits, a `decimal` of magnitude 10²⁸ or more | `OverflowException` |
| division by zero in `divide`, `integerDivide`, `modulo` | `DivideByZeroException` |
| a value outside a function's domain (`sqrt` of a negative number, `ln` of a number that is not positive, `asin` outside −1 to 1, …); a `substring` or `position` with a `start` below 1, a negative `length` or `count`, a `splitPart` with an `index` of 0; a date or datetime outside the years 0001–9999; a part out of range in `dateFromParts` / `timeFromParts` | `ArgumentOutOfRangeException` |
| text that is not a literal of the type, read with `as…` | `FormatException` |
| a zone computed for a row that is not a time zone name | `TimeZoneNotFoundException` |
| a row of a recursive subquery deeper than `maxDepth` | `InvalidOperationException` |
| a field declared non-null whose cell is empty (in memory) | `FormatException` |

### How values are computed

Run in memory, the interpreter computes every value as the specification's **Semantics** define it, so the digits, the order and the text of a result do not depend on .NET's own arithmetic, culture or string order. Where the BCL computes exactly that, the tree calls the BCL; where it does not, the tree calls a function of this library (see [Functions and query operators](#functions-and-query-operators)).

| Values | How they are read and computed |
|---|---|
| `integer` | a checked 64-bit `long`: a result outside the range is an `OverflowException`. `add`, `subtract` and `multiply` over a list go from left to right, an integer step is an integer step even next to a later decimal operand; `sum` is computed from the exact values in any order, and only the sum is checked. `modulo` of the least integer by −1 is 0. |
| `decimal` | `System.Decimal` holding only values m × 10⁻ˢ with \|m\| < 10²⁸ and 0 ≤ s ≤ 28. A sum, difference, product and quotient are computed exactly, with big integers, and rounded once, half away from zero, to the most decimal places that fit in 28 digits; a result of magnitude 10²⁸ or more is an `OverflowException`. .NET's own decimal operators (29 digits where 96 bits hold them, half to even, up to 7.9 × 10²⁸) are never used for a result. `divide(2, 3, 2)` is `0.3333333333333333333333333334`: each step of a list is rounded. |
| rounding | `round`, `truncate`, `floor` and `ceiling` are exact; `round` to a negative number of digits divides exactly and rounds once (`round(149.9999999999999999999999999, -2)` is `100`); `halfEven` is `MidpointRounding.ToEven`. With `digits`, an `integer` stays an `integer` and a result beyond 64 bits is an `OverflowException`. |
| `integerDivide`, `modulo` | of decimals: the exact quotient truncated toward zero, never rounded first, and the exact remainder with the sign of `left`. |
| `power`, `sqrt`, `exp`, `ln`, `log10`, `log`, trigonometry | computed in integer arithmetic of more digits than the result, until the 28 digits are certain, and rounded once: the exact mathematical value, correctly rounded. Results a decimal holds are returned as they are (`power(1.5, 2)` is `2.25`, `log(8, 2)` is `3`). No `double` is used anywhere, also not while a tree is built. `pi` is the constant `3.141592653589793238462643383`. |
| `sum`, `average`, statistics | from the exact values of the rows (sums of values, squares and products as big integers), one division and, for a standard deviation or a correlation, one square root of the exact ratio; rounded once. A partial sum is never an error, and a correlation never leaves −1 to 1. |
| strings | UTF-16 text read by code point: `length`, `substring`, `position`, `padStart` / `padEnd`, `reverse` and `splitPart` count code points; equality and `contains` / `startsWith` / `endsWith` compare ordinally; order, `min` / `max`, `greatest` / `least`, `mode` and `percentileDiscrete` order by code point, not by UTF-16 code unit. |
| `upper`, `lower`, `foldCase` | the full mappings of the Unicode Character Database 16.0.0 (`UnicodeData.txt`, the unconditional mappings of `SpecialCasing.txt` with the final sigma rule, `CaseFolding.txt` statuses C and F), from tables generated into this library: `upper("straße")` is `STRASSE`. .NET's own case conversion (simple mappings only) and the culture are not used. |
| `normalize`, `removeDiacritics` | `string.Normalize`, the runtime's normalization data (ICU on Linux and macOS); `removeDiacritics` is NFD, every code point of general category `Mn` (the runtime's `CharUnicodeInfo`, Unicode 16.0.0 on .NET 11) dropped, then NFC. |
| regular expressions | a pattern is parsed as the specification's dialect and rejected outside it, then written as a `System.Text.RegularExpressions.Regex` (backtracking engine, culture-invariant, case-sensitive) that matches by code point: the dot, a class and a negated class take one whole code point, `\d`, `\w`, `\s` and `\b` are the ASCII sets, `^` and `$` the two ends of the string. `replaceRegex` puts the replacement in as literal text and moves on by one code point after an empty match. |
| `date`, `time`, `datetime` | `DateOnly`, `TimeOnly`, and a `DateTime` that is a UTC instant. Seconds are rounded half away from zero to the microsecond. Months are added with the day clamped to the end of a shorter month, and `dateDiffMonths` / `datetimeDiffMonths` are the inverse. ISO 8601 weeks and week years. |
| time zones | a zone is a name of the specification's list (444 names of release 2026e of the IANA database, compiled into this library) or `UTC`; any other text is refused. The offset of a zone at an instant comes from the tz database of the machine. From a local date and time to an instant there is one rule, computed from the zone's offsets and not left to .NET's handling of ambiguous and invalid times: the first instant at which the zone's clocks read that date and time or later. `startOf…` with a zone is that rule applied to the start of the local period. |
| conversions | `asString` writes the one spelling of a literal of the type; `as…` of a string and `tryAs…` read exactly that spelling with parsers of this library (no `+`, leading zeros, exponent, other letter case, offset other than `Z`), not .NET's `Parse`. |
| windows | per partition, the rows are sorted by the window's `orderBy`; a frame of rows is a slice of them, a range the rows whose key lies within the distances of the current row's key (found by binary search; a bound outside the type reaches the end of the partition; rows whose key is `null` are only each other's window; a distance in months over a `datetime` key counts months in UTC, as the specification says). `percentRank` and `cumulativeDistribution` divide exactly. |
| recursive subqueries | level by level, every row of every level kept; the level after `maxDepth` being non-empty fails the query before any row is returned. |
| set operations | rows compare column by column as `equal` compares values: `null` equals `null`, `1` equals `1.0`, strings by code points. |

Reading stored cells and binding parameters is described under [Storage mapping](#storage-mapping) and [Parameters](#parameters).

### Functions and query operators

The public static classes below hold what an operator of the specification is in the tree when the BCL has no member that computes exactly what the specification defines. They are part of the public API so that a provider can recognise a call by its method and translate it; each takes non-null arguments (the tree checks operands for `null` around the call) and is a pure function.

| Class | Members |
|---|---|
| `PureQLMath` | `Add`, `Subtract`, `Multiply`, `Divide` (what a decimal `+ - * /` of the tree is computed with in memory), `IntegerDivide`, `Modulo`, `Round`, `Truncate`, `RoundQuotient`, `PowerOfTen`, `Power`, `Sqrt`, `Exp`, `Ln`, `Log10`, `Log`, `Sin`, `Cos`, `Tan`, `Asin`, `Acos`, `Atan`, `Atan2`, `Pi` |
| `PureQLText` | `Length`, `Upper`, `Lower`, `FoldCase`, `RemoveDiacritics`, `Trim`, `TrimStart`, `TrimEnd`, `Substring`, `Replace`, `Position`, `SplitPart`, `PadStart`, `PadEnd`, `Repeat`, `Reverse`, `ConcatWith`, `MatchesRegex`, `ExtractRegex`, `ReplaceRegex`, `RegexGroups` (the number of groups of a pattern of the dialect; `FormatException` for text that is not one) |
| `PureQLTime` | `Quarter`, `DayOfWeek`, `Week`, `WeekYear`, `Microsecond`, `StartOfYear` … `StartOfSecond` (of a `DateOnly`, a `TimeOnly`, a `DateTime` in UTC, and a `DateTime` in a zone), `LocalDate`, `LocalTime`, `DatetimeFromParts` (in a zone), `TimeFromParts`, `DiffMonths`, `AddMonths` (in a zone), `IsZone` |
| `PureQLConversions` | `AsString` of every type, `AsInteger`, `AsDecimal`, `AsBoolean`, `AsDate`, `AsTime`, `AsDatetime`, `AsUuid` of a string, and `TryAs…` of a nullable string |
| `PureQLAggregates` | over the values an aggregate sees, nulls skipped: `Sum`, `Average` (what `Queryable.Sum` / `Average` of the tree are computed with in memory), `Median`, `PercentileContinuous`, `PercentileDiscrete<T>`, `Mode<T>`, `VariancePopulation`, `VarianceSample`, `StandardDeviationPopulation`, `StandardDeviationSample`, and over pairs `CovariancePopulation`, `CovarianceSample`, `Correlation`, `RegressionSlope`, `RegressionIntercept`, `RegressionRSquared` |
| `PureQLWindows` | `Over(rows, partitionBy, orderBy, peers, frame, result)`: the window operator, over an `IQueryable` (what the tree holds) and over a plain sequence (what computes it) |
| `PureQLWindow<TRow>` | what a window function reads for a row: `Row`, `Rows` (the rows of its frame), `RowNumber`, `Rank`, `DenseRank`, `PercentRank`, `CumulativeDistribution`, `Ntile(buckets)`, `Has(offset)`, `At(offset)` |
| `PureQLFrame`, `PureQLRangeUnit` | the frame of a window: `Partition`, `Rows(preceding, following)`, `Range(descending, preceding, unit, following, unit)`; `null` is every row on that side |
| `PureQLRecursion` | `Recursive(seed, next, maxDepth)`: the operator of a recursive subquery, in the same two forms, and a third over a plain sequence whose `next` takes each level as an `IQueryable`, which LINQ to Objects runs the tree's form with when the tree is run as it is over an `EnumerableQuery` |

`PureQLWindows.Over` and `PureQLRecursion.Recursive` over an `IQueryable` are nodes of a tree for a provider to translate. Called as code, they compute the operator over a query of LINQ to Objects (`AsQueryable()`), build a query of a projection's provider, and refuse a query of any other provider with `NotSupportedException`: a provider that calls them while reading a tree (`Pure.RelationalSchema.Storage.PostgreSQL.Querying` evaluates a call that returns a query and reads no lambda parameter) would be given the same node back without end, and refused, it runs the query in memory instead.

### Parameters

Parameters are bound by name to an `IParameter` of [`Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions`](https://github.com/kudima03/Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions), implemented by [`Pure.RelationalSchema.Storage.PureQL.Projection.Parameters`](https://github.com/kudima03/Pure.RelationalSchema.Storage.PureQL.Projection.Parameters). They are passed to the constructor that takes `parameters`. A Pure parameter's `Type.Name` is a PureQL type, `Type.Nullable` makes it that type's typed null, and its `Value` is the invariant text a stored cell holds, parsed as a cell is (a datetime without an offset is UTC). An `IListParameter` binds a list parameter with its `Items`, each a scalar parameter of the item type; its `Value` is not read.

| Parameter | Pure parameter |
|---|---|
| `integer`, `string`, `boolean`, `date`, `time`, `datetime`, `uuid` | `IntegerParameter`, `StringParameter`, `BooleanParameter`, `DateParameter`, `TimeParameter`, `DateTimeParameter`, `UuidParameter` |
| `decimal` | `DecimalParameter`, or `IntegerParameter` (an integer stands for a decimal, as in decimal expressions) |
| nullable `<type>` | the parameter of its type, or that type's typed null (`NullableIntegerParameter`, …) |
| `<type>List` | `<Type>ListParameter` (`IntegerListParameter` also binds a `decimalList`); its items are copied when the projection is built |
| `date`, `time`, `datetime` relative to the run | `CurrentDateParameter`, `DateOffsetParameter`, `CurrentTimeParameter`, `TimeOffsetParameter`, `CurrentDateTimeParameter`, `DateTimeOffsetParameter`, whose `Value` is date math (see [Date math](#date-math)) |

The projection raises `ArgumentException` when it is built, for every parameter of the document, read or not, in any of these cases:
- a parameter is given no value; with the constructors without `parameters`, that is every parameter
- the value has another type, including a type name that is not a PureQL type
- a typed null is given for a parameter that is not nullable
- a `Value` is not a value of its type, or is date math that is malformed for its type or out of range
- a list parameter is given a parameter that is not an `IListParameter`, or a list item of another type or a typed null

Names the query does not use are ignored.

```csharp
IStoredTableDataSet result = new PureQLProjection(
    datasets,
    query,
    new Dictionary<string, IParameter>
    {
        ["min_total"] = new DecimalParameter(new Decimal(100m)),
        ["page_size"] = new IntegerParameter(new Long(20)),
        ["since"] = new NullableDateParameter(),
        ["statuses"] = new StringListParameter([new String("paid"), new String("shipped")]),
    }
);
```

#### Date math

The value of a `date`, `time` or `datetime` parameter may be date math, the relative values of Elasticsearch and Grafana, which the current and offset parameters of `Pure.RelationalSchema.Storage.PureQL.Projection.Parameters` write: `now`, then at most one part per unit in the order `y` (years), `M` (months), `d` (days), `h` (hours), `m` (minutes) and `s` (seconds), each `+N` or `-N`, all of one sign, such as `now-10d` or `now-1M-5d`. A date takes `y`, `M` and `d`, a time `h`, `m` and `s`, a datetime all six. Any other text of these types is parsed as a cell is; a `string` parameter starting with `now` is its text.

Its value is the one at the instant a run starts. Each enumeration reads the projection's clock once, `TimeProvider.GetUtcNow()` truncated to the microsecond, and binds every date math parameter at that instant before the query goes to the provider or runs in memory, so a projection built once and run tomorrow has tomorrow's dates. Years and months move the instant by calendar months together (`now+1y+1M` is `now+13M`), with the day clamped to the end of a shorter month; then the days, hours, minutes and seconds are added. A date is the UTC date of the result and a time its UTC time of day, which wraps around midnight (`now-9h` at 08:30 is 23:30). When the projection is built, date math is checked at the clock's instant, and `projection.Expression` holds that value as the captured variable; a run that reads no date math does not read the clock.

On Thursday 2026-10-29 at 08:30 UTC:

| Pure parameter | Date math | Value |
|---|---|---|
| `new CurrentDateParameter()` | `now` | `2026-10-29` |
| `new DateOffsetParameter(new Long(-10))` | `now-10d` | `2026-10-19` |
| `new DateOffsetParameter(new Long(-1), new Long(-5))` | `now-1M-5d` | `2026-09-24` |
| `new TimeOffsetParameter(new Long(-30), new Long(0))` | `now-30m` | `08:00:00` |
| `new DateTimeOffsetParameter(new Long(-7), new Long(0), new Long(0), new Long(0))` | `now-7d` | `2026-10-22T08:30:00` |

```csharp
IStoredTableDataSet lastTenDays = new PureQLProjection(
    datasets,
    query,
    new Dictionary<string, IParameter>
    {
        ["since"] = new DateOffsetParameter(new Long(-10)),
        ["until"] = new CurrentDateParameter(),
    },
    TimeProvider.System
);
```

### Filtered tables

`PureQLFilteredSchemaDataSet` is an `IStoredSchemaDataSet` over another one whose tables hold only the rows a PureQL condition is true for: the rows a user may read, for instance. A `PureQLProjection` is given it in place of the dataset it filters.

| Member | Description |
|--------|-------------|
| `PureQLFilteredSchemaDataSet(IStoredSchemaDataSet origin, IEnumerable<KeyValuePair<ITable, BooleanRow>> conditions)` | The origin's tables, each under the conditions given for it. Nothing is read or checked until the tables are. |
| `ISchema Schema` | The origin's schema. |
| `IReadOnlyDictionary<ITable, IStoredTableDataSet>` members | The origin's tables, by table hash (`TableHash`): a table given a condition is an `IStoredTableDataSet` of the rows the condition is true for, a table given none is the origin's own. |

```csharp
IStoredSchemaDataSet visible = new PureQLFilteredSchemaDataSet(
    new PostgreSqlStoredSchemaDataSet(schema, connection),
    [
        // orders: an ITable of the schema; only the orders of the tenant
        KeyValuePair.Create<ITable, BooleanRow>(
            orders,
            new EqualUuidRow(
                new FieldUuid("orders", "tenant_id"),
                new LiteralUuid(tenant)
            )
        ),
    ]
);

IStoredTableDataSet result = new PureQLProjection([visible], query, parameters);
```

- **Conditions** are row expressions (`BooleanRow`), one or more per table. A condition belongs to the table with its table's hash (`TableHash`), so it may be given for any `ITable` equal to the dataset's. A table given several conditions holds the rows all of them are true for. A condition given for a table the origin does not have raises `ArgumentException` when the dataset's tables are first read (when a projection over it is built): it would otherwise filter nothing.
- **Fields** of a condition are columns of its table: a field names a column and declares its type and whether it may be null, as a field of a query does (a field declared nullable reads an empty cell as `null`, one declared non-null is parsed as it is). The condition may read columns no query reads. A field's `source` is not read: every field of a table's condition reads that table, whatever alias a query gives it.
- **A condition is closed:** it reads no parameter, no group key and no subquery column, only the table's fields and literals.
- **Semantics** are those of a query's `where`: a row is a row of the table only when the condition is true; where it is false or `null`, the table does not have the row.
- **Every read of the table is filtered:** `from`, every kind of join, subqueries, aggregates and `in` over a subquery, in the main query and in subqueries. The condition applies at the table's scan, below everything else, so for an outer join a row the condition drops is a row that does not exist: the preserved side still appears, with nulls.
- **The filter runs where the query runs.** The scan of a filtered table is `Constant(table).Where(row => condition).Select(row => (field, ...))`, the constant holding the origin's table and the condition reading cells as a field read does (see [Expression tree](#expression-tree)), so the provider of the stored tables is still given the whole query and translates the condition into the statement, at the table's scan (`... FROM orders WHERE tenant_id = ...`, or `LEFT JOIN (SELECT ... FROM orders WHERE ...)`); `Execution` is `Translated`. Over tables held in memory the same tree runs in memory.
- **A condition that cannot be applied fails, and never lets rows through:** a field that names no column of the table, a field declared with another type than its column holds, a parameter, a group key or a subquery column raises `ArgumentException` whenever the table is read, before any row: when a projection whose query reads the table is built, or when the table is enumerated. A query that does not read the table is not affected.
- A filtered table read on its own (`visible[orders]`, enumerated or queried with LINQ) is `origin.Where(row => condition)` run by the origin's provider, so it is filtered in the store too.

### Expression tree

`projection.Expression` is built only from constructs a SQL provider can translate: `Queryable` operators (`Enumerable` ones only over a group, a window's rows or a list), members and methods of the BCL's value types, strings and `Math`, the functions and query operators of this library, value tuples, constants and captured parameters. Nothing in it is compiled, invoked or evaluated on the client except the result rows, built by the last `Select`, which only maps each row of the query, item by item, to an `IRow`. Its shape is fixed:

```
scan(from) [.Where] (.Join | .GroupJoin.SelectMany | .SelectMany)* [.Where(rest of where)]
  plain:   [.Over(window function)]* [.OrderBy/ThenBy] .Select(columns)
  grouped: .GroupBy(keys) [.Where(having)] [.OrderBy/ThenBy] .Select(columns)
  total:   one-row constant .Select(aggregates over the rows)
[.DistinctBy(columns)] [.Skip] [.Take]
combined:  input [.Select(columns)] (.Concat | .Union | .Intersect | .Except)(input [.Select(columns)]) ...
.Select(row => new ResultRow(columns, new object[] { row.Item1, ... }))
```

Everything above the last line is the query a provider is given (see [Running a query](#running-a-query)); its rows are value tuples of the result columns, in order.

- **Sources.** Every stored table is its own root, `Constant(IStoredTableDataSet)` typed `IQueryable<IRow>`, so a provider can tell tables (and their databases) apart and split the tree there. A table is always scanned as `root.Select(row => (field, ...))`, reading only the fields the query reads, and a filtered table (see [Filtered tables](#filtered-tables)) as `root.Where(row => condition).Select(row => (field, ...))`, its condition reading the row's cells as a field read does; a field read is `IsNullOrEmpty(row.Cells[column].Value.TextValue) ? null : Parse(row.Cells[column].Value.TextValue, ...)` for a field the query declares nullable and `Parse(row.Cells[column].Value.TextValue, ...)` for one it declares non-null, the `IColumn` a constant. A subquery is inlined where it is read.
- **Rows** are value tuples: a source row holds its fields, a joined row one source row per source, a group key one value per key, a query result one value per column. Every value is nullable: `long?` (integer), `decimal?`, `string`, `bool?`, `DateOnly?`, `TimeOnly?`, `DateTime?` (UTC), `Guid?`.
- **Joins** are hash joins on the equalities of their condition: each and-ed condition of `on` (or of `where`, when it moves into an inner join) that equates an expression over the rows so far with one over the joined source is a join key. An inner join is `Join` on the tuple of keys, the remaining conditions in a `Where` after it; an outer side is `GroupJoin` (keyed by the driving side: the rows so far for `left`, the joined source for `right`) then `SelectMany` over the matches the remaining conditions hold for, `DefaultIfEmpty`; `full` is the left join `Concat` the joined rows whose group of matches is empty (`!Any()`). A tuple of keys is never null, so `null` keys match, as `=` does. A join whose condition has no such equality is a correlated `SelectMany` over the joined rows the condition holds for (`DefaultIfEmpty` for an outer side; `full` adds the joined rows `!Any(...)` row matches). An unmatched side is the default tuple: all nulls.
- **Aggregates** are LINQ's over the group or, over all rows, over the query's rows, which are one shared subtree. A total query reads a one-row constant source, as SQL's `SELECT` without `FROM`.
- **Semantics** are C#'s where they match the specification: `null == null`, an ordering comparison with one null is false, lifted operators. Strings compare with `string.CompareOrdinal` and order with `StringComparer.Ordinal` (SQL's `"C"` collation, code point order); ascending order puts null first. Parameters are captured variables, the `Value` of a constant holding the parameter, as C# closures are.

Built in, before any provider sees it:

- constants fold (an operation that fails, like `1 / 0`, is kept and fails the query; two constant strings compare by code point; decimals fold exactly, as they are computed in memory; no `double` is computed), `true`/`false` fold out of conditions, lifting is dropped where a value cannot be null and an operation over a `null` literal is `null`;
- `where` is split into its and-ed conditions, each moved to the scan of the source it reads (above the scan's `Select`; only the condition of a filtered table stands below it), or into the join that first sees every source it reads, unless an outer join may leave one of them without a row; a condition on a subquery source moves into the subquery (onto its `having` when it reads an aggregate), but never below a window function, a set operation or a recursion;
- adjacent `Where`s merge, `Where(true)` and identity `Select`s are dropped, consecutive `Select`s are one, and pagination ends the query (only the result rows' `Select` follows it);
- a repeated order key or one that is the same for every row is dropped, a `distinctBy` over every group key is dropped, and a subquery read only by `in` (without pagination) is neither ordered nor `distinctBy`-filtered.

#### Shapes of specification 3.0.0

Everything a tree of specification `0.1.0-preview.2.0.1` held has the shape it had, with two exceptions the specification changed: `a >= b` and `a <= b` of two nullable operands are `a >= b || (a == null && b == null)` (two nulls are at least and at most each other), and an `add` / `subtract` / `multiply` over a list that mixes integers and decimals keeps its integer steps (`(decimal?)(a + b) + c`, the integer step checked). The new constructs are:

| Construct | Shape |
|---|---|
| `orderBy` by `{ column }` | the `OrderBy` / `ThenBy` key is the expression of that `select` column |
| `pagination` | `.Skip(n)` only with `skip`, `.Take(n)` only with `take` |
| `unionAll`, `union`, `intersect`, `except` | `Queryable.Concat` / `Union` / `Intersect` / `Except` over the inputs' rows, left to right; an input whose columns are in another order or of a narrower type is first `.Select(row => (column, ...))` into the operation's columns |
| inline rows | `oneRow.SelectMany(one => new[] { (cell, ...), ... }, (one, row) => row)`: the one-row constant of a total query and an array of tuples, a cell a constant or a captured parameter |
| recursive subquery | `PureQLRecursion.Recursive(seed, level => next, maxDepth)`: `next` is the tree of the `next` query with the `IQueryable` parameter `level` where it reads the subquery; `maxDepth` a constant or a captured parameter |
| window function | one `PureQLWindows.Over(rows, row => (partition keys), partition => partition.OrderBy(...)..., row => (sort keys), frame, window => (window.Row, value))` per function, each over the rows of the one before, so after n functions a row is `(...((row, value1), value2)..., valuen)` and the `select` reads the values there. `frame` is `PureQLFrame.Partition`, `PureQLFrame.Rows(preceding, following)` or `PureQLFrame.Range(descending, preceding, unit, following, unit)`, its counts constants or captured parameters. `value` is an aggregate over `window.Rows` (the `Enumerable` aggregates of a group), `window.RowNumber`, `window.Rank`, `window.DenseRank`, `window.PercentRank`, `window.CumulativeDistribution`, `window.Ntile(buckets)`, or for `previous` / `next` `window.Has(±offset) ? operand over window.At(±offset) : default over window.Row` |
| `countDistinct` | `rows.Select(selector).Where(value => value != null).Distinct().LongCount()` |
| `first`, `last` | `rows.OrderBy(...).Select(selector).FirstOrDefault()`, the order reversed for `last` |
| `concatAll` | `string.Join(separator, rows.OrderBy(...).Select(selector).Where(value => value != null)[.Distinct()])` |
| statistics | `PureQLAggregates.Median(rows.Select(selector))`, `PercentileContinuous(values, fraction)`, `PercentileDiscrete<T>`, `Mode<T>`, `VariancePopulation`, …; two columns as `rows.Select(row => (left, right))` |
| `integerDivide`, `modulo` of decimals | `PureQLMath.IntegerDivide(left, right)`, `PureQLMath.Modulo(left, right)`; of two integers still `/` and `%` |
| `round` | without `digits` `(long)Math.Round(value, mode)`; a decimal with literal digits `Math.Round(value, digits, mode)` or, for negative digits, `Math.Round(value / 100m, mode) * 100m`; with computed digits the three-way conditional of both with `(decimal)Math.Pow(10, -digits)`; an integer with digits `PureQLMath.Round(value, digits, mode)`. `mode` is `MidpointRounding.AwayFromZero` or `ToEven` |
| `truncate` | without `digits` `(long)Math.Truncate(value)`, with digits `PureQLMath.Truncate(value, digits)` |
| `abs`, `sign` | `Math.Abs(value)`, `(long)Math.Sign(value)` |
| `greatest`, `least` | numbers `Math.Max` / `Math.Min` folded; other types `new[] { a, b, ... }.Max()` / `.Min()`, strings with `StringComparer.Ordinal` |
| `power` … `atan2` | `PureQLMath.Power(operand, exponent)`, …; `pi` is the decimal constant |
| `nullIfEqual` | `left == right ? null : left` |
| `concat`, `concatWith` | `string.Concat` folded; `PureQLText.ConcatWith(separator, new[] { a, b, ... })` |
| `contains`, `startsWith`, `endsWith` | `operand != null && pattern != null && operand.Contains(pattern)`, `.StartsWith(pattern, StringComparison.Ordinal)`, `.EndsWith(pattern, StringComparison.Ordinal)` |
| `normalize` | `operand.Normalize(NormalizationForm.FormC)` (`FormD`, `FormKC`, `FormKD`) |
| other string operators | `PureQLText.Length(operand)`, `Upper`, `Lower`, `FoldCase`, `RemoveDiacritics`, `Trim(operand, new[] { removable, ... })`, `Substring(operand, start[, length])`, `Replace`, `Position`, `SplitPart`, `PadStart`, `PadEnd`, `Repeat`, `Reverse` |
| regular expressions | `operand != null && PureQLText.MatchesRegex(operand, pattern)`, `operand == null ? null : PureQLText.ExtractRegex(operand, pattern[, group])`, `PureQLText.ReplaceRegex(operand, pattern, replacement)`; `pattern` and `group` constants or captured parameters |
| `dateAddMonths`, `datetimeAddMonths` | `date.AddMonths((int)months)`, `instant.AddMonths((int)months)`; with a zone `PureQLTime.AddMonths(instant, months, zone)` |
| `dateDiffMonths`, `datetimeDiffMonths` | `PureQLTime.DiffMonths(left, right[, zone])` |
| `dateFromParts`, `timeFromParts`, `datetimeFromParts` | `new DateOnly((int)year, (int)month, (int)day)`, `PureQLTime.TimeFromParts(hour, minute, second)`, `date.ToDateTime(time, DateTimeKind.Utc)` or `PureQLTime.DatetimeFromParts(date, time, zone)` |
| parts | of a date `(long)date.Year`, `.Month`, `.Day`, `.DayOfYear`, `PureQLTime.Quarter(date)`, `DayOfWeek`, `Week`, `WeekYear`; of a time `(long)time.Hour`, `.Minute`, `.Second`, `PureQLTime.Microsecond(time)`; of a datetime the same over `DateOnly.FromDateTime(instant)` / `TimeOnly.FromDateTime(instant)`, or over `PureQLTime.LocalDate(instant, zone)` / `PureQLTime.LocalTime(instant, zone)` |
| `startOf…` | `PureQLTime.StartOfMonth(value)`, …, with the zone as a second argument |
| `asDate`, `asTime`, `asDatetime` | `DateOnly.FromDateTime(instant)`, `TimeOnly.FromDateTime(instant)`, `date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)`; with a zone `PureQLTime.LocalDate`, `LocalTime`, `DatetimeFromParts(date, TimeOnly.MinValue, zone)`; of a string `PureQLConversions.AsDate(text)`, … |
| other conversions | `PureQLConversions.AsString(value)`, `AsInteger(text)`, …, `TryAsInteger(text)`, …; `asDecimal` of an integer is `(decimal?)value`; `asNullable` is the value |

Every lifted operator checks its operands for `null` around these (`operand == null || zone == null ? null : ...`), also the operands named by their role and a zone.

### Running a query

Enumerating the projection hands the query below the result rows (`projection.Expression` without its last `Select`) to the `Provider` of the first stored table the query reads whose provider is not LINQ to Objects' (`EnumerableQuery`, the provider of a table held in memory), as `provider.CreateQuery(query)`, and maps each value the provider returns to a result row. A provider that translates the tree, such as `Pure.RelationalSchema.Storage.PostgreSQL.Querying`, thus runs the whole query (filters, joins, grouping, ordering, pagination) as one SQL statement, and reads only the result; the stored tables are never enumerated in full. For that, the provider must understand the tree's shape: value tuples, and a field read as `IsNullOrEmpty(row.Cells[column].Value.TextValue) ? null : Parse(row.Cells[column].Value.TextValue, ...)` (`long.Parse`, `decimal.Parse`, `bool.Parse`, `DateOnly.Parse`, `TimeOnly.Parse`, `DateTime.Parse`, `Guid.Parse` with the invariant culture, or the text itself for a string column), which is the typed read of the column and maps to the column as a whole (`Pure.RelationalSchema.Storage.PostgreSQL.Querying` from 0.1.0-preview.0.9.0 reads it as the stored column itself, with no text in between, so a join or filter on a date, uuid or number compares the column, which PostgreSQL hashes, estimates and indexes). A field the query declares non-null is read without the null check, as `Parse(row.Cells[column].Value.TextValue, ...)`: the value is never null, so SQL compares, joins and filters on it with `=`, which a hash or merge join and an index serve, where a nullable value needs `IS NOT DISTINCT FROM` (`null` equals `null`), which PostgreSQL can only evaluate row by row in a nested loop. An aggregate over all rows in a plain `select` reads the one shared subtree of the query's rows, which `Linq.Expressions.Translation.PostgreSql` from 0.1.0-preview.0.7.0 translates as a window aggregate (`COUNT(*) OVER ()`) computed in the pass that reads the rows, not as a subquery that runs the joins again. Declare a join key non-null wherever the column holds no NULL. A join key declared nullable that the join's `on` (or a `where` condition) guards with `notEqual(key, null)` is compared with `=` too, as the specification allows: the key is read as its value, or the default of its type where it is null (`key ?? default`), so it is never null; the guard drops every row with a null key, so the result is the same.

The tree runs in memory, with LINQ to Objects, when the tables are held in memory, or when the provider refuses the query with `NotSupportedException` (when the query is created, enumerated, or before its first value): a construct the provider cannot translate, tables of more than one database, or a source that is not a stored table (a total query's one-row constant). Any other exception the provider raises fails the enumeration. After an enumeration, `Execution` says which happened (`Translated` or `InMemory`) and `FallbackReason` holds the provider's exception when the query fell back to memory, so that a host can log it.

Run in memory, the tree is compiled once per enumeration as `Enumerable` calls: each stored table is read once, a query nested in a lambda that reads nothing of it (a joined source, an `in` list, an aggregate over all rows) is computed once and answers `in` from a hash set, hash joins cost the rows of both sides rather than their product, and strings order by code point. LINQ composed over the projection (`projection.Take(10)`, `projection.Count()`) runs in memory over the projection's rows, which are read as above.

Run in memory, the tree is also computed exactly, on a rewritten copy: a decimal `+`, `-`, `*` and `/` is `PureQLMath.Add`, `Subtract`, `Multiply` and `Divide`; `Math.Round` of a quotient is rounded once from the exact quotient (`PureQLMath.RoundQuotient`); `(decimal)Math.Pow(10, n)` is `PureQLMath.PowerOfTen(n)`, with no `double`; `Sum` and `Average` of integers and decimals are `PureQLAggregates.Sum` and `Average`; an integer `%` is `PureQLMath.Modulo`. A provider that translates the tree answers for the arithmetic of its store instead: PostgreSQL's `numeric` is exact, but rounds a quotient to its own number of digits.

A construct a provider does not translate does not fail a query: the provider refuses the tree with `NotSupportedException` and the whole query runs in memory, with every stored table it reads read in full. With `Pure.RelationalSchema.Storage.PostgreSQL.Querying` 0.1.0-preview.0.10.0 (translator `Linq.Expressions.Translation.PostgreSql` 0.1.0-preview.0.9.0) that is every query that holds a function or a query operator of this library ([Functions and query operators](#functions-and-query-operators)), reads inline rows, or holds a BCL shape its translator does not know yet (`string.Join` of `concatAll`, an ordered `FirstOrDefault` over a group of `first` / `last`, `new DateOnly(...)` of `dateFromParts`, the `Max` / `Min` of an array of `greatest` / `least`, …): 214 of the 386 queries of `PureQL.CSharp.Model.Samples` 0.1.0-preview.4.1.0, and 14 more read no stored table. Refused, a query reads its stored tables through the provider's own enumeration, which reads a `time` cell as `HH:mm`, without its seconds (kudima03/Pure.RelationalSchema.Storage.PostgreSQL.Querying#101): of the samples only `BlankValuesOfEveryTypeAsNullQuery` reads such a time. The other constructs of specification 3.0.0 that the samples hold (`orderBy` by a column, `pagination` with one side, most set operations) are translated.


### Storage mapping

| Column type | PureQL type |
|---|---|
| `long`, `int`, `uint`, `ulong`, `ushort` | `integer` |
| `double`, `float`, `decimal` (a result column of this library) | `decimal` |
| `string`, `bool`, `date`, `time`, `datetime`, `uuid` | `string`, `boolean`, `date`, `time`, `datetime`, `uuid` |

Empty cell text is `NULL`. The query says whether a field holds it: a field declared nullable reads an empty cell as `null`, a field declared non-null (in every reference where its source has a row of its own, so not on the optional side of an outer join) is parsed as it is, and an empty cell there fails the query, in memory with `FormatException` (an empty string cell is the empty string). A stored datetime without an offset is read as UTC. Result columns are typed `long` for `integer` and, for `decimal`, a column type named `decimal` of this library, as `Pure.RelationalSchema` has no column type that holds a decimal exactly; result cells hold invariant text (`True`/`False`, a `decimal` in the specification's spelling, exact with no exponent and no trailing zeros in the fraction, `yyyy-MM-dd`, `HH:mm:ss[.fffffff]`, `yyyy-MM-ddTHH:mm:ss[.fffffff]Z`, the UTC instant written with `Z` as a `datetime` literal is, lower case uuids).

### Not supported

- Stored times and datetimes are read to the .NET tick (100 ns); the specification's values have microsecond precision, so finer stored digits are kept as read. A stored `decimal` cell is read as `System.Decimal` reads it: text of more than 28 significant digits, which is not a value of the specification's `decimal`, is not refused.
- Parameter values are the cell texts of Pure parameters, read as cells are (see [Parameters](#parameters)), not JSON literals: text in another spelling than a literal's (`1.50`, a datetime with an offset) is accepted where .NET parses it. A `decimal` beyond 28 digits is refused.
- The longest string is .NET's: `repeat`, `padStart`, `padEnd` and `concatAll` fail with the runtime's exception beyond it. A regular expression runs on .NET's backtracking engine without a time limit.
- What the specification leaves unspecified is taken from the machine: the Unicode data behind `normalize` (the runtime's normalization, ICU on Linux and macOS; with `InvariantGlobalization` .NET does not normalize, and `normalize` and `removeDiacritics` return their operand unchanged), and the release of the tz database behind the rules of a zone. A zone outside the specification's list is refused, also an older link the machine's database has.
- A row of a model built in code that the specification's schema would reject (an `offset` of 1, `"skip": 0`, an empty list literal, an operator declared with another type than its operands give) is not checked against the schema: the interpreter checks only what the schema leaves to it.

## Dependencies

- [`Pure.Primitives`](https://github.com/kudima03/Pure.Primitives/tree/3.8.0) — core primitive value types (`String`, `Date`, `DateTime`, `Time`, `Guid`, `Number`)
- [`Pure.RelationalSchema`](https://github.com/kudima03/Pure.RelationalSchema/tree/2.2.0) — relational schema abstractions (`ISchema`, `ITable`, `IColumn`, column type hierarchy)
- [`Pure.RelationalSchema.HashCodes`](https://github.com/kudima03/Pure.RelationalSchema.HashCodes/tree/3.4.0) — structural hash codes for relational schema types
- [`Pure.RelationalSchema.Storage`](https://github.com/kudima03/Pure.RelationalSchema.Storage/tree/0.1.0-preview.8.1.0) — in-memory relational data model (`IStoredSchemaDataSet`, `IStoredTableDataSet`, `IRow`, `ICell`)
- [`PureQL.CSharp.Model`](https://github.com/kudima03/PureQL.CSharp.Model/tree/0.1.0-preview.15.0.0) — PureQL query model (`PureQLQuery`, `Query`, expressions per context), generated from PureQL specification `0.1.0-preview.3.0.0`
- [`Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions`](https://github.com/kudima03/Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions/tree/0.1.0-preview.0.3.0) — the parameters a query's parameters are bound to (`IParameter`, `IListParameter`)
- [`Pure.Collections.Generic`](https://github.com/kudima03/Pure.Collections.Generic/tree/0.1.0-preview.3.1.0) — generic collection utilities used in row projection
- `System.Linq.Async` — async enumeration of the result rows

## Target Frameworks

- .NET 11 (`PureQL.CSharp.Model` is built with C# 15 unions, which need it)

## Installation

```bash
dotnet add package Pure.RelationalSchema.Storage.PureQL.Projection
```

## Usage

```csharp
// datasets: IEnumerable<IStoredSchemaDataSet> populated by your storage layer
// query:    a PureQLQuery, built directly or deserialized with
//           PureQL.CSharp.Model.Serialization

IStoredTableDataSet result = new PureQLProjection(datasets, query);

foreach (IRow row in result)
{
    // ...
}

await foreach (IRow row in result)
{
    // ...
}
```

A query selecting one field. A model record converts to every union it may stand in, so a query is written with records only:

```csharp
PureQLQuery query = new MainPlainQueryWithoutPagination(
    from: new FromEntity("mySchema.myTable"),
    select: [new SelectItemProjection("name", new FieldString("mySchema.myTable", "name"))]
);
```

Each row with its rank among the rows of its group, a window function:

```csharp
PureQLQuery ranked = new MainPlainQueryWithoutPagination(
    from: new FromEntity("shop.orders"),
    select:
    [
        new SelectItemProjection("id", new FieldUuid("shop.orders", "id")),
        new SelectItemProjection(
            "rank",
            new RankProjection(
                new OrderedWindow(
                    orderBy:
                    [
                        new OrderItemRow(
                            new FieldDecimal("shop.orders", "total"),
                            SortDirection.Desc
                        ),
                    ],
                    partitionBy: [new FieldUuid("shop.orders", "user_id")]
                )
            )
        ),
    ]
);
```

[`PureQL.CSharp.Model.Samples`](https://github.com/kudima03/PureQL.CSharp.Model.Samples) holds a query for every feature of the specification; this package's tests run all of them.
