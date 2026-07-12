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

    private static IOrderedQueryable<IRow> ApplyFirstOrderBy(
        IQueryable<IRow> source,
        OrderByItem item
    )
    {
        bool descending = item.Direction == SortDirection.Desc;

        return item.Field.Match(
            f =>
                descending
                    ? source.OrderByDescending(row =>
                        CellValueExtractor.GetBoolValue(row, f.Entity, f.Field)
                    )
                    : source.OrderBy(row =>
                        CellValueExtractor.GetBoolValue(row, f.Entity, f.Field)
                    ),
            f =>
                descending
                    ? source.OrderByDescending(row =>
                        CellValueExtractor.GetDateOnlyValue(row, f.Entity, f.Field)
                    )
                    : source.OrderBy(row =>
                        CellValueExtractor.GetDateOnlyValue(row, f.Entity, f.Field)
                    ),
            f =>
                descending
                    ? source.OrderByDescending(row =>
                        CellValueExtractor.GetDateTimeValue(row, f.Entity, f.Field)
                    )
                    : source.OrderBy(row =>
                        CellValueExtractor.GetDateTimeValue(row, f.Entity, f.Field)
                    ),
            _ =>
                descending
                    ? source.OrderByDescending(_ => (string?)null)
                    : source.OrderBy(_ => (string?)null),
            f =>
                descending
                    ? source.OrderByDescending(row =>
                        CellValueExtractor.GetDoubleValue(row, f.Entity, f.Field)
                    )
                    : source.OrderBy(row =>
                        CellValueExtractor.GetDoubleValue(row, f.Entity, f.Field)
                    ),
            f =>
                descending
                    ? source.OrderByDescending(row =>
                        CellValueExtractor.GetTimeOnlyValue(row, f.Entity, f.Field)
                    )
                    : source.OrderBy(row =>
                        CellValueExtractor.GetTimeOnlyValue(row, f.Entity, f.Field)
                    ),
            f =>
                descending
                    ? source.OrderByDescending(row =>
                        CellValueExtractor.GetGuidValue(row, f.Entity, f.Field)
                    )
                    : source.OrderBy(row =>
                        CellValueExtractor.GetGuidValue(row, f.Entity, f.Field)
                    ),
            f =>
                descending
                    ? source.OrderByDescending(row =>
                        CellValueExtractor.GetTextValue(row, f.Entity, f.Field)
                    )
                    : source.OrderBy(row =>
                        CellValueExtractor.GetTextValue(row, f.Entity, f.Field)
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
                descending
                    ? source.ThenByDescending(row =>
                        CellValueExtractor.GetBoolValue(row, f.Entity, f.Field)
                    )
                    : source.ThenBy(row =>
                        CellValueExtractor.GetBoolValue(row, f.Entity, f.Field)
                    ),
            f =>
                descending
                    ? source.ThenByDescending(row =>
                        CellValueExtractor.GetDateOnlyValue(row, f.Entity, f.Field)
                    )
                    : source.ThenBy(row =>
                        CellValueExtractor.GetDateOnlyValue(row, f.Entity, f.Field)
                    ),
            f =>
                descending
                    ? source.ThenByDescending(row =>
                        CellValueExtractor.GetDateTimeValue(row, f.Entity, f.Field)
                    )
                    : source.ThenBy(row =>
                        CellValueExtractor.GetDateTimeValue(row, f.Entity, f.Field)
                    ),
            _ =>
                descending
                    ? source.ThenByDescending(_ => (string?)null)
                    : source.ThenBy(_ => (string?)null),
            f =>
                descending
                    ? source.ThenByDescending(row =>
                        CellValueExtractor.GetDoubleValue(row, f.Entity, f.Field)
                    )
                    : source.ThenBy(row =>
                        CellValueExtractor.GetDoubleValue(row, f.Entity, f.Field)
                    ),
            f =>
                descending
                    ? source.ThenByDescending(row =>
                        CellValueExtractor.GetTimeOnlyValue(row, f.Entity, f.Field)
                    )
                    : source.ThenBy(row =>
                        CellValueExtractor.GetTimeOnlyValue(row, f.Entity, f.Field)
                    ),
            f =>
                descending
                    ? source.ThenByDescending(row =>
                        CellValueExtractor.GetGuidValue(row, f.Entity, f.Field)
                    )
                    : source.ThenBy(row =>
                        CellValueExtractor.GetGuidValue(row, f.Entity, f.Field)
                    ),
            f =>
                descending
                    ? source.ThenByDescending(row =>
                        CellValueExtractor.GetTextValue(row, f.Entity, f.Field)
                    )
                    : source.ThenBy(row =>
                        CellValueExtractor.GetTextValue(row, f.Entity, f.Field)
                    )
        );
    }
}
