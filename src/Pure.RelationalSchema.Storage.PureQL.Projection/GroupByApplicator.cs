using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

internal static class GroupByApplicator
{
    internal static IQueryable<IRow> Apply(
        IQueryable<IRow> source,
        IEnumerable<Field> groupBy,
        BooleanReturning? having
    )
    {
        List<Field> groupByList = [.. groupBy];

        Func<IRow, bool>? havingCondition = having is not null
            ? WhereExpressionBuilder.Build(having).Compile()
            : null;

        IEnumerable<IRow> grouped = source
            .AsEnumerable()
            .GroupBy(row => BuildGroupKey(row, groupByList))
            .Where(group => havingCondition is null || havingCondition(group.First()))
            .Select(group => group.First());

        return grouped.AsQueryable();
    }

    private static string BuildGroupKey(IRow row, List<Field> fields)
    {
        return string.Join(
            "\0",
            fields.Select(field =>
                field.Match(
                    f =>
                        CellValueExtractor.GetBoolValue(row, f.Field)?.ToString()
                        ?? string.Empty,
                    f =>
                        CellValueExtractor.GetDateOnlyValue(row, f.Field)?.ToString()
                        ?? string.Empty,
                    f =>
                        CellValueExtractor.GetDateTimeValue(row, f.Field)?.ToString()
                        ?? string.Empty,
                    _ => string.Empty,
                    f =>
                        CellValueExtractor.GetDoubleValue(row, f.Field)?.ToString()
                        ?? string.Empty,
                    f =>
                        CellValueExtractor.GetTimeOnlyValue(row, f.Field)?.ToString()
                        ?? string.Empty,
                    f =>
                        CellValueExtractor.GetGuidValue(row, f.Field)?.ToString()
                        ?? string.Empty,
                    f => CellValueExtractor.GetTextValue(row, f.Field) ?? string.Empty
                )
            )
        );
    }
}
