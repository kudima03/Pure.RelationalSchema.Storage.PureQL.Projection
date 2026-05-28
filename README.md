# Pure.RelationalSchema.Storage.PureQL.Projection

Executes PureQL queries against in-memory relational schema datasets — translating a `Query` AST into a LINQ-backed `IStoredTableDataSet`.

[![.NET build & test](https://github.com/kudima03/Pure.RelationalSchema.Storage.PureQL.Projection/actions/workflows/build-and-test.yml/badge.svg?branch=main)](https://github.com/kudima03/Pure.RelationalSchema.Storage.PureQL.Projection/actions/workflows/build-and-test.yml)
[![Build and Deploy](https://github.com/kudima03/Pure.RelationalSchema.Storage.PureQL.Projection/actions/workflows/publish-nuget.yml/badge.svg?branch=main)](https://github.com/kudima03/Pure.RelationalSchema.Storage.PureQL.Projection/actions/workflows/publish-nuget.yml)
[![NuGet](https://img.shields.io/nuget/v/Pure.RelationalSchema.Storage.PureQL.Projection)](https://www.nuget.org/packages/Pure.RelationalSchema.Storage.PureQL.Projection)
[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)

## Overview

`Pure.RelationalSchema.Storage.PureQL.Projection` bridges the PureQL query language with the Pure relational storage model. Given a collection of `IStoredSchemaDataSet` objects and a parsed `Query`, it produces a fully enumerable `IStoredTableDataSet` whose rows reflect the query's SELECT, JOIN, WHERE, ORDER BY, GROUP BY/HAVING, and LIMIT/OFFSET clauses.

The translation is entirely in-memory and LINQ-based — no SQL, no external engine.

## Public API

### `PureQLProjection`

The sole public type. Implements `IStoredTableDataSet`, `IAsyncEnumerable<IRow>`, and `IQueryable<IRow>`.

```csharp
public sealed record PureQLProjection : IStoredTableDataSet
```

| Member | Description |
|--------|-------------|
| `PureQLProjection(IEnumerable<IStoredSchemaDataSet> datasets, Query query)` | Builds the projection. Resolves the target table from `query.From`, applies all query clauses, and projects to the selected columns. |
| `ITable TableSchema` | The virtual table schema inferred from the query's SELECT expressions. |
| `IAsyncEnumerator<IRow> GetAsyncEnumerator(...)` | Async enumeration over projected rows (delegates to `System.Linq.Async`). |
| `IEnumerator<IRow> GetEnumerator()` | Synchronous enumeration over projected rows. |
| `Type ElementType / Expression / Provider` | `IQueryable<IRow>` plumbing — allows further LINQ composition. |

### Query clause support

| Clause | Handled by |
|--------|-----------|
| `FROM` | Resolves `schema.table` path into a source `IStoredTableDataSet` |
| `SELECT` | `ColumnsFromQuery` — derives column schema; `RowsFromDatasets` — projects row cells |
| `JOIN` (INNER / LEFT / RIGHT / FULL) | `JoinApplicator` |
| `WHERE` | `WhereExpressionBuilder` — compiles a `BooleanReturning` AST node into a `Func<IRow, bool>` |
| `ORDER BY` | `OrderByApplicator` |
| `GROUP BY` / `HAVING` | `GroupByApplicator` |
| `LIMIT` / `OFFSET` (`Pagination`) | `.Skip().Take()` |

## Dependencies

- [`Pure.Primitives`](https://github.com/kudima03/Pure.Primitives/tree/3.6.2) — core primitive value types (`String`, `Date`, `DateTime`, `Time`, `Guid`, `Number`)
- [`Pure.RelationalSchema`](https://github.com/kudima03/Pure.RelationalSchema/tree/2.0.0) — relational schema abstractions (`ISchema`, `ITable`, `IColumn`, column type hierarchy)
- [`Pure.RelationalSchema.HashCodes`](https://github.com/kudima03/Pure.RelationalSchema.HashCodes/tree/3.3.0) — structural hash codes for relational schema types
- [`Pure.RelationalSchema.Storage`](https://github.com/kudima03/Pure.RelationalSchema.Storage/tree/0.1.0-preview.7.0.0) — in-memory relational data model (`IStoredSchemaDataSet`, `IStoredTableDataSet`, `IRow`, `ICell`)
- [`PureQL.CSharp.Model`](https://github.com/kudima03/PureQL.CSharp.Model/tree/0.1.0-preview.10.0.0) — PureQL query AST (`Query`, `SelectExpression`, `BooleanReturning`, `Join`, `Pagination`, …)
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
// query:    PureQL Query AST built by your query parser

IStoredTableDataSet result = new PureQLProjection(datasets, query);

// Synchronous
foreach (IRow row in result)
{
    ICell cell = row.Cells[myColumn];
    Console.WriteLine(cell.Value.TextValue);
}

// Asynchronous
await foreach (IRow row in result)
{
    // ...
}
```

A `Query` selects columns from a `schema.table` path, with optional joins, filters, ordering, grouping, and pagination:

```csharp
Query query = new Query(
    from: new FromExpression("mySchema.myTable", "mySchema.myTable"),
    select:
    [
        new SelectExpression(
            new ArrayReturning(
                new StringArrayReturning(
                    new StringField("mySchema.myTable", "name")
                )
            )
        ),
    ]
);
```
