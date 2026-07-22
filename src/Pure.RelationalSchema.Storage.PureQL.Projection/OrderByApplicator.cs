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
                    row => CellValueExtractor.GetBoolValue(row, f.Entity, f.Field),
                    descending
                ),
            f =>
                OrderByNullsLast(
                    source,
                    row => CellValueExtractor.GetDateOnlyValue(row, f.Entity, f.Field),
                    descending
                ),
            f =>
                OrderByNullsLast(
                    source,
                    row => CellValueExtractor.GetDateTimeValue(row, f.Entity, f.Field),
                    descending
                ),
            _ =>
                descending
                    ? source.OrderByDescending(_ => (string?)null)
                    : source.OrderBy(_ => (string?)null),
            f =>
                OrderByNullsLast(
                    source,
                    row => CellValueExtractor.GetDoubleValue(row, f.Entity, f.Field),
                    descending
                ),
            f =>
                OrderByNullsLast(
                    source,
                    row => CellValueExtractor.GetTimeOnlyValue(row, f.Entity, f.Field),
                    descending
                ),
            f =>
                OrderByNullsLast(
                    source,
                    row => CellValueExtractor.GetGuidValue(row, f.Entity, f.Field),
                    descending
                ),
            f =>
                OrderByNullsLast(
                    source,
                    row => CellValueExtractor.GetTextValue(row, f.Entity, f.Field),
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
                    row => CellValueExtractor.GetBoolValue(row, f.Entity, f.Field),
                    descending
                ),
            f =>
                ThenByNullsLast(
                    source,
                    row => CellValueExtractor.GetDateOnlyValue(row, f.Entity, f.Field),
                    descending
                ),
            f =>
                ThenByNullsLast(
                    source,
                    row => CellValueExtractor.GetDateTimeValue(row, f.Entity, f.Field),
                    descending
                ),
            _ =>
                descending
                    ? source.ThenByDescending(_ => (string?)null)
                    : source.ThenBy(_ => (string?)null),
            f =>
                ThenByNullsLast(
                    source,
                    row => CellValueExtractor.GetDoubleValue(row, f.Entity, f.Field),
                    descending
                ),
            f =>
                ThenByNullsLast(
                    source,
                    row => CellValueExtractor.GetTimeOnlyValue(row, f.Entity, f.Field),
                    descending
                ),
            f =>
                ThenByNullsLast(
                    source,
                    row => CellValueExtractor.GetGuidValue(row, f.Entity, f.Field),
                    descending
                ),
            f =>
                ThenByNullsLast(
                    source,
                    row => CellValueExtractor.GetTextValue(row, f.Entity, f.Field),
                    descending
                )
        );
    }
}
