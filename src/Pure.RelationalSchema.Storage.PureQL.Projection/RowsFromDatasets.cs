using System.Collections;
using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Returnings;
using String = Pure.Primitives.String.String;

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
        List<IStoredSchemaDataSet> datasetList = [.. datasets];

        IEnumerable<string> reversedPath = query
            .From.Entity.Split(".")
            .AsEnumerable()
            .Reverse();

        string tableName = reversedPath.First();
        string schemaName = reversedPath.Skip(1).First();

        IStoredTableDataSet targetTableDataset = datasetList
            .First(x => x.Schema.Name.TextValue == schemaName)
            .First(x => x.Key.Name.TextValue == tableName)
            .Value;

        IQueryable<IRow> queryable = targetTableDataset;

        if (query.Join is not null)
        {
            foreach (Join join in query.Join)
            {
                queryable = JoinApplicator.Apply(queryable, datasetList, join);
            }
        }

        if (query.Where is not null)
        {
            queryable = queryable.Where(WhereExpressionBuilder.Build(query.Where));
        }

        if (query.OrderBy is not null)
        {
            queryable = OrderByApplicator.Apply(queryable, query.OrderBy);
        }

        if (query.GroupBy is not null)
        {
            queryable = GroupByApplicator.Apply(queryable, query.GroupBy, query.Having);
        }

        if (query.Pagination is not null)
        {
            queryable = queryable.Skip((int)query.Pagination.Skip);
            queryable = queryable.Take((int)query.Pagination.Take);
        }

        IEnumerable<IColumn> columns = query
            .SelectExpressions.Select(ColumnFromSelectExpression);

        return queryable.Select(row =>
            (IRow)
                new Row(
                    new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                        columns,
                        c => c,
                        c => row.Cells[c],
                        c => new ColumnHash(c)
                    )
                )
        );
    }

    private static IColumn ColumnFromSelectExpression(SelectExpression expression)
    {
        return expression.TryPickT0(out SingleValueReturning singleValue, out _)
            ? ColumnFromSingleValueReturning(singleValue)
            : expression.TryPickT1(out ArrayReturning arrayReturning, out _)
                ? ColumnFromArrayReturning(arrayReturning)
                : throw new NotSupportedException();
    }

    private static IColumn ColumnFromSingleValueReturning(SingleValueReturning _)
    {
        throw new NotSupportedException(
            "SingleValueReturning (scalar/parameter) cannot be projected as a column field."
        );
    }

    private static IColumn ColumnFromArrayReturning(ArrayReturning returning)
    {
        return returning.Match(
            b => new Column.Column(new String(b.AsT1.Field), new BoolColumnType()),
            d => new Column.Column(new String(d.AsT1.Field), new DateColumnType()),
            dt => new Column.Column(new String(dt.AsT1.Field), new DateTimeColumnType()),
            n => new Column.Column(new String(n.AsT1.Field), new DoubleColumnType()),
            s => new Column.Column(new String(s.AsT1.Field), new StringColumnType()),
            t => new Column.Column(new String(t.AsT1.Field), new TimeColumnType()),
            u => new Column.Column(new String(u.AsT1.Field), new UuidColumnType())
        );
    }
}
