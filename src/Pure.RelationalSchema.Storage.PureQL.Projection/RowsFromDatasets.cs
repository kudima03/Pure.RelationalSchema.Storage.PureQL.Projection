using System.Collections;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

internal sealed record RowsFromDatasets : IEnumerable<IRow>
{
    private readonly Query _query;

    private readonly IEnumerable<IStoredSchemaDataSet> _datasets;

    public RowsFromDatasets(IEnumerable<IStoredSchemaDataSet> datasets, Query query)
    {
        _query = query;
        _datasets = datasets;
    }

    public IEnumerator<IRow> GetEnumerator()
    {
        IEnumerable<string> reversedPath = _query
            .From.Entity.Split(".")
            .AsEnumerable()
            .Reverse();

        string tableName = reversedPath.First();

        string schemaName = reversedPath.Skip(1).First();

        IStoredSchemaDataSet targetSchemaDataset = _datasets.First(x =>
            x.Schema.Name.TextValue == schemaName
        );

        IStoredTableDataSet targetTableDataset = targetSchemaDataset
            .First(x => x.Key.Name.TextValue == tableName)
            .Value;

        IEnumerable<IColumn> columns = _query
            .SelectExpressions.Select(x => x.AsT1.AsT4.AsT1)
            .Select(x => new Column.Column(new String(x.Field), new StringColumnType()));

        return targetTableDataset
            .AsEnumerable()
            .Select(x => new Row(
                new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                    columns,
                    c => c,
                    c => x.Cells[c],
                    c => new ColumnHash(c)
                )
            ))
            .GetEnumerator();
    }

    IEnumerator IEnumerable.GetEnumerator()
    {
        return GetEnumerator();
    }
}
