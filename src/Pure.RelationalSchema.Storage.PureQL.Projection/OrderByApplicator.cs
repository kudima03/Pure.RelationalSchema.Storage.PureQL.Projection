using System.Linq.Expressions;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

internal static class OrderByApplicator
{
    internal static IQueryable<IRow> Apply(
        IQueryable<IRow> source,
        IEnumerable<OrderByItem> items
    )
    {
        IOrderedQueryable<IRow>? ordered = null;

        foreach (OrderByItem item in items)
        {
            ordered = ordered is null
                ? ApplyFirstOrderBy(source, item)
                : ApplyThenBy(ordered, item);
        }

        return ordered ?? source;
    }

    // CellValueExtractor.GetRequiredCell throws a bare KeyNotFoundException
    // naming only the field when no column matches. For orderBy specifically
    // that's a frequent trial-and-error trap, because which name is expected
    // depends on query mode (RowsFromDatasets.Build): without groupBy, the
    // sort runs on raw pre-projection rows and needs the source column name;
    // with groupBy/aggregates, it runs on the projected, post-alias rows and
    // needs the select alias instead. Re-throwing with that rule spelled out
    // turns a silent trap into an actionable message, without changing which
    // name resolves in which mode.
    private static TValue? ResolveOrThrow<TValue>(
        Func<TValue?> resolve,
        string entity,
        string fieldName
    )
        where TValue : struct
    {
        try
        {
            return resolve();
        }
        catch (KeyNotFoundException ex)
        {
            throw OrderByFieldNotFound(entity, fieldName, ex);
        }
    }

    private static string? ResolveOrThrow(
        Func<string?> resolve,
        string entity,
        string fieldName
    )
    {
        try
        {
            return resolve();
        }
        catch (KeyNotFoundException ex)
        {
            throw OrderByFieldNotFound(entity, fieldName, ex);
        }
    }

    private static KeyNotFoundException OrderByFieldNotFound(
        string entity,
        string fieldName,
        Exception inner
    )
    {
        return new KeyNotFoundException(
            $"orderBy field '{fieldName}' on entity '{entity}' has no "
                + "matching column in the rows being sorted. Without "
                + "groupBy, orderBy fields must name the original source "
                + "column (as it appears before projection); with groupBy "
                + "or aggregates, they must name the select alias of the "
                + "projected column instead.",
            inner
        );
    }

    private static IOrderedQueryable<IRow> OrderByNullsLast<TValue>(
        IQueryable<IRow> source,
        Expression<Func<IRow, TValue?>> selector,
        bool descending
    )
        where TValue : struct
    {
        Func<IRow, TValue?> compiled = selector.Compile();

        IOrderedQueryable<IRow> byNullRank = source.OrderBy(row =>
            compiled(row).HasValue ? 0 : 1
        );

        return descending
            ? byNullRank.ThenByDescending(selector)
            : byNullRank.ThenBy(selector);
    }

    private static IOrderedQueryable<IRow> OrderByNullsLast(
        IQueryable<IRow> source,
        Expression<Func<IRow, string?>> selector,
        bool descending
    )
    {
        Func<IRow, string?> compiled = selector.Compile();

        IOrderedQueryable<IRow> byNullRank = source.OrderBy(row =>
            compiled(row) == null ? 1 : 0
        );

        return descending
            ? byNullRank.ThenByDescending(selector)
            : byNullRank.ThenBy(selector);
    }

    private static IOrderedQueryable<IRow> ThenByNullsLast<TValue>(
        IOrderedQueryable<IRow> source,
        Expression<Func<IRow, TValue?>> selector,
        bool descending
    )
        where TValue : struct
    {
        Func<IRow, TValue?> compiled = selector.Compile();

        IOrderedQueryable<IRow> byNullRank = source.ThenBy(row =>
            compiled(row).HasValue ? 0 : 1
        );

        return descending
            ? byNullRank.ThenByDescending(selector)
            : byNullRank.ThenBy(selector);
    }

    private static IOrderedQueryable<IRow> ThenByNullsLast(
        IOrderedQueryable<IRow> source,
        Expression<Func<IRow, string?>> selector,
        bool descending
    )
    {
        Func<IRow, string?> compiled = selector.Compile();

        IOrderedQueryable<IRow> byNullRank = source.ThenBy(row =>
            compiled(row) == null ? 1 : 0
        );

        return descending
            ? byNullRank.ThenByDescending(selector)
            : byNullRank.ThenBy(selector);
    }

    private static IOrderedQueryable<IRow> ApplyFirstOrderBy(
        IQueryable<IRow> source,
        OrderByItem item
    )
    {
        bool descending = item.Direction == SortDirection.Desc;

        return item.Field.Match(
            f =>
                OrderByNullsLast(
                    source,
                    row =>
                        ResolveOrThrow(
                            () => CellValueExtractor.GetBoolValue(row, f.Entity, f.Field),
                            f.Entity,
                            f.Field
                        ),
                    descending
                ),
            f =>
                OrderByNullsLast(
                    source,
                    row =>
                        ResolveOrThrow(
                            () => CellValueExtractor.GetDateOnlyValue(row, f.Entity, f.Field),
                            f.Entity,
                            f.Field
                        ),
                    descending
                ),
            f =>
                OrderByNullsLast(
                    source,
                    row =>
                        ResolveOrThrow(
                            () => CellValueExtractor.GetDateTimeValue(row, f.Entity, f.Field),
                            f.Entity,
                            f.Field
                        ),
                    descending
                ),
            _ =>
                descending
                    ? source.OrderByDescending(_ => (string?)null)
                    : source.OrderBy(_ => (string?)null),
            f =>
                OrderByNullsLast(
                    source,
                    row =>
                        ResolveOrThrow(
                            () => CellValueExtractor.GetDoubleValue(row, f.Entity, f.Field),
                            f.Entity,
                            f.Field
                        ),
                    descending
                ),
            f =>
                OrderByNullsLast(
                    source,
                    row =>
                        ResolveOrThrow(
                            () => CellValueExtractor.GetTimeOnlyValue(row, f.Entity, f.Field),
                            f.Entity,
                            f.Field
                        ),
                    descending
                ),
            f =>
                OrderByNullsLast(
                    source,
                    row =>
                        ResolveOrThrow(
                            () => CellValueExtractor.GetGuidValue(row, f.Entity, f.Field),
                            f.Entity,
                            f.Field
                        ),
                    descending
                ),
            f =>
                OrderByNullsLast(
                    source,
                    row =>
                        ResolveOrThrow(
                            () => CellValueExtractor.GetTextValue(row, f.Entity, f.Field),
                            f.Entity,
                            f.Field
                        ),
                    descending
                )
        );
    }

    private static IOrderedQueryable<IRow> ApplyThenBy(
        IOrderedQueryable<IRow> source,
        OrderByItem item
    )
    {
        bool descending = item.Direction == SortDirection.Desc;

        return item.Field.Match(
            f =>
                ThenByNullsLast(
                    source,
                    row =>
                        ResolveOrThrow(
                            () => CellValueExtractor.GetBoolValue(row, f.Entity, f.Field),
                            f.Entity,
                            f.Field
                        ),
                    descending
                ),
            f =>
                ThenByNullsLast(
                    source,
                    row =>
                        ResolveOrThrow(
                            () => CellValueExtractor.GetDateOnlyValue(row, f.Entity, f.Field),
                            f.Entity,
                            f.Field
                        ),
                    descending
                ),
            f =>
                ThenByNullsLast(
                    source,
                    row =>
                        ResolveOrThrow(
                            () => CellValueExtractor.GetDateTimeValue(row, f.Entity, f.Field),
                            f.Entity,
                            f.Field
                        ),
                    descending
                ),
            _ =>
                descending
                    ? source.ThenByDescending(_ => (string?)null)
                    : source.ThenBy(_ => (string?)null),
            f =>
                ThenByNullsLast(
                    source,
                    row =>
                        ResolveOrThrow(
                            () => CellValueExtractor.GetDoubleValue(row, f.Entity, f.Field),
                            f.Entity,
                            f.Field
                        ),
                    descending
                ),
            f =>
                ThenByNullsLast(
                    source,
                    row =>
                        ResolveOrThrow(
                            () => CellValueExtractor.GetTimeOnlyValue(row, f.Entity, f.Field),
                            f.Entity,
                            f.Field
                        ),
                    descending
                ),
            f =>
                ThenByNullsLast(
                    source,
                    row =>
                        ResolveOrThrow(
                            () => CellValueExtractor.GetGuidValue(row, f.Entity, f.Field),
                            f.Entity,
                            f.Field
                        ),
                    descending
                ),
            f =>
                ThenByNullsLast(
                    source,
                    row =>
                        ResolveOrThrow(
                            () => CellValueExtractor.GetTextValue(row, f.Entity, f.Field),
                            f.Entity,
                            f.Field
                        ),
                    descending
                )
        );
    }
}
