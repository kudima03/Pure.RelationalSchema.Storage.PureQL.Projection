using Pure.RelationalSchema.Abstractions.Column;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.Types;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The checks the specification leaves to the interpreter, because they need
// the catalog or names: entities, subqueries, sources and fields exist; a
// field declares its column's type and, after outer joins, its nullability;
// group key references name a key and repeat its type; names and aliases are
// unique; distinctBy lists select aliases; a bound parameter's value has the
// parameter's type and a bound pagination is in range.
// Storage columns carry no nullability, so for an entity column only the
// outer-join rule can be checked; subquery columns are checked exactly.
internal static class QueryValidator
{
    public static void Validate(
        QueryProgram program,
        Catalog catalog,
        ParameterValues parameters
    )
    {
        Dictionary<string, QueryClauses> declared = new(StringComparer.Ordinal);

        foreach (NamedQuery subquery in program.Subqueries)
        {
            Validate(subquery.Clauses, catalog, declared, parameters);

            if (!declared.TryAdd(subquery.Name, subquery.Clauses))
            {
                throw new ArgumentException(
                    $"Subquery '{subquery.Name}' is declared more than once."
                );
            }
        }

        Validate(program.Main, catalog, declared, parameters);
    }

    private static void Validate(
        QueryClauses query,
        Catalog catalog,
        IReadOnlyDictionary<string, QueryClauses> subqueries,
        ParameterValues parameters
    )
    {
        List<Source> sources = [query.From, .. query.Joins.Select(join => join.Source)];
        Dictionary<string, IReadOnlyDictionary<string, DeclaredType>> columns = new(
            StringComparer.Ordinal
        );

        foreach (Source source in sources)
        {
            if (!columns.TryAdd(source.Name, ColumnsOf(source, catalog, subqueries)))
            {
                throw new ArgumentException(
                    $"Source name '{source.Name}' is declared more than once."
                );
            }
        }

        HashSet<string> optional = new(StringComparer.Ordinal);

        for (int i = 0; i < query.Joins.Count; i++)
        {
            JoinClause join = query.Joins[i];

            new References(
                columns
                    .Where(source => sources.Take(i + 2).Any(s => s.Name == source.Key))
                    .ToDictionary(StringComparer.Ordinal),
                new HashSet<string>(optional, StringComparer.Ordinal),
                subqueries,
                null,
                parameters
            ).Check(join.On);

            optional.UnionWith(
                join.Kind switch
                {
                    JoinKind.Left => [join.Source.Name],
                    JoinKind.Right => sources.Take(i + 1).Select(source => source.Name),
                    JoinKind.Full => sources.Take(i + 2).Select(source => source.Name),
                    JoinKind.Inner or _ => [],
                }
            );
        }

        RejectRepeated(
            (query.GroupBy ?? []).Select(key => key.Alias),
            "Group key alias"
        );

        References references = new(
            columns,
            optional,
            subqueries,
            query.GroupBy,
            parameters
        );

        foreach (
            object expression in new object?[] { query.Where, query.Having }
                .Concat((query.GroupBy ?? []).Select(key => key.Expression))
                .Concat(query.Select.Select(column => column.Expression))
                .Concat(query.OrderBy.Select(key => key.Expression))
                .OfType<object>()
        )
        {
            references.Check(expression);
        }

        RejectRepeated(query.Select.Select(column => column.Alias), "Column alias");

        string? unknown = query.DistinctBy.FirstOrDefault(alias =>
            !query.Select.Any(column => column.Alias == alias)
        );

        if (unknown is not null)
        {
            throw new ArgumentException(
                $"distinctBy lists '{unknown}', which is not a column alias."
            );
        }

        if (query.Pagination is not null)
        {
            ValidatePagination(query.Pagination, parameters);
        }
    }

    private static void RejectRepeated(IEnumerable<string> names, string what)
    {
        string? repeated = names
            .GroupBy(name => name, StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1)
            ?.Key;

        if (repeated is not null)
        {
            throw new ArgumentException($"{what} '{repeated}' is used more than once.");
        }
    }

    // A pagination parameter is checked like every other: bound to an integer
    // literal, whose value is then in range.
    private static void ValidatePagination(
        Pagination pagination,
        ParameterValues parameters
    )
    {
        long skip = pagination.Skip.Match(number => number, parameters.Integer);
        long take = pagination.Take.Match(number => number, parameters.Integer);

        if (skip < 0)
        {
            throw new ArgumentException($"Pagination skip {skip} is negative.");
        }

        if (take < 1)
        {
            throw new ArgumentException($"Pagination take {take} is less than 1.");
        }
    }

    private static IReadOnlyDictionary<string, DeclaredType> ColumnsOf(
        Source source,
        Catalog catalog,
        IReadOnlyDictionary<string, QueryClauses> subqueries
    )
    {
        Dictionary<string, DeclaredType> columns = new(StringComparer.Ordinal);

        if (source.IsSubquery)
        {
            QueryClauses subquery = subqueries.TryGetValue(
                source.Target,
                out QueryClauses? found
            )
                ? found
                : throw new ArgumentException(
                    $"Subquery '{source.Target}' is not declared before it is read."
                );

            foreach (TypedExpression column in subquery.Select)
            {
                columns[column.Alias] = new DeclaredType(
                    column.Type.Name,
                    column.Type.Nullable
                );
            }
        }
        else
        {
            foreach (IColumn column in catalog.Table(source.Target).Key.Columns)
            {
                columns[column.Name.TextValue] = new DeclaredType(
                    ValueTypes.OfColumn(column.Type),
                    null
                );
            }
        }

        return columns;
    }

    // A column's PureQL type; Nullable is null when storage does not say.
    private sealed record DeclaredType(string? Name, bool? Nullable);

    private sealed class References(
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, DeclaredType>> sources,
        IReadOnlySet<string> optional,
        IReadOnlyDictionary<string, QueryClauses> subqueries,
        IReadOnlyList<TypedExpression>? keys,
        ParameterValues parameters
    )
    {
        public void Check(object expression)
        {
            foreach (object node in ModelNode.Nodes(expression, _ => true))
            {
                switch (node)
                {
                    case IField field:
                        CheckField(field);
                        break;
                    case IKey key:
                        CheckKey(key);
                        break;
                    case IParameter parameter:
                        parameters.Check(parameter);
                        break;
                    default:
                        if (ModelNode.Has(node, "Subquery"))
                        {
                            CheckSubqueryColumn(node);
                        }

                        break;
                }
            }
        }

        private void CheckField(IField field)
        {
            string name = $"{field.Source}.{field.Field}";

            DeclaredType column = sources.TryGetValue(
                field.Source,
                out IReadOnlyDictionary<string, DeclaredType>? fields
            )
                ? fields.TryGetValue(field.Field, out DeclaredType? found)
                    ? found
                    : throw new ArgumentException(
                        $"Source '{field.Source}' has no field '{field.Field}'."
                    )
                : throw new ArgumentException(
                    $"Field '{name}' reads source '{field.Source}', which is not "
                        + "declared at that point of the query."
                );

            if (column.Name != field.Type.Name)
            {
                throw new ArgumentException(
                    $"Field '{name}' is declared {field.Type.Name} but holds "
                        + $"{column.Name ?? "a type PureQL does not have"}."
                );
            }

            bool joinedOptional = optional.Contains(field.Source);

            if (
                (!field.Type.Nullable && joinedOptional)
                || (
                    column.Nullable is bool nullable
                    && field.Type.Nullable != (nullable || joinedOptional)
                )
            )
            {
                throw new ArgumentException(
                    $"Field '{name}' is declared "
                        + (field.Type.Nullable ? "nullable" : "non-null")
                        + " but is not at that point of the query."
                );
            }
        }

        private void CheckKey(IKey key)
        {
            TypedExpression declared =
                (
                    keys
                    ?? throw new ArgumentException(
                        $"Group key '{key.Key}' is referenced outside a grouped query."
                    )
                ).FirstOrDefault(candidate => candidate.Alias == key.Key)
                ?? throw new ArgumentException($"Group key '{key.Key}' does not exist.");

            if (!SameType(declared.Type, key.Type))
            {
                throw new ArgumentException(
                    $"Group key '{key.Key}' is referenced with another type than it "
                        + "declares."
                );
            }
        }

        private void CheckSubqueryColumn(object column)
        {
            string subquery = (string)ModelNode.Child(column, "Subquery");
            string field = (string)ModelNode.Child(column, "Field");

            TypedExpression declared =
                (
                    subqueries.TryGetValue(subquery, out QueryClauses? clauses)
                        ? clauses
                        : throw new ArgumentException(
                            $"Subquery '{subquery}' is not declared before it is read."
                        )
                ).Select.FirstOrDefault(selected => selected.Alias == field)
                ?? throw new ArgumentException(
                    $"Subquery '{subquery}' has no column '{field}'."
                );

            if (!SameType(declared.Type, (IType)ModelNode.Child(column, "Type")))
            {
                throw new ArgumentException(
                    $"Column '{field}' of subquery '{subquery}' is read with another "
                        + "type than it declares."
                );
            }
        }

        private static bool SameType(IType left, IType right)
        {
            return left.Name == right.Name && left.Nullable == right.Nullable;
        }
    }
}
