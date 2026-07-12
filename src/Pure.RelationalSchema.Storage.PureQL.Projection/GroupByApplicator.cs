using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Groups the row set, filters groups with HAVING (evaluated per group, so
// aggregates fold each group's rows) and emits one projected row per group:
// aggregate select expressions fold the group, field select expressions take
// the group's representative (key) value. With no groupBy fields the whole
// row set forms a single group (whole-set aggregates).
internal static class GroupByApplicator
{
    internal static IQueryable<IRow> Apply(
        IQueryable<IRow> source,
        IEnumerable<Field>? groupBy,
        BooleanReturning? having,
        IEnumerable<SelectExpression> selectExpressions
    )
    {
        List<Field> groupByList = groupBy is null ? [] : [.. groupBy];

        Func<IReadOnlyList<IRow>, bool>? havingCondition = having is not null
            ? AggregateEvaluator.BuildCondition(having)
            : null;

        List<ProjectionItem> plan = [.. selectExpressions.Select(ProjectionItemOf)];

        IEnumerable<IReadOnlyList<IRow>> groups = groupByList.Count == 0
            ? WholeSetGroup(source)
            : source
                .AsEnumerable()
                .GroupBy(row => BuildGroupKey(row, groupByList))
                .Select(group => (IReadOnlyList<IRow>)[.. group]);

        IEnumerable<IRow> rows = groups
            .Where(group =>
                havingCondition is null || havingCondition(group)
            )
            .Select(group => ProjectGroup(group, plan));

        return rows.AsQueryable();
    }

    private sealed record ProjectionItem(
        IColumn Column,
        Func<IReadOnlyList<IRow>, ICell> Cell
    );

    private static ProjectionItem ProjectionItemOf(SelectExpression expression)
    {
        IColumn column = SelectColumns.OutputColumn(expression);

        if (
            expression.TryPickT0(
                out SingleValueReturning singleValue,
                out ArrayReturning arrayReturning
            )
        )
        {
            if (!AggregateEvaluator.IsAggregate(singleValue))
            {
                throw new NotSupportedException(
                    "SingleValueReturning (scalar/parameter) cannot be projected "
                        + "as a column field."
                );
            }

            Func<IReadOnlyList<IRow>, string?> text =
                AggregateEvaluator.BuildText(singleValue);

            return new ProjectionItem(
                column,
                rows => new Cell(new String(text(rows) ?? string.Empty))
            );
        }

        string fieldEntity = SelectColumns.FieldEntity(arrayReturning);
        string fieldName = SelectColumns.FieldName(arrayReturning);

        return new ProjectionItem(
            column,
            rows => CellValueExtractor.GetRequiredCell(rows[0], fieldEntity, fieldName)
        );
    }

    private static IEnumerable<IReadOnlyList<IRow>> WholeSetGroup(
        IQueryable<IRow> source
    )
    {
        List<IRow> rows = [.. source];
        yield return rows;
    }

    private static IRow ProjectGroup(
        IReadOnlyList<IRow> group,
        List<ProjectionItem> plan
    )
    {
        return new Row(
            new Collections.Generic.Dictionary<ProjectionItem, IColumn, ICell>(
                plan,
                item => item.Column,
                item => item.Cell(group),
                column => new ColumnHash(column)
            )
        );
    }

    private static string BuildGroupKey(IRow row, List<Field> fields)
    {
        return string.Join(
            "\0",
            fields.Select(field =>
                field.Match(
                    f =>
                        CellValueExtractor
                            .GetBoolValue(row, f.Entity, f.Field)
                            ?.ToString() ?? string.Empty,
                    f =>
                        CellValueExtractor
                            .GetDateOnlyValue(row, f.Entity, f.Field)
                            ?.ToString() ?? string.Empty,
                    f =>
                        CellValueExtractor
                            .GetDateTimeValue(row, f.Entity, f.Field)
                            ?.ToString() ?? string.Empty,
                    _ => string.Empty,
                    f =>
                        CellValueExtractor
                            .GetDoubleValue(row, f.Entity, f.Field)
                            ?.ToString() ?? string.Empty,
                    f =>
                        CellValueExtractor
                            .GetTimeOnlyValue(row, f.Entity, f.Field)
                            ?.ToString() ?? string.Empty,
                    f =>
                        CellValueExtractor
                            .GetGuidValue(row, f.Entity, f.Field)
                            ?.ToString() ?? string.Empty,
                    f =>
                        CellValueExtractor.GetTextValue(row, f.Entity, f.Field)
                        ?? string.Empty
                )
            )
        );
    }
}
