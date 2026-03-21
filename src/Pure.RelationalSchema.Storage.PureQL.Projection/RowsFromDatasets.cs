using System.Collections;
using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model;
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
        IEnumerable<string> reversedPath = query
            .From.Entity.Split(".")
            .AsEnumerable()
            .Reverse();

        string tableName = reversedPath.First();
        string schemaName = reversedPath.Skip(1).First();

        IStoredTableDataSet targetTableDataset = datasets
            .First(x => x.Schema.Name.TextValue == schemaName)
            .First(x => x.Key.Name.TextValue == tableName)
            .Value;

        IQueryable<IRow> queryable = targetTableDataset;

        if (query.Where is not null)
        {
            queryable = queryable.Where(WhereExpressionBuilder.Build(query.Where));
        }

        if (query.OrderBy is not null)
        {
            queryable = OrderByApplicator.Apply(queryable, query.OrderBy);
        }

        if (query.Pagination is not null)
        {
            queryable = queryable.Skip((int)query.Pagination.Skip);
            queryable = queryable.Take((int)query.Pagination.Take);
        }

        IEnumerable<IColumn> columns = query
            .SelectExpressions.Select(x => x.AsT1.AsT4.AsT1)
            .Select(x => new Column.Column(new String(x.Field), new StringColumnType()))
            .ToList();

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
}
