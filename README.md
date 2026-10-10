# Pure.RelationalSchema.Storage.PureQL.Projection

Executes PureQL queries against in-memory relational schema datasets: a `PureQLQuery` from `PureQL.CSharp.Model` becomes an enumerable `IStoredTableDataSet`.

[![.NET build & test](https://github.com/kudima03/Pure.RelationalSchema.Storage.PureQL.Projection/actions/workflows/build-and-test.yml/badge.svg?branch=main)](https://github.com/kudima03/Pure.RelationalSchema.Storage.PureQL.Projection/actions/workflows/build-and-test.yml)
[![Build and Deploy](https://github.com/kudima03/Pure.RelationalSchema.Storage.PureQL.Projection/actions/workflows/publish-nuget.yml/badge.svg?branch=main)](https://github.com/kudima03/Pure.RelationalSchema.Storage.PureQL.Projection/actions/workflows/publish-nuget.yml)
[![NuGet](https://img.shields.io/nuget/v/Pure.RelationalSchema.Storage.PureQL.Projection)](https://www.nuget.org/packages/Pure.RelationalSchema.Storage.PureQL.Projection)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Overview

`Pure.RelationalSchema.Storage.PureQL.Projection` is an interpreter for the [PureQL specification](https://github.com/kudima03/PureQL-Specification) `0.1.0-preview.1.0.0` over the Pure relational storage model. Given a collection of `IStoredSchemaDataSet` objects and a `PureQLQuery`, it produces an `IStoredTableDataSet` whose rows are the query's result under the specification's semantics.

Everything runs in memory: no SQL, no external engine.

## Public API

### `PureQLProjection`

The only public type. Implements `IStoredTableDataSet`, `IAsyncEnumerable<IRow>` and `IQueryable<IRow>`.

| Member | Description |
|--------|-------------|
| `PureQLProjection(IEnumerable<IStoredSchemaDataSet> datasets, PureQLQuery query)` | Validates the query against the datasets' schemas and builds the projection. |
| `PureQLProjection(IEnumerable<IStoredSchemaDataSet> datasets, Query query)` | The same for a query without subqueries (the model's subquery type). |
| `ITable TableSchema` | An unnamed table with one column per `select` item, named by its alias and typed by its declared type. |
| `IEnumerator<IRow> GetEnumerator()` | Runs the query and enumerates the result rows. Every enumeration runs the query again. |
| `IAsyncEnumerator<IRow> GetAsyncEnumerator(...)` | Async enumeration over the same rows. |
| `Type ElementType / Expression / Provider` | `IQueryable<IRow>` plumbing for further LINQ composition. |

### What is supported

Every clause and operator of the specification: `from` and `joins` (`inner`, `left`, `right`, `full`, aliases and self-joins), `where`, `groupBy` over any row expression, `having`, `select`, `distinct`, `orderBy` over any expression, `pagination`, and subqueries read through `from`, `join` and `in`. All operators in the row, projection and group contexts are supported, including aggregates with `predicate` and `over`.

The semantics are the specification's: `null` equals `null` and sorts first, ordering comparisons against `null` are false, lifted operators are `null` on a `null` operand, `integer` is a checked 64-bit integer, `decimal` is exact (`System.Decimal`), strings compare by code point, and a `datetime` is an instant written in UTC. Overflow, division by zero and dates outside the calendar fail the query with the corresponding .NET exception.

The checks the specification leaves to the interpreter run when the projection is built, and a failing query raises `ArgumentException`: entities, subqueries, sources and fields exist; each field declares its column's type and, after outer joins, its nullability; group key references match their keys; source names, column aliases and subquery names are unique; a subquery reads only subqueries declared before it.

### Storage mapping

| Column type | PureQL type |
|---|---|
| `long`, `int`, `uint`, `ulong`, `ushort` | `integer` |
| `double`, `float` | `decimal` |
| `string`, `bool`, `date`, `time`, `datetime`, `uuid` | `string`, `boolean`, `date`, `time`, `datetime`, `uuid` |

Empty cell text is `NULL`. A stored datetime without an offset is read as UTC. Result columns are typed `long` for `integer` and `double` for `decimal`; result cells hold invariant text (`True`/`False`, `yyyy-MM-dd`, `HH:mm:ss[.fffffff]`, `yyyy-MM-ddTHH:mm:ss[.fffffff]` in UTC, lower case uuids).

### Not supported

- **Parameters.** There is no API to bind parameter values, so evaluating a parameter (in an expression, a list or `pagination`) raises `NotSupportedException`.
- Time and datetime precision is the .NET tick (100 ns), not the specification's nanosecond.

## Dependencies

- [`Pure.Primitives`](https://github.com/kudima03/Pure.Primitives/tree/3.6.2) — core primitive value types (`String`, `Date`, `DateTime`, `Time`, `Guid`, `Number`)
- [`Pure.RelationalSchema`](https://github.com/kudima03/Pure.RelationalSchema/tree/2.0.0) — relational schema abstractions (`ISchema`, `ITable`, `IColumn`, column type hierarchy)
- [`Pure.RelationalSchema.HashCodes`](https://github.com/kudima03/Pure.RelationalSchema.HashCodes/tree/3.3.0) — structural hash codes for relational schema types
- [`Pure.RelationalSchema.Storage`](https://github.com/kudima03/Pure.RelationalSchema.Storage/tree/0.1.0-preview.7.0.0) — in-memory relational data model (`IStoredSchemaDataSet`, `IStoredTableDataSet`, `IRow`, `ICell`)
- [`PureQL.CSharp.Model`](https://github.com/kudima03/PureQL.CSharp.Model/tree/0.1.0-preview.12.0.0) — PureQL query model (`PureQLQuery`, `Query`, expressions per context) — tracks PureQL specification `0.1.0-preview.1.0.0`
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
