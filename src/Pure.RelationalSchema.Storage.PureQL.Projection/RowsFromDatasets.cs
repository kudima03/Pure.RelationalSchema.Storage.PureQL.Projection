using System.Collections;
using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

internal sealed record RowsFromDatasets : IQueryable<IRow>
{
    private readonly IQueryable<IRow> _queryable;

    public RowsFromDatasets(IEnumerable<IStoredSchemaDataSet> datasets, Query query)
    {
        _queryable = Build(datasets, query);
    }

    public Type ElementType => _queryable.ElementType;

    public Expression Expression => _queryable.Expression;

    public IQueryProvider Provider => _queryable.Provider;

    public IEnumerator<IRow> GetEnumerator()
    {
        return _queryable.GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return _queryable.GetEnumerator();
    }

    private static IQueryable<IRow> Build(
        IEnumerable<IStoredSchemaDataSet> datasets,
        Query query
    )
    {
        EntityReferenceValidator.Validate(query);

        List<IStoredSchemaDataSet> datasetList = [.. datasets];

        IEnumerable<string> reversedPath = query
            .From.Entity.Split(".")
            .AsEnumerable()
            .Reverse();

        string tableName = reversedPath.First();
        string schemaName = reversedPath.Skip(1).First();

        IStoredTableDataSet targetTableDataset = datasetList
            .Where(x => x.Schema.Name.TextValue == schemaName)
            .SelectMany(x => x)
            .First(x => x.Key.Name.TextValue == tableName)
            .Value;

        IQueryable<IRow> queryable = targetTableDataset;

        if (query.Join is not null)
        {
            IReadOnlyList<IColumn> columns =
            [
                .. targetTableDataset.TableSchema.Columns,
            ];

            foreach (Join join in query.Join)
            {
                JoinedRows joined = JoinApplicator.Apply(
                    queryable,
                    columns,
                    datasetList,
                    join
                );

                queryable = joined.Rows;
                columns = joined.Columns;
            }
        }

        if (query.Where is not null)
        {
            queryable = queryable.Where(
                WhereExpressionBuilder.BuildPredicate(query.Where.Value)
            );
        }

        if (query.OrderBy is not null)
        {
            queryable = OrderByApplicator.Apply(queryable, query.OrderBy);
        }

        // HAVING with no groupBy still filters the implicit whole-set group,
        // so it engages group mode on its own.
        bool groupMode =
            query.GroupBy is not null
            || query.Having is not null
            || query.SelectExpressions.Any(expression =>
                expression.TryPickT0(out SingleValueReturning singleValue, out _)
                && AggregateEvaluator.IsAggregate(singleValue)
            );

        queryable = groupMode
            ? GroupByApplicator.Apply(
                queryable,
                query.GroupBy,
                query.Having,
                query.SelectExpressions
            )
            : ApplyRowProjection(queryable, query.SelectExpressions);

        if (query.Distinct)
        {
            queryable = DistinctApplicator.Apply(queryable);
        }

        if (query.Pagination is not null)
        {
            // Skip/take are Int64 on the wire; clamp instead of letting the
            // narrowing cast wrap into negative (no-op / empty) LINQ calls.
            queryable = queryable.Skip(
                (int)Math.Clamp(query.Pagination.Skip, 0, int.MaxValue)
            );
            queryable = queryable.Take(
                (int)Math.Clamp(query.Pagination.Take, 0, int.MaxValue)
            );
        }

        return queryable;
    }

    private static IQueryable<IRow> ApplyRowProjection(
        IQueryable<IRow> source,
        IEnumerable<SelectExpression> selectExpressions
    )
    {
        List<ProjectionItem> plan = [.. selectExpressions.Select(ProjectionItemOf)];

        return source.Select(row =>
            (IRow)
                new Row(
                    new Collections.Generic.Dictionary<ProjectionItem, IColumn, ICell>(
                        plan,
                        item => item.Column,
                        item => item.Cell(row),
                        column => new ColumnHash(column)
                    )
                )
        );
    }

    private static ProjectionItem ProjectionItemOf(SelectExpression expression)
    {
        IColumn column = SelectColumns.OutputColumn(expression);

        if (
            expression.TryPickT0(
                out SingleValueReturning singleValue,
                out ArrayReturning array
            )
        )
        {
            if (!ScalarCell.IsScalar(singleValue))
            {
                throw new NotSupportedException(
                    "Only scalar SingleValueReturning expressions can be "
                        + "projected per row; parameters and single-value "
                        + "composites are not supported."
                );
            }

            ICell cell = ScalarCell.From(singleValue);

            return new ProjectionItem(column, _ => cell);
        }

        string fieldEntity = SelectColumns.FieldEntity(array);
        string fieldName = SelectColumns.FieldName(array);

        return new ProjectionItem(
            column,
            row => CellValueExtractor.GetRequiredCell(row, fieldEntity, fieldName)
        );
    }

    private sealed record ProjectionItem(
        IColumn Column,
        Func<IRow, ICell> Cell
    );
}
