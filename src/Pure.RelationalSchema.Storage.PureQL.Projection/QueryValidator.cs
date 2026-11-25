using Pure.RelationalSchema.Abstractions.Column;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.Types;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The checks the specification leaves to the interpreter, because they need
// the catalog, names or parameter values: entities, subqueries, sources and
// fields exist; a field declares its column's type and, after outer joins,
// its nullability; group key references name a key and repeat its type;
// names and aliases are unique; distinctBy and the columns of orderBy are
// select aliases; the inputs of a set operation and the rows of an inline
// subquery have the same aliases and types that meet; the next of a
// recursive subquery reads the subquery once, where it has a row of its own,
// and gives the seed's columns; a parameter has one type and a value of it;
// a regular expression is one of the dialect and has the group asked for; a
// zone is a time zone name; a bound pagination, fraction, frame, range,
// offset, buckets and maxDepth are in range. A failing query is an
// ArgumentException.
// Storage columns carry no nullability, so for an entity column only the
// outer-join rule can be checked; subquery columns are checked exactly.
internal sealed class QueryValidator
{
    // The properties of a frame, a range and a distance that hold a count
    // or an amount.
    private static readonly string[] Distances =
    [
        "Preceding",
        "Following",
        "Days",
        "Months",
        "Seconds",
    ];

    private readonly Catalog _catalog;

    private readonly ParameterValues _parameters;

    private readonly Dictionary<string, IReadOnlyList<QueryColumn>> _declared = [with(
        StringComparer.Ordinal
    )];

    private readonly Dictionary<string, IType> _parameterTypes = [with(
        StringComparer.Ordinal
    )];

    private QueryValidator(Catalog catalog, ParameterValues parameters)
    {
        _catalog = catalog;
        _parameters = parameters;
    }

    public static void Validate(
        QueryProgram program,
        Catalog catalog,
        ParameterValues parameters
    )
    {
        QueryValidator validator = new(catalog, parameters);

        foreach (NamedQuery subquery in program.Subqueries)
        {
            IReadOnlyList<QueryColumn> columns = validator.Validate(subquery);

            if (!validator._declared.TryAdd(subquery.Name, columns))
            {
                throw new ArgumentException(
                    $"Subquery '{subquery.Name}' is declared more than once."
                );
            }
        }

        _ = validator.Validate(program.Main);
    }

    private IReadOnlyList<QueryColumn> Validate(NamedQuery subquery)
    {
        switch (subquery.Body)
        {
            case InlineRows rows:
                foreach (TypedExpression cell in rows.Rows.SelectMany(row => row))
                {
                    Closed().Check(cell.Expression);
                }

                return Headers.Of(rows);
            case RecursiveQuery recursive:
                return Validate(subquery.Name, recursive);
            case PlainSubquery plain:
                return Validate(plain.Query);
            default:
                throw new NotSupportedException(
                    $"{subquery.Body.GetType().Name} is not a subquery this "
                        + "interpreter knows."
                );
        }
    }

    private IReadOnlyList<QueryColumn> Validate(QueryShape query)
    {
        if (query is CombinedQuery combined)
        {
            foreach (QueryShape input in combined.Inputs)
            {
                _ = Validate(input);
            }
        }
        else
        {
            Validate((QueryClauses)query);
        }

        return Headers.Of(query);
    }

    // The seed does not read the subquery, which is not declared yet; next
    // reads it under its own name, with the seed's columns, exactly once, in
    // from or a join and not where an outer join may leave it without a row,
    // and has the seed's aliases, each once, with types the seed's accept.
    private IReadOnlyList<QueryColumn> Validate(string name, RecursiveQuery recursive)
    {
        IReadOnlyList<QueryColumn> columns = Validate(recursive.Seed);
        QueryClauses next = recursive.Next;

        if (_declared.ContainsKey(name))
        {
            throw new ArgumentException($"Subquery '{name}' is declared more than once.");
        }

        _declared[name] = columns;
        Validate(next);
        _ = _declared.Remove(name);

        List<Source> sources = [next.From, .. next.Joins.Select(join => join.Source)];
        List<int> reads =
        [
            .. Enumerable
                .Range(0, sources.Count)
                .Where(index => sources[index].IsSubquery && sources[index].Target == name),
        ];

        if (
            reads.Count != 1
            || Expressions(next)
                .SelectMany(expression => ModelNode.Nodes(expression, _ => true))
                .OfType<ISubqueryColumn>()
                .Any(column => column.Subquery == name)
        )
        {
            throw new ArgumentException(
                $"The next of recursive subquery '{name}' reads the subquery "
                    + (reads.Count == 0 ? "nowhere" : "more than once")
                    + "; it reads it exactly once, in from or a join."
            );
        }

        if (Optional(next, sources.Count).Contains(sources[reads[0]].Name))
        {
            throw new ArgumentException(
                $"The next of recursive subquery '{name}' reads the subquery on the "
                    + "optional side of an outer join."
            );
        }

        IReadOnlyList<QueryColumn> produced = Headers.Of(next);
        RejectRepeated(produced.Select(column => column.Alias), "Column alias");

        if (
            produced.Count != columns.Count
            || produced.Any(column => columns.All(seed => seed.Alias != column.Alias))
        )
        {
            throw new ArgumentException(
                $"The next of recursive subquery '{name}' does not have the column "
                    + "aliases of its seed."
            );
        }

        foreach (QueryColumn seed in columns)
        {
            IType type = produced.First(column => column.Alias == seed.Alias).Type;

            if (!DeclaredType.Of(seed.Type).Accepts(type))
            {
                throw new ArgumentException(
                    $"Column '{seed.Alias}' of recursive subquery '{name}' is "
                        + $"{Describe(seed.Type)} in its seed, which does not accept the "
                        + $"{Describe(type)} its next gives."
                );
            }
        }

        if (recursive.MaxDepth is IParameter depth)
        {
            Closed().Check(depth);
            AtLeast(depth, 1, "maxDepth");
        }

        return columns;
    }

    private void Validate(QueryClauses query)
    {
        List<Source> sources = [query.From, .. query.Joins.Select(join => join.Source)];
        Dictionary<string, IReadOnlyDictionary<string, ColumnType>> columns = [with(
            StringComparer.Ordinal
        )];

        foreach (Source source in sources)
        {
            if (!columns.TryAdd(source.Name, ColumnsOf(source)))
            {
                throw new ArgumentException(
                    $"Source name '{source.Name}' is declared more than once."
                );
            }
        }

        for (int i = 0; i < query.Joins.Count; i++)
        {
            new References(
                this,
                columns
                    .Where(source => sources.Take(i + 2).Any(s => s.Name == source.Key))
                    .ToDictionary(StringComparer.Ordinal),
                Optional(query, i + 1),
                null
            ).Check(query.Joins[i].On);
        }

        RejectRepeated(
            (query.GroupBy ?? []).Select(key => key.Alias),
            "Group key alias"
        );
        RejectRepeated(query.Select.Select(column => column.Alias), "Column alias");

        string? unknown = query
            .DistinctBy.Concat(query.OrderBy.Select(key => key.Column).OfType<string>())
            .FirstOrDefault(alias => !query.Select.Any(column => column.Alias == alias));

        if (unknown is not null)
        {
            throw new ArgumentException(
                query.DistinctBy.Contains(unknown)
                    ? $"distinctBy lists '{unknown}', which is not a column alias."
                    : $"orderBy sorts by column '{unknown}', which is not a column alias."
            );
        }

        References references = new(
            this,
            columns,
            Optional(query, sources.Count),
            query.GroupBy
        );

        foreach (object expression in Expressions(query))
        {
            references.Check(expression);
        }

        if (query.Pagination is not null)
        {
            ValidatePagination(query.Pagination);
        }
    }

    // The expressions of a query after its joins.
    private static IEnumerable<object> Expressions(QueryClauses query)
    {
        return new object?[] { query.Where, query.Having }
            .Concat((query.GroupBy ?? []).Select(key => key.Expression))
            .Concat(query.Select.Select(column => column.Expression))
            .Concat(query.OrderBy.Select(key => key.Expression))
            .OfType<object>();
    }

    // The sources an outer join may leave without a row once the first
    // sources of a query are joined: the joined source of a left join, every
    // earlier source of a right join, both for a full join.
    private static HashSet<string> Optional(QueryClauses query, int joined)
    {
        List<Source> sources = [query.From, .. query.Joins.Select(join => join.Source)];
        HashSet<string> optional = [with(StringComparer.Ordinal)];

        for (int i = 0; i < joined - 1; i++)
        {
            JoinClause join = query.Joins[i];

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

        return optional;
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
    // literal, whose value is then in range. A number is in range too: a
    // model built in code is not held to the schema.
    private void ValidatePagination(Pagination pagination)
    {
        if (ModelNode.Optional(pagination, "Skip") is object skip && Number(skip) < 0)
        {
            throw new ArgumentException($"Pagination skip {Number(skip)} is negative.");
        }

        if (ModelNode.Optional(pagination, "Take") is object take && Number(take) < 1)
        {
            throw new ArgumentException(
                $"Pagination take {Number(take)} is less than 1."
            );
        }
    }

    private long Number(object count)
    {
        if (count is IParameter parameter)
        {
            Closed().Check(parameter);
            return (long)_parameters.Read(parameter)!;
        }

        return (long)count;
    }

    // A parameter that counts or measures something is bound to a value
    // that is not below a minimum.
    private void AtLeast(IParameter parameter, decimal minimum, string what)
    {
        if (
            _parameters.Read(parameter) is object value
            && Convert.ToDecimal(value, null) < minimum
        )
        {
            throw new ArgumentException(
                $"Parameter '{parameter.Param}' is the {what} and is bound to {value}, "
                    + $"which is below {minimum}."
            );
        }
    }

    // The references of an expression that reads no source: a cell of an
    // inline row, a parameter of a clause.
    private References Closed()
    {
        return new References(
            this,
            new Dictionary<string, IReadOnlyDictionary<string, ColumnType>>(),
            new HashSet<string>(),
            null
        );
    }

    private IReadOnlyDictionary<string, ColumnType> ColumnsOf(Source source)
    {
        Dictionary<string, ColumnType> columns = [with(StringComparer.Ordinal)];

        if (source.IsSubquery)
        {
            IReadOnlyList<QueryColumn> subquery = _declared.TryGetValue(
                source.Target,
                out IReadOnlyList<QueryColumn>? found
            )
                ? found
                : throw new ArgumentException(
                    $"Subquery '{source.Target}' is not declared before it is read."
                );

            foreach (QueryColumn column in subquery)
            {
                columns[column.Alias] = new ColumnType(
                    column.Type.Name,
                    column.Type.Nullable
                );
            }
        }
        else
        {
            foreach (IColumn column in _catalog.Table(source.Target).Key.Columns)
            {
                columns[column.Name.TextValue] = new ColumnType(
                    ValueTypes.OfColumn(column.Type),
                    null
                );
            }
        }

        return columns;
    }

    private static string Describe(IType type)
    {
        return type.Nullable ? $"nullable {type.Name}" : type.Name;
    }

    // A column's PureQL type; Nullable is null when storage does not say.
    private sealed record ColumnType(string? Name, bool? Nullable);

    private sealed class References(
        QueryValidator validator,
        IReadOnlyDictionary<string, IReadOnlyDictionary<string, ColumnType>> sources,
        IReadOnlySet<string> optional,
        IReadOnlyList<TypedExpression>? keys
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
                        CheckParameter(parameter);
                        break;
                    case ISubqueryColumn column:
                        CheckSubqueryColumn(column);
                        break;
                    default:
                        CheckValues(node);
                        break;
                }
            }
        }

        private void CheckField(IField field)
        {
            string name = $"{field.Source}.{field.Field}";

            ColumnType column = sources.TryGetValue(
                field.Source,
                out IReadOnlyDictionary<string, ColumnType>? fields
            )
                ? fields.TryGetValue(field.Field, out ColumnType? found)
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

        // A parameter has a value of its type, and one type in the document.
        private void CheckParameter(IParameter parameter)
        {
            validator._parameters.Check(parameter);

            if (
                !validator._parameterTypes.TryAdd(parameter.Param, parameter.Type)
                && !SameType(validator._parameterTypes[parameter.Param], parameter.Type)
            )
            {
                throw new ArgumentException(
                    $"Parameter '{parameter.Param}' is declared "
                        + $"{Describe(validator._parameterTypes[parameter.Param])} and "
                        + $"{Describe(parameter.Type)}; a parameter has one type."
                );
            }
        }

        private void CheckSubqueryColumn(ISubqueryColumn column)
        {
            QueryColumn declared =
                (
                    validator._declared.TryGetValue(
                        column.Subquery,
                        out IReadOnlyList<QueryColumn>? columns
                    )
                        ? columns
                        : throw new ArgumentException(
                            $"Subquery '{column.Subquery}' is not declared before it is "
                                + "read."
                        )
                ).FirstOrDefault(selected => selected.Alias == column.Field)
                ?? throw new ArgumentException(
                    $"Subquery '{column.Subquery}' has no column '{column.Field}'."
                );

            if (!SameType(declared.Type, column.Type))
            {
                throw new ArgumentException(
                    $"Column '{column.Field}' of subquery '{column.Subquery}' is read "
                        + "with another type than it declares."
                );
            }
        }

        // What a node holds that is a literal or a parameter with a range or
        // a form of its own: a regular expression and its group, a zone, a
        // fraction, an offset, a number of buckets, the sides of a frame and
        // the distances of a range.
        private void CheckValues(object node)
        {
            if (ModelNode.Optional(node, "Zone") is object zone)
            {
                CheckZone(zone);
            }

            if (ModelNode.Optional(node, "Fraction") is object fraction)
            {
                CheckFraction(fraction);
            }

            if (ModelNode.Optional(node, "Offset") is IParameter offset)
            {
                CheckParameter(offset);
                validator.AtLeast(offset, 1, "offset of previous or next");
            }

            if (ModelNode.Optional(node, "Buckets") is IParameter buckets)
            {
                CheckParameter(buckets);
                validator.AtLeast(buckets, 1, "number of buckets of ntile");
            }

            if (node is IOperator && Operator.Name(node.GetType()) is string name)
            {
                if (name is "MatchesRegex" or "ExtractRegex" or "ReplaceRegex")
                {
                    CheckRegex(node);
                }

                return;
            }

            foreach (string side in Distances)
            {
                if (
                    node is not IType
                    && ModelNode.Optional(node, side) is IParameter distance
                )
                {
                    CheckParameter(distance);
                    validator.AtLeast(
                        distance,
                        0,
                        "count of a frame or distance of a range"
                    );
                }
            }
        }

        private void CheckZone(object zone)
        {
            object? name = Value(zone);

            if (name is string text && !PureQLTime.IsZone(text))
            {
                throw new ArgumentException(
                    zone is IParameter parameter
                        ? $"Parameter '{parameter.Param}' is a zone and is bound to "
                            + $"'{text}', which is not a time zone name."
                        : $"Zone '{text}' is not a time zone name."
                );
            }
        }

        private void CheckFraction(object fraction)
        {
            if (Value(fraction) is decimal value && (value < 0 || value > 1))
            {
                throw new ArgumentException(
                    fraction is IParameter parameter
                        ? $"Parameter '{parameter.Param}' is a fraction and is bound to "
                            + $"{value}, which is not from 0 to 1."
                        : $"Fraction {value} is not from 0 to 1."
                );
            }
        }

        // A pattern is one of the dialect and has the group asked for.
        private void CheckRegex(object node)
        {
            object pattern = ModelNode.Child(node, "Pattern");
            string text = (string)Value(ModelNode.Unwrap(pattern))!;
            int groups;

            try
            {
                groups = PureQLText.RegexGroups(text);
            }
            catch (FormatException exception)
            {
                throw new ArgumentException(
                    $"'{text}' is not a regular expression of the dialect: "
                        + exception.Message,
                    exception
                );
            }

            if (
                ModelNode.Optional(node, "Group") is object group
                && Value(group) is long number
                && (number < 1 || number > groups)
            )
            {
                throw new ArgumentException(
                    $"Regular expression '{text}' has no group {number}."
                );
            }
        }

        // The value of a literal, or the one a parameter is bound to.
        private object? Value(object node)
        {
            if (node is IParameter parameter)
            {
                CheckParameter(parameter);
                return validator._parameters.Read(parameter);
            }

            return node is ILiteral literal ? Literals.Of(literal) : null;
        }

        private static bool SameType(IType left, IType right)
        {
            return left.Name == right.Name && left.Nullable == right.Nullable;
        }
    }
}
