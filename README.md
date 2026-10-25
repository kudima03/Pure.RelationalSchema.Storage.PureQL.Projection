# Pure.RelationalSchema.Storage.PureQL.Projection

Compiles PureQL queries over relational schema datasets into one LINQ expression tree: a `PureQLQuery` from `PureQL.CSharp.Model` becomes an `IStoredTableDataSet` whose `IQueryable<IRow>` tree a LINQ provider can translate to SQL, and which runs in memory when enumerated.

[![.NET build & test](https://github.com/kudima03/Pure.RelationalSchema.Storage.PureQL.Projection/actions/workflows/build-and-test.yml/badge.svg?branch=main)](https://github.com/kudima03/Pure.RelationalSchema.Storage.PureQL.Projection/actions/workflows/build-and-test.yml)
[![Build and Deploy](https://github.com/kudima03/Pure.RelationalSchema.Storage.PureQL.Projection/actions/workflows/publish-nuget.yml/badge.svg?branch=main)](https://github.com/kudima03/Pure.RelationalSchema.Storage.PureQL.Projection/actions/workflows/publish-nuget.yml)
[![NuGet](https://img.shields.io/nuget/v/Pure.RelationalSchema.Storage.PureQL.Projection)](https://www.nuget.org/packages/Pure.RelationalSchema.Storage.PureQL.Projection)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Overview

`Pure.RelationalSchema.Storage.PureQL.Projection` is an interpreter for the [PureQL specification](https://github.com/kudima03/PureQL-Specification) `0.1.0-preview.2.0.1` over the Pure relational storage model. Given a collection of `IStoredSchemaDataSet` objects and a `PureQLQuery`, it produces an `IStoredTableDataSet` whose rows are the query's result under the specification's semantics.

The query is compiled when the projection is built to a single `IQueryable<IRow>` expression tree made of standard query operators over one root per stored table (see [Expression tree](#expression-tree)). Enumerating the projection runs that tree in memory with LINQ to Objects; a provider that translates the tree (to PostgreSQL, across several databases) reads `projection.Expression` instead.

## Public API

### `PureQLProjection`

The only public type. Implements `IStoredTableDataSet`, `IAsyncEnumerable<IRow>` and `IQueryable<IRow>`.

| Member | Description |
|--------|-------------|
| `PureQLProjection(IEnumerable<IStoredSchemaDataSet> datasets, PureQLQuery query)` | Validates the query against the datasets' schemas and builds the projection. |
| `PureQLProjection(IEnumerable<IStoredSchemaDataSet> datasets, Query query)` | The same for a query without subqueries (the model's subquery type). |
| `PureQLProjection(..., IReadOnlyDictionary<string, ILiteral> parameters)` | Either of the above with parameters bound by name to literals (see [Parameters](#parameters)). |
| `ITable TableSchema` | An unnamed table with one column per `select` item, named by its alias and typed by its declared type. |
| `IEnumerator<IRow> GetEnumerator()` | Runs the query and enumerates the result rows. Every enumeration runs the query again. |
| `IAsyncEnumerator<IRow> GetAsyncEnumerator(...)` | Async enumeration over the same rows. |
| `Expression Expression` | The query's expression tree, for a LINQ provider to translate (see below). |
| `Type ElementType / IQueryProvider Provider` | `IQueryable<IRow>` plumbing: LINQ composed over the projection runs in memory. |

### What is supported

Every clause and operator of the specification: `from` and `joins` (`inner`, `left`, `right`, `full`, aliases and self-joins), `where`, `groupBy` over any row expression with keys referenced by alias, `having`, `select`, `aggregate` (total queries), `orderBy` over any expression, `distinctBy`, `pagination`, and subqueries read through `from`, `join` and `in`. All operators in the row, projection, group and total contexts are supported, including aggregates with `predicate` and `over`, and comparisons, `min` and `max` over every type.

A plain query returns one row per row left by `where`, a total query exactly one row (even over no rows) and a grouped query one row per group. `distinctBy` runs after `orderBy` and keeps, of the rows equal on the listed columns, the first in order.

The semantics are the specification's: `null` equals `null` and sorts first, ordering comparisons against `null` are false, lifted operators are `null` on a `null` operand, `integer` is a checked 64-bit integer, `decimal` is exact (`System.Decimal`), strings compare by code point, a `datetime` is an instant written in UTC, and seconds and `time` / `datetime` averages round half away from zero to the microsecond. Overflow, division by zero and dates outside the calendar fail the query with the corresponding .NET exception.

The checks the specification leaves to the interpreter run when the projection is built, and a failing query raises `ArgumentException`: entities, subqueries, sources and fields exist; each field declares its column's type and, after outer joins, its nullability; group key references name a key and repeat its type; source names, group key aliases, column aliases and subquery names are unique; `distinctBy` lists only column aliases; a subquery reads only subqueries declared before it; a bound parameter's literal has the parameter's type, and a bound `pagination` is in range.

### Parameters

Parameters are bound by name to literals of the query model (`PureQL.CSharp.Model.Literals` / `.Lists`), passed to the constructor that takes `parameters`. Binding a parameter substitutes the value of a literal of its type:

| Parameter | Literal |
|---|---|
| `integer`, `string`, `boolean`, `date`, `time`, `datetime`, `uuid` | `LiteralInteger`, `LiteralString`, `LiteralBoolean`, `LiteralDate`, `LiteralTime`, `LiteralDatetime`, `LiteralUuid` |
| `decimal` | `LiteralDecimal`, or `LiteralInteger` (an integer stands for a decimal, as in decimal expressions) |
| nullable `<type>` | the literal of its type, or that type's typed null (`LiteralIntegerNullable`, …) |
| `<type>List` | `ListLiteral<Type>` (`ListLiteralInteger` also binds a `decimalList`); its items are copied when the projection is built |

A literal of another type, or a typed null for a parameter that is not nullable, raises `ArgumentException` when the projection is built, for every parameter of the document, read or not. Names the query does not use are ignored. A parameter given no literal (or every parameter, with the constructors without `parameters`) raises `NotSupportedException` when the query reads it, so a tree can still be built for a provider that binds parameters itself.

```csharp
IStoredTableDataSet result = new PureQLProjection(
    datasets,
    query,
    new Dictionary<string, ILiteral>
    {
        ["min_total"] = new LiteralDecimal(100m),
        ["page_size"] = new LiteralInteger(20),
        ["statuses"] = new ListLiteralString(["paid", "shipped"]),
    }
);
```

### Expression tree

`projection.Expression` is built only from constructs a SQL provider translates: `Queryable` operators (`Enumerable` ones only over a group or a list), members and methods of the BCL's value types, strings and `Math`, value tuples, constants and captured parameters. Nothing in it is compiled, invoked or evaluated on the client except the result rows, built by the last `Select`. Its shape is fixed:

```
scan(from) [.Where] (.Join | .GroupJoin.SelectMany | .SelectMany)* [.Where(rest of where)]
  plain:   [.OrderBy/ThenBy] .Select(columns)
  grouped: .GroupBy(keys) [.Where(having)] [.OrderBy/ThenBy] .Select(columns)
  total:   one-row constant .Select(aggregates over the rows)
[.DistinctBy(columns)] [.Skip] [.Take]
```

- **Sources.** Every stored table is its own root, `Constant(IStoredTableDataSet)` typed `IQueryable<IRow>`, so a provider can tell tables (and their databases) apart and split the tree there. A table is always scanned as `root.Select(row => (field, ...))`, reading only the fields the query reads; a field read is `IsNullOrEmpty(row.Cells[column].Value.TextValue) ? null : Parse(row.Cells[column].Value.TextValue, ...)`, the `IColumn` a constant. A subquery is inlined where it is read.
- **Rows** are value tuples: a source row holds its fields, a joined row one source row per source, a group key one value per key, a query result one value per column. Every value is nullable: `long?` (integer), `decimal?`, `string`, `bool?`, `DateOnly?`, `TimeOnly?`, `DateTime?` (UTC), `Guid?`.
- **Joins** are hash joins on the equalities of their condition: each and-ed condition of `on` (or of `where`, when it moves into an inner join) that equates an expression over the rows so far with one over the joined source is a join key. An inner join is `Join` on the tuple of keys, the remaining conditions in a `Where` after it; an outer side is `GroupJoin` (keyed by the driving side: the rows so far for `left`, the joined source for `right`) then `SelectMany` over the matches the remaining conditions hold for, `DefaultIfEmpty`; `full` is the left join `Concat` the joined rows whose group of matches is empty (`!Any()`). A tuple of keys is never null, so `null` keys match, as `=` does. A join whose condition has no such equality is a correlated `SelectMany` over the joined rows the condition holds for (`DefaultIfEmpty` for an outer side; `full` adds the joined rows `!Any(...)` row matches). An unmatched side is the default tuple: all nulls.
- **Aggregates** are LINQ's over the group or, over all rows, over the query's rows, which are one shared subtree. A total query reads a one-row constant source, as SQL's `SELECT` without `FROM`.
- **Semantics** are C#'s, which match the specification: `null == null`, an ordering comparison with null is false, lifted operators. Strings compare with `string.CompareOrdinal` and order with `StringComparer.Ordinal` (SQL's `"C"` collation, code point order); ascending order puts null first. Parameters are captured variables, the `Value` of a constant holding the parameter, as C# closures are, bound or not; reading an unbound one fails.

Built in, before any provider sees it:

- constants fold (an operation that fails, like `1 / 0`, is kept and fails the query; two constant strings compare by code point), `true`/`false` fold out of conditions, lifting is dropped where a value cannot be null and an operation over a `null` literal is `null`;
- `where` is split into its and-ed conditions, each moved to the scan of the source it reads, or into the join that first sees every source it reads, unless an outer join may leave one of them without a row; a condition on a subquery source moves into the subquery (onto its `having` when it reads an aggregate);
- adjacent `Where`s merge, `Where(true)` and identity `Select`s are dropped, consecutive `Select`s are one, and pagination comes last;
- a repeated order key or one that is the same for every row is dropped, a `distinctBy` over every group key is dropped, and a subquery read only by `in` (without pagination) is neither ordered nor `distinctBy`-filtered.

Run in memory, the tree is compiled once per enumeration as `Enumerable` calls: each stored table is read once, a query nested in a lambda that reads nothing of it (a joined source, an `in` list, an aggregate over all rows) is computed once and answers `in` from a hash set, hash joins cost the rows of both sides rather than their product, and strings order by code point.

### Storage mapping

| Column type | PureQL type |
|---|---|
| `long`, `int`, `uint`, `ulong`, `ushort` | `integer` |
| `double`, `float` | `decimal` |
| `string`, `bool`, `date`, `time`, `datetime`, `uuid` | `string`, `boolean`, `date`, `time`, `datetime`, `uuid` |

Empty cell text is `NULL`. A stored datetime without an offset is read as UTC. Result columns are typed `long` for `integer` and `double` for `decimal`; result cells hold invariant text (`True`/`False`, `yyyy-MM-dd`, `HH:mm:ss[.fffffff]`, `yyyy-MM-ddTHH:mm:ss[.fffffff]` in UTC, lower case uuids).

### Not supported

- Stored times and datetimes are read to the .NET tick (100 ns); the specification's values have microsecond precision, so finer stored digits are kept as read.

## Dependencies

- [`Pure.Primitives`](https://github.com/kudima03/Pure.Primitives/tree/3.6.2) — core primitive value types (`String`, `Date`, `DateTime`, `Time`, `Guid`, `Number`)
- [`Pure.RelationalSchema`](https://github.com/kudima03/Pure.RelationalSchema/tree/2.0.0) — relational schema abstractions (`ISchema`, `ITable`, `IColumn`, column type hierarchy)
- [`Pure.RelationalSchema.HashCodes`](https://github.com/kudima03/Pure.RelationalSchema.HashCodes/tree/3.3.0) — structural hash codes for relational schema types
- [`Pure.RelationalSchema.Storage`](https://github.com/kudima03/Pure.RelationalSchema.Storage/tree/0.1.0-preview.7.0.0) — in-memory relational data model (`IStoredSchemaDataSet`, `IStoredTableDataSet`, `IRow`, `ICell`)
- [`PureQL.CSharp.Model`](https://github.com/kudima03/PureQL.CSharp.Model/tree/0.1.0-preview.13.0.0) — PureQL query model (`PureQLQuery`, `Query`, expressions per context) — tracks PureQL specification `0.1.0-preview.2.0.0`
- [`Pure.Collections.Generic`](https://github.com/kudima03/Pure.Collections.Generic/tree/0.1.0-preview.3.0.0) — generic collection utilities used in row projection

## Target Frameworks

- .NET 8
- .NET 9
- .NET 10

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

A query selecting one field:

```csharp
PureQLQuery query = new PureQLQuery(
    new MainPlainQuery(
        new From(new FromEntity("mySchema.myTable")),
        [
            new SelectItemProjection(
                new SelectItemProjectionNonNullable(
                    new SelectItemProjectionString(
                        "name",
                        new StringProjection(new FieldString("mySchema.myTable", "name"))
                    )
                )
            ),
        ]
    )
);
```

[`PureQL.CSharp.Model.Samples`](https://github.com/kudima03/PureQL.CSharp.Model.Samples) holds a query for every feature of the specification; this package's tests run all of them.
