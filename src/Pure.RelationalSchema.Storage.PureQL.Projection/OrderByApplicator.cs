using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

internal static class OrderByApplicator
{
    internal static IQueryable<IRow> Apply(
        IQueryable<IRow> source,
        IEnumerable<Field> fields
    )
    {
        IOrderedQueryable<IRow>? ordered = null;

        foreach (Field field in fields)
        {
            ordered = ordered is null
                ? ApplyFirstOrderBy(source, field)
                : ApplyThenBy(ordered, field);
        }

        return ordered ?? source;
    }

    private static IOrderedQueryable<IRow> ApplyFirstOrderBy(
        IQueryable<IRow> source,
        Field field
    )
    {
        return field.Match(
            f => source.OrderBy(row => CellValueExtractor.GetBoolValue(row, f.Field)),
            f => source.OrderBy(row => CellValueExtractor.GetDateOnlyValue(row, f.Field)),
            f => source.OrderBy(row => CellValueExtractor.GetDateTimeValue(row, f.Field)),
            f => source.OrderBy(row => CellValueExtractor.GetDoubleValue(row, f.Field)),
            f => source.OrderBy(row => CellValueExtractor.GetTimeOnlyValue(row, f.Field)),
            f => source.OrderBy(row => CellValueExtractor.GetGuidValue(row, f.Field)),
            f => source.OrderBy(row => CellValueExtractor.GetTextValue(row, f.Field))
        );
    }

    private static IOrderedQueryable<IRow> ApplyThenBy(
        IOrderedQueryable<IRow> source,
        Field field
    )
    {
        return field.Match(
            f => source.ThenBy(row => CellValueExtractor.GetBoolValue(row, f.Field)),
            f => source.ThenBy(row => CellValueExtractor.GetDateOnlyValue(row, f.Field)),
            f => source.ThenBy(row => CellValueExtractor.GetDateTimeValue(row, f.Field)),
            f => source.ThenBy(row => CellValueExtractor.GetDoubleValue(row, f.Field)),
            f => source.ThenBy(row => CellValueExtractor.GetTimeOnlyValue(row, f.Field)),
            f => source.ThenBy(row => CellValueExtractor.GetGuidValue(row, f.Field)),
            f => source.ThenBy(row => CellValueExtractor.GetTextValue(row, f.Field))
        );
    }
}
