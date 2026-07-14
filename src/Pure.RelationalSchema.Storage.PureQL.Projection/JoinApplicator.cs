using System.Runtime.CompilerServices;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Applies one join: resolves the right-hand table by its "schema.table"
// path, tags its columns with the join entity (QualifiedColumn) so
// same-named columns from both sides stay distinct and addressable, merges
// matched rows keeping every cell, and pads unmatched outer-join rows with
// empty cells for the missing side so projection always sees every column.
internal static class JoinApplicator
{
    private static readonly ICell EmptyCell = new Cell(new String(string.Empty));

    internal static JoinedRows Apply(
        IQueryable<IRow> left,
        IReadOnlyList<IColumn> leftColumns,
        List<IStoredSchemaDataSet> datasets,
        Join join
    )
    {
        IEnumerable<string> reversedPath = join
            .Entity.Split(".")
            .AsEnumerable()
            .Reverse();

        string tableName = reversedPath.First();
        string schemaName = reversedPath.Skip(1).First();

        KeyValuePair<ITable, IStoredTableDataSet> rightDataset = datasets
            .Where(x => x.Schema.Name.TextValue == schemaName)
            .SelectMany(x => x)
            .First(x => x.Key.Name.TextValue == tableName);

        List<IColumn> rightColumns =
        [
            .. rightDataset.Key.Columns.Select(column =>
                (IColumn)new QualifiedColumn(join.Entity, column)
            ),
        ];

        Func<IRow, bool> onCondition = WhereExpressionBuilder
            .BuildPredicate(join.On)
            .Compile();

        List<IRow> leftList = [.. left];
        List<IRow> rightList =
        [
            .. rightDataset
                .Value.AsEnumerable()
                .Select(row => Qualify(join.Entity, row)),
        ];

        IEnumerable<IRow> result = join.Type switch
        {
            JoinType.Inner => InnerJoin(leftList, rightList, onCondition),
            JoinType.Left => LeftJoin(leftList, rightList, onCondition, rightColumns),
            JoinType.Right => RightJoin(leftList, rightList, onCondition, leftColumns),
            JoinType.Full => FullJoin(
                leftList,
                rightList,
                onCondition,
                leftColumns,
                rightColumns
            ),
            _ => throw new NotSupportedException(
                $"JoinType {join.Type} is not supported."
            ),
        };

        return new JoinedRows(
            result.AsQueryable(),
            [.. leftColumns, .. rightColumns]
        );
    }

    private static IEnumerable<IRow> InnerJoin(
        List<IRow> left,
        List<IRow> right,
        Func<IRow, bool> onCondition
    )
    {
        return left.SelectMany(l => right.Select(r => MergeRows(l, r)))
            .Where(onCondition);
    }

    private static IEnumerable<IRow> LeftJoin(
        List<IRow> left,
        List<IRow> right,
        Func<IRow, bool> onCondition,
        IReadOnlyList<IColumn> rightColumns
    )
    {
        return left.SelectMany(l =>
        {
            List<IRow> matched = [.. right
                .Select(r => MergeRows(l, r))
                .Where(onCondition)];

            return matched.Count > 0 ? matched : [Pad(l, rightColumns)];
        });
    }

    private static IEnumerable<IRow> RightJoin(
        List<IRow> left,
        List<IRow> right,
        Func<IRow, bool> onCondition,
        IReadOnlyList<IColumn> leftColumns
    )
    {
        return right.SelectMany(r =>
        {
            List<IRow> matched = [.. left.Select(l => MergeRows(l, r)).Where(onCondition)];

            return matched.Count > 0 ? matched : [Pad(r, leftColumns)];
        });
    }

    private static IEnumerable<IRow> FullJoin(
        List<IRow> left,
        List<IRow> right,
        Func<IRow, bool> onCondition,
        IReadOnlyList<IColumn> leftColumns,
        IReadOnlyList<IColumn> rightColumns
    )
    {
        HashSet<int> matchedRightIndexes = [];

        List<IRow> result = [];

        foreach (IRow l in left)
        {
            List<IRow> matched = [];

            for (int i = 0; i < right.Count; i++)
            {
                IRow merged = MergeRows(l, right[i]);

                if (onCondition(merged))
                {
                    matched.Add(merged);
                    _ = matchedRightIndexes.Add(i);
                }
            }

            if (matched.Count > 0)
            {
                result.AddRange(matched);
            }
            else
            {
                result.Add(Pad(l, rightColumns));
            }
        }

        for (int i = 0; i < right.Count; i++)
        {
            if (!matchedRightIndexes.Contains(i))
            {
                result.Add(Pad(right[i], leftColumns));
            }
        }

        return result;
    }

    private static IRow Qualify(string entity, IRow row)
    {
        Dictionary<IColumn, ICell> cells = new(ReferenceColumnComparer.Instance);

        foreach (KeyValuePair<IColumn, ICell> cell in row.Cells)
        {
            cells[new QualifiedColumn(entity, cell.Key)] = cell.Value;
        }

        return new Row(cells);
    }

    private static IRow MergeRows(IRow leftRow, IRow rightRow)
    {
        Dictionary<IColumn, ICell> cells = new(ReferenceColumnComparer.Instance);

        foreach (KeyValuePair<IColumn, ICell> cell in leftRow.Cells)
        {
            cells[cell.Key] = cell.Value;
        }

        foreach (KeyValuePair<IColumn, ICell> cell in rightRow.Cells)
        {
            cells[cell.Key] = cell.Value;
        }

        return new Row(cells);
    }

    private static IRow Pad(IRow row, IReadOnlyList<IColumn> missingColumns)
    {
        Dictionary<IColumn, ICell> cells = new(ReferenceColumnComparer.Instance);

        foreach (KeyValuePair<IColumn, ICell> cell in row.Cells)
        {
            cells[cell.Key] = cell.Value;
        }

        foreach (IColumn column in missingColumns)
        {
            cells[column] = EmptyCell;
        }

        return new Row(cells);
    }

    // Column instances are the identity of a cell within a row. The schema
    // column types' own Equals/GetHashCode throw by design (hashing is meant
    // to go through ColumnHash), so row dictionaries key by reference.
    private sealed class ReferenceColumnComparer : IEqualityComparer<IColumn>
    {
        public static readonly ReferenceColumnComparer Instance = new();

        public bool Equals(IColumn? x, IColumn? y)
        {
            return ReferenceEquals(x, y);
        }

        public int GetHashCode(IColumn obj)
        {
            return RuntimeHelpers.GetHashCode(obj);
        }
    }
}

// One applied join: the joined row set plus the columns every emitted row
// carries (left side's columns followed by the qualified right columns).
internal sealed record JoinedRows(
    IQueryable<IRow> Rows,
    IReadOnlyList<IColumn> Columns
);
