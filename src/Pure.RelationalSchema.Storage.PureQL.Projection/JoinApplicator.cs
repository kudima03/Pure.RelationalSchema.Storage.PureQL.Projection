using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

internal static class JoinApplicator
{
    internal static IQueryable<IRow> Apply(
        IQueryable<IRow> left,
        List<IStoredSchemaDataSet> datasets,
        Join join
    )
    {
        IEnumerable<string> reversedPath = join.Entity
            .Split(".")
            .AsEnumerable()
            .Reverse();

        string tableName = reversedPath.First();
        string schemaName = reversedPath.Skip(1).First();

        IEnumerable<IRow> right = datasets
            .First(x => x.Schema.Name.TextValue == schemaName)
            .First(x => x.Key.Name.TextValue == tableName)
            .Value;

        Func<IRow, bool> onCondition = WhereExpressionBuilder.Build(join.On).Compile();

        List<IRow> leftList = left.ToList();
        List<IRow> rightList = right.ToList();

        IEnumerable<IRow> result = join.Type switch
        {
            JoinType.Inner => InnerJoin(leftList, rightList, onCondition),
            JoinType.Left => LeftJoin(leftList, rightList, onCondition),
            JoinType.Right => RightJoin(leftList, rightList, onCondition),
            JoinType.Full => FullJoin(leftList, rightList, onCondition),
            _ => throw new NotSupportedException($"JoinType {join.Type} is not supported."),
        };

        return result.AsQueryable();
    }

    private static IEnumerable<IRow> InnerJoin(
        List<IRow> left,
        List<IRow> right,
        Func<IRow, bool> onCondition
    )
    {
        return left
            .SelectMany(l => right.Select(r => MergeRows(l, r)))
            .Where(onCondition);
    }

    private static IEnumerable<IRow> LeftJoin(
        List<IRow> left,
        List<IRow> right,
        Func<IRow, bool> onCondition
    )
    {
        return left.SelectMany(l =>
        {
            List<IRow> matched = right
                .Select(r => MergeRows(l, r))
                .Where(onCondition)
                .ToList();

            return matched.Count > 0 ? matched : [l];
        });
    }

    private static IEnumerable<IRow> RightJoin(
        List<IRow> left,
        List<IRow> right,
        Func<IRow, bool> onCondition
    )
    {
        return right.SelectMany(r =>
        {
            List<IRow> matched = left
                .Select(l => MergeRows(l, r))
                .Where(onCondition)
                .ToList();

            return matched.Count > 0 ? matched : [r];
        });
    }

    private static IEnumerable<IRow> FullJoin(
        List<IRow> left,
        List<IRow> right,
        Func<IRow, bool> onCondition
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
                    matchedRightIndexes.Add(i);
                }
            }

            if (matched.Count > 0)
            {
                result.AddRange(matched);
            }
            else
            {
                result.Add(l);
            }
        }

        for (int i = 0; i < right.Count; i++)
        {
            if (!matchedRightIndexes.Contains(i))
            {
                result.Add(right[i]);
            }
        }

        return result;
    }

    private static IRow MergeRows(IRow leftRow, IRow rightRow)
    {
        HashSet<string> leftNames = leftRow
            .Cells.Keys.Select(c => c.Name.TextValue)
            .ToHashSet();

        IEnumerable<KeyValuePair<IColumn, ICell>> rightOnly = rightRow
            .Cells.Where(kvp => !leftNames.Contains(kvp.Key.Name.TextValue));

        List<IColumn> allColumns = leftRow
            .Cells.Keys.Concat(rightOnly.Select(kvp => kvp.Key))
            .ToList();

        IReadOnlyDictionary<IColumn, ICell> allCells = leftRow
            .Cells.Concat(rightOnly)
            .ToDictionary(kvp => kvp.Key, kvp => kvp.Value);

        return new Row(
            new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                allColumns,
                c => c,
                c => allCells.First(kvp => kvp.Key.Name.TextValue == c.Name.TextValue).Value,
                c => new ColumnHash(c)
            )
        );
    }
}
