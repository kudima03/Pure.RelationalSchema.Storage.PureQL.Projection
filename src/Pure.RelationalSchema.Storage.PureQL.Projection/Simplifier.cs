using System.Linq.Expressions;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Simplifies a tree once it is built, bottom up, without changing what it
// computes:
// - an operation over constants is folded to its value (unless computing it
//   fails, which is left to the query; constant strings compare by code
//   point), a captured parameter is never read, and a value boxed to object
//   keeps its type;
// - true and false fold out of and, or, not and conditionals, a value that
//   cannot be null is not checked for null, a lifted operation over null is
//   null, over values that cannot be null it is not lifted, and a conversion
//   to the type a value already has is dropped;
// - a member of a tuple being built is the item it is built from;
// - adjacent Where calls merge into one, Where(true) and an identity Select
//   are dropped, a Select over a Select is one Select and a Where not
//   correlated with an enclosing row moves below a Select (except a stored
//   source's scan, which keeps its shape), and a Select moves below Skip and
//   Take, so a query ends in its pagination.
internal sealed class Simplifier : ExpressionVisitor
{
    // The types whose members compute values from their operands only.
    private static readonly HashSet<Type> Pure =
    [
        typeof(Math),
        typeof(decimal),
        typeof(double),
        typeof(long),
        typeof(int),
        typeof(bool),
        typeof(string),
        typeof(DateOnly),
        typeof(TimeOnly),
        typeof(DateTime),
        typeof(TimeSpan),
        typeof(Guid),
    ];

    private Simplifier() { }

    public static Expression Simplify(Expression node)
    {
        return new Simplifier().Visit(node);
    }

    // Whether an expression reads a parameter: an order key that does not
    // read its row is the same for every row.
    public static bool Reads(Expression node, ParameterExpression parameter)
    {
        Finder finder = new(parameter);
        _ = finder.Visit(node);
        return finder.Found;
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        Expression visited = base.VisitBinary(node);
        return visited is BinaryExpression binary ? Binary(binary) : visited;
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        Expression visited = base.VisitUnary(node);
        return visited is UnaryExpression unary ? Unary(unary) : visited;
    }

    protected override Expression VisitConditional(ConditionalExpression node)
    {
        Expression visited = base.VisitConditional(node);

        return visited is ConditionalExpression conditional
            ? Conditional(conditional)
            : visited;
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        Expression visited = base.VisitMember(node);
        return visited is MemberExpression member ? Member(member) : visited;
    }

    protected override Expression VisitDefault(DefaultExpression node)
    {
        return node.Type.IsValueType && Nullable.GetUnderlyingType(node.Type) is null
            ? node
            : Expression.Constant(null, node.Type);
    }

    protected override Expression VisitNew(NewExpression node)
    {
        Expression visited = base.VisitNew(node);

        return visited is NewExpression created
            && Pure.Contains(created.Type)
            && created.Arguments.All(argument => argument is ConstantExpression)
            ? Fold(created)
            : visited;
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        Expression visited = base.VisitMethodCall(node);

        return visited is not MethodCallExpression call ? visited
            : call.Method.DeclaringType == typeof(Queryable) ? Query(call)
            : IsOrdinalComparison(call) ? CodePointComparison(call)
            : IsPure(call) ? Fold(call)
            : call;
    }

    private Expression Binary(BinaryExpression node)
    {
        return node.NodeType is ExpressionType.AndAlso ? AndAlso(node)
            : node.NodeType is ExpressionType.OrElse ? OrElse(node)
            : node.NodeType is ExpressionType.Coalesce ? Coalesce(node)
            : Lifted(node);
    }

    private static Expression AndAlso(BinaryExpression node)
    {
        return IsConstant(node.Left, true) ? node.Right
            : IsConstant(node.Left, false) ? node.Left
            : IsConstant(node.Right, true) ? node.Left
            : IsConstant(node.Right, false) ? node.Right
            : node;
    }

    private static Expression OrElse(BinaryExpression node)
    {
        return IsConstant(node.Left, true) ? node.Left
            : IsConstant(node.Left, false) ? node.Right
            : IsConstant(node.Right, true) ? node.Right
            : IsConstant(node.Right, false) ? node.Left
            : node;
    }

    // The first operand unless it is null, the second when it is.
    private Expression Coalesce(BinaryExpression node)
    {
        return node.Left is ConstantExpression { Value: null }
                ? Typed.As(node.Right, node.Type)
            : node.Left is ConstantExpression || Plain(node.Left) is not null
                ? Visit(Typed.As(node.Left, node.Type))
            : node.Right is ConstantExpression { Value: null }
                ? Typed.As(node.Left, node.Type)
            : node;
    }

    // A lifted operation over null is null (an ordering comparison false);
    // over values that cannot be null it is the plain operation.
    private static Expression Lifted(BinaryExpression node)
    {
        bool overNull =
            node.Left is ConstantExpression { Value: null }
            || node.Right is ConstantExpression { Value: null };

        return overNull && node.IsLiftedToNull ? Expression.Constant(null, node.Type)
            : overNull && node.IsLifted && IsOrdering(node.NodeType)
                ? Expression.Constant(false)
            : Equality(node)
                ?? (
                    node.IsLifted
                    && Plain(node.Left) is Expression left
                    && Plain(node.Right) is Expression right
                        ? Typed.As(
                            Fold(Expression.MakeBinary(node.NodeType, left, right)),
                            node.Type
                        )
                        : Fold(node)
                );
    }

    // A value that cannot be null is never equal to null.
    private static Expression? Equality(BinaryExpression node)
    {
        bool againstNull =
            (IsNull(node.Left) && Plain(node.Right) is not null)
            || (IsNull(node.Right) && Plain(node.Left) is not null);

        return againstNull
            && node.NodeType is ExpressionType.Equal or ExpressionType.NotEqual
            ? Expression.Constant(node.NodeType == ExpressionType.NotEqual)
            : null;
    }

    private Expression Unary(UnaryExpression node)
    {
        return node.NodeType is ExpressionType.Not && node.Type == typeof(bool)
                ? Not(node.Operand)
            : node.NodeType is ExpressionType.Convert or ExpressionType.ConvertChecked
            && node.Method is null
                ? Converted(node)
            : node.NodeType is ExpressionType.Quote ? node
            : Fold(node);
    }

    // A conversion to the type a value has is the value, and so is the
    // conversion back of a value converted to its nullable type; a value that
    // cannot be null is boxed as itself.
    private static Expression Converted(UnaryExpression node)
    {
        Expression operand = node.Operand;
        Expression? plain = operand is UnaryExpression ? Plain(operand) : null;

        return operand.Type == node.Type ? operand
            : plain is not null && plain.Type == node.Type ? plain
            : plain is not null && !node.Type.IsValueType
                ? Expression.Convert(plain, node.Type)
            : Fold(node);
    }

    private Expression Conditional(ConditionalExpression node)
    {
        return IsConstant(node.Test, true) ? node.IfTrue
            : IsConstant(node.Test, false) ? node.IfFalse
            : IsConstant(node.IfTrue, true) && IsConstant(node.IfFalse, false) ? node.Test
            : IsConstant(node.IfTrue, false) && IsConstant(node.IfFalse, true)
                ? Not(node.Test)
            : node;
    }

    // A member of a tuple being built is the item built for it, and of the
    // default tuple the default item; the value of a value converted to its
    // nullable type is the one converted.
    private Expression Member(MemberExpression node)
    {
        Expression? owner = node.Expression;
        string name = node.Member.Name;

        return owner switch
        {
            NewExpression created when Tuples.IsTuple(created.Type) =>
                created.Arguments[Tuples.Position(name)],
            DefaultExpression empty when Tuples.IsTuple(empty.Type) => VisitDefault(
                Expression.Default(node.Type)
            ),
            not null when name == "Value" && Plain(owner) is Expression value => value,
            ConstantExpression { Value: not null }
                when IsPure(node.Member.DeclaringType!) => Fold(node),
            _ => node,
        };
    }

    private Expression Query(MethodCallExpression call)
    {
        return Linq.Is(call, nameof(Queryable.Where)) ? Where(call)
            : Linq.Is(call, nameof(Queryable.Select)) ? Select(call)
            : call;
    }

    // Where(true) is its rows; two Wheres are one; a Where over a Select it
    // can read through filters below it, closer to the source.
    private Expression Where(MethodCallExpression call)
    {
        Expression source = call.Arguments[0];
        LambdaExpression predicate = Linq.Lambda(call.Arguments[1]);

        return predicate.Parameters.Count != 1 ? call
            : IsConstant(predicate.Body, true) ? source
            : source is MethodCallExpression inner
            && Linq.Is(inner, nameof(Queryable.Where))
                ? Visit(
                    Linq.Where(
                        inner.Arguments[0],
                        Composed(Linq.Lambda(inner.Arguments[1]), predicate)
                    )
                )
            : source is MethodCallExpression select
            && Linq.Is(select, nameof(Queryable.Select))
            && ReadsOnlyItself(predicate)
            && Composable(predicate, select) is LambdaExpression projection
                ? Visit(
                    Linq.Select(
                        Linq.Where(select.Arguments[0], Through(projection, predicate)),
                        projection
                    )
                )
            : call;
    }

    // An identity Select is its rows; a Select over a Select it can read
    // through is one Select; a Select over a page is the page of a Select.
    private Expression Select(MethodCallExpression call)
    {
        Expression source = call.Arguments[0];
        LambdaExpression selector = Linq.Lambda(call.Arguments[1]);
        ParameterExpression row = selector.Parameters[0];

        return selector.Body == row || Rebuilds(selector.Body, row) ? source
            : source is not MethodCallExpression previous ? call
            : Linq.Is(previous, nameof(Queryable.Select))
            && Composable(selector, previous) is LambdaExpression before
                ? Visit(Linq.Select(previous.Arguments[0], Through(before, selector)))
            : Linq.Is(previous, nameof(Queryable.Skip))
                ? Linq.Skip(
                    Visit(Linq.Select(previous.Arguments[0], selector)),
                    previous.Arguments[1]
                )
            : Linq.Is(previous, nameof(Queryable.Take))
                ? Linq.Take(
                    Visit(Linq.Select(previous.Arguments[0], selector)),
                    previous.Arguments[1]
                )
            : call;
    }

    // The selector of a Select an operator over its rows can read through:
    // one that builds a tuple the operator reads items of, or that reads a
    // member, so each read stands for what it reads. A stored source's scan
    // keeps its shape.
    private static LambdaExpression? Composable(
        LambdaExpression outer,
        MethodCallExpression select
    )
    {
        LambdaExpression selector = Linq.Lambda(select.Arguments[1]);

        return select.Arguments[0]
                is not ConstantExpression { Value: IStoredTableDataSet }
            && selector.Parameters.Count == 1
            && (
                Cheap(selector.Body)
                || (
                    selector.Body is NewExpression built
                    && Tuples.IsTuple(built.Type)
                    && MembersOnly(outer.Body, outer.Parameters[0])
                )
            )
            ? selector
            : null;
    }

    // The outer lambda over the rows the inner lambda's body builds.
    private static LambdaExpression Through(
        LambdaExpression inner,
        LambdaExpression outer
    )
    {
        return Expression.Lambda(
            Replace(outer.Body, outer.Parameters[0], inner.Body),
            inner.Parameters
        );
    }

    // Both conditions of two Wheres, over the first one's row.
    private static LambdaExpression Composed(
        LambdaExpression first,
        LambdaExpression second
    )
    {
        return Expression.Lambda(
            Expression.AndAlso(
                first.Body,
                Replace(second.Body, second.Parameters[0], first.Parameters[0])
            ),
            first.Parameters
        );
    }

    private Expression Not(Expression operand)
    {
        return operand switch
        {
            ConstantExpression { Value: bool flag } => Expression.Constant(!flag),
            UnaryExpression { NodeType: ExpressionType.Not } inner => inner.Operand,
            BinaryExpression
            {
                NodeType: ExpressionType.Equal or ExpressionType.NotEqual,
                IsLiftedToNull: false,
            } compared => Visit(
                Expression.MakeBinary(
                    compared.NodeType == ExpressionType.Equal
                        ? ExpressionType.NotEqual
                        : ExpressionType.Equal,
                    compared.Left,
                    compared.Right
                )
            ),
            _ => Expression.Not(operand),
        };
    }

    // The value of an expression over constants, computed now. Computing it
    // may fail (an overflow, a division by zero, a date out of range); the
    // expression is then kept, to fail the query when it runs.
    private static Expression Fold(Expression node)
    {
        bool constant = node switch
        {
            BinaryExpression binary => binary.Left is ConstantExpression
                && binary.Right is ConstantExpression
                && binary.Conversion is null,
            UnaryExpression unary => unary.Operand is ConstantExpression
                && (unary.Type.IsValueType || unary.Type == typeof(string)),
            _ => true,
        };

        if (!constant)
        {
            return node;
        }

        try
        {
            return Expression.Constant(
                Expression
                    .Lambda<Func<object?>>(Expression.Convert(node, typeof(object)))
                    .Compile(preferInterpretation: true)(),
                node.Type
            );
        }
        catch (ArithmeticException)
        {
            return node;
        }
        catch (ArgumentException)
        {
            return node;
        }
        catch (FormatException)
        {
            return node;
        }
        catch (InvalidOperationException)
        {
            return node;
        }
    }

    // Two constant strings compare by code point, the specification's order,
    // which running the tree in memory also uses (CodePoints); UTF-16 ordinal
    // order, which the tree names, differs for surrogates.
    private static Expression CodePointComparison(MethodCallExpression call)
    {
        return Expression.Constant(
            CodePoints.Compare(
                (string?)((ConstantExpression)call.Arguments[0]).Value,
                (string?)((ConstantExpression)call.Arguments[1]).Value
            )
        );
    }

    private static bool IsOrdinalComparison(MethodCallExpression call)
    {
        return call.Method.DeclaringType == typeof(string)
            && call.Method.Name == nameof(string.CompareOrdinal)
            && call.Arguments.Count == 2
            && call.Arguments.All(argument => argument is ConstantExpression);
    }

    // A call of a pure method over constants, which folds to its value.
    private static bool IsPure(MethodCallExpression call)
    {
        Type owner = call.Method.DeclaringType!;

        return (
                IsPure(owner)
                || (
                    owner == typeof(Enumerable)
                    && call.Method.Name == nameof(Enumerable.Contains)
                )
            )
            && call.Object is null or ConstantExpression
            && call.Arguments.All(argument => argument is ConstantExpression);
    }

    private static bool IsPure(Type owner)
    {
        return Pure.Contains(owner) || Nullable.GetUnderlyingType(owner) is not null;
    }

    private static bool IsOrdering(ExpressionType type)
    {
        return type
            is ExpressionType.GreaterThan
                or ExpressionType.GreaterThanOrEqual
                or ExpressionType.LessThan
                or ExpressionType.LessThanOrEqual;
    }

    private static bool IsNull(Expression node)
    {
        return node is ConstantExpression { Value: null };
    }

    private static bool IsConstant(Expression node, bool value)
    {
        return node is ConstantExpression { Value: bool flag } && flag == value;
    }

    // The non-null value of an operand of a nullable type that cannot be
    // null: a value converted to its nullable type, or a constant.
    private static Expression? Plain(Expression operand)
    {
        return operand switch
        {
            UnaryExpression { NodeType: ExpressionType.Convert } converted
                when Nullable.GetUnderlyingType(converted.Type)
                    == converted.Operand.Type => converted.Operand,
            ConstantExpression { Value: not null } constant
                when Nullable.GetUnderlyingType(constant.Type) is Type underlying =>
                Expression.Constant(constant.Value, underlying),
            _ => null,
        };
    }

    // Whether a tuple is built from exactly the items of a tuple of its type.
    private static bool Rebuilds(Expression body, Expression tuple)
    {
        return body is NewExpression created
            && created.Type == tuple.Type
            && Tuples.IsTuple(created.Type)
            && created
                .Arguments.Select(
                    (argument, index) =>
                        (
                            argument is MemberExpression member
                            && member.Expression == tuple
                            && Tuples.Position(member.Member.Name) == index
                        )
                        || (
                            argument is NewExpression
                            && Rebuilds(argument, Expression.Field(tuple, "Rest"))
                        )
                )
                .All(rebuilt => rebuilt);
    }

    // A parameter or a member of one: reading it twice costs nothing.
    private static bool Cheap(Expression node)
    {
        return node is ParameterExpression
            || (
                node is MemberExpression { Expression: Expression owner }
                && Cheap(owner)
            );
    }

    // Whether a parameter is only read through its members.
    private static bool MembersOnly(Expression body, ParameterExpression parameter)
    {
        MemberReader reader = new(parameter);
        _ = reader.Visit(body);
        return !reader.ReadWhole;
    }

    // Whether a lambda reads no parameter but its own: a condition that is
    // not correlated with an enclosing row.
    private static bool ReadsOnlyItself(LambdaExpression lambda)
    {
        Outsider outsider = new(lambda.Parameters);
        _ = outsider.Visit(lambda.Body);
        return !outsider.Found;
    }

    private static Expression Replace(
        Expression body,
        ParameterExpression parameter,
        Expression replacement
    )
    {
        return new Replacer(parameter, replacement).Visit(body);
    }

    private sealed class Replacer(ParameterExpression parameter, Expression replacement)
        : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter = parameter;

        private readonly Expression _replacement = replacement;

        protected override Expression VisitParameter(ParameterExpression node)
        {
            return node == _parameter ? _replacement : node;
        }
    }

    private sealed class Finder(ParameterExpression parameter) : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter = parameter;

        public bool Found { get; private set; }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            Found |= node == _parameter;
            return node;
        }
    }

    private sealed class Outsider(IEnumerable<ParameterExpression> own)
        : ExpressionVisitor
    {
        private readonly HashSet<ParameterExpression> _known = [.. own];

        public bool Found { get; private set; }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            _known.UnionWith(node.Parameters);
            return base.VisitLambda(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            Found |= !_known.Contains(node);
            return node;
        }
    }

    private sealed class MemberReader(ParameterExpression parameter) : ExpressionVisitor
    {
        private readonly ParameterExpression _parameter = parameter;

        public bool ReadWhole { get; private set; }

        protected override Expression VisitMember(MemberExpression node)
        {
            return node.Expression == _parameter ? node : base.VisitMember(node);
        }

        protected override Expression VisitParameter(ParameterExpression node)
        {
            ReadWhole |= node == _parameter;
            return node;
        }
    }
}
