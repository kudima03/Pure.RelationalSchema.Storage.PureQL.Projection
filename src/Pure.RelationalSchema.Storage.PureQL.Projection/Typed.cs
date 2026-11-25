using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Typing helpers for the tree's values. An integer stands wherever a decimal
// is expected and a non-null value wherever a nullable one is, so an operand
// is converted to the type its operator works in; a condition holds where its
// value is true, and null is not true.
internal static class Typed
{
    // The lifted operations that are null exactly when an operand is.
    private static readonly HashSet<ExpressionType> Arithmetic =
    [
        ExpressionType.Add,
        ExpressionType.AddChecked,
        ExpressionType.Subtract,
        ExpressionType.SubtractChecked,
        ExpressionType.Multiply,
        ExpressionType.MultiplyChecked,
        ExpressionType.Divide,
        ExpressionType.Modulo,
    ];

    public static Expression As(Expression value, Type type)
    {
        if (value.Type == type)
        {
            return value;
        }

        Type? underlying = Nullable.GetUnderlyingType(type);

        return underlying is not null
            && value.Type.IsValueType
            && Nullable.GetUnderlyingType(value.Type) is null
            && value.Type != underlying
            ? Expression.Convert(Expression.Convert(value, underlying), type)
            : Expression.Convert(value, type);
    }

    public static Type OrNull(Type type)
    {
        return type.IsValueType && Nullable.GetUnderlyingType(type) is null
            ? typeof(Nullable<>).MakeGenericType(type)
            : type;
    }

    // The type two operands compare or combine in: decimal when one is an
    // integer and the other a decimal, their common nullable type otherwise.
    public static Type Common(Type left, Type right)
    {
        Type first = OrNull(left);
        Type second = OrNull(right);

        return first == second ? first
            : (first == typeof(long?) && second == typeof(decimal?))
                || (first == typeof(decimal?) && second == typeof(long?))
                ? typeof(decimal?)
            : throw new InvalidOperationException(
                $"Values of {left.Name} and {right.Name} cannot be compared."
            );
    }

    public static Expression Null(Type type)
    {
        return Expression.Constant(null, OrNull(type));
    }

    // Whether a value is null. A value that cannot be null, also one converted
    // to its nullable type, is not. An operator reads the operands it checks,
    // so the check of an operand that holds a conditional (a lifted operation,
    // an if) is built from its parts, never from the operand as a whole,
    // which would double the tree at every level of nesting: a conditional
    // with a null branch is null when it takes that branch or its other branch
    // is null, a lifted arithmetic operation or conversion when an operand is.
    public static Expression IsNull(Expression value)
    {
        return Plain(value) is not null ? Expression.Constant(false)
            : Nested(value) is not Expression nested
                ? Expression.Equal(value, Expression.Constant(null, value.Type))
            : nested switch
            {
                ConditionalExpression choice when IsNullConstant(choice.IfTrue) => Either(
                    choice.Test,
                    IsNull(choice.IfFalse)
                ),
                ConditionalExpression choice => Either(
                    Expression.Not(choice.Test),
                    IsNull(choice.IfTrue)
                ),
                BinaryExpression lifted => Either(
                    IsNull(lifted.Left),
                    IsNull(lifted.Right)
                ),
                _ => IsNull(((UnaryExpression)nested).Operand),
            };
    }

    // The value of a nullable value, which the caller has checked is not null:
    // of a value converted to its nullable type, the value converted; of an
    // operand that holds a conditional, the value of its non-null branch, or
    // the operation over the values of its operands (see IsNull).
    public static Expression ValueOf(Expression value)
    {
        return Plain(value) is Expression plain ? plain
            : Nested(value) is not Expression nested
                ? Nullable.GetUnderlyingType(value.Type) is not null
                    ? Expression.Property(value, "Value")
                    : value
            : nested switch
            {
                ConditionalExpression choice when IsNullConstant(choice.IfTrue) => As(
                    ValueOf(choice.IfFalse),
                    Nullable.GetUnderlyingType(value.Type)!
                ),
                ConditionalExpression choice => As(
                    ValueOf(choice.IfTrue),
                    Nullable.GetUnderlyingType(value.Type)!
                ),
                BinaryExpression lifted => Expression.MakeBinary(
                    lifted.NodeType,
                    ValueOf(lifted.Left),
                    ValueOf(lifted.Right),
                    false,
                    lifted.Method
                ),
                _ => Expression.Convert(
                    ValueOf(((UnaryExpression)nested).Operand),
                    Nullable.GetUnderlyingType(value.Type)!
                ),
            };
    }

    // A value that cannot be null, as its plain type: the value itself, or the
    // one a conversion to the nullable type converts.
    private static Expression? Plain(Expression value)
    {
        return value.Type.IsValueType && Nullable.GetUnderlyingType(value.Type) is null
                ? value
            : value is UnaryExpression { NodeType: ExpressionType.Convert } convert
            && convert.Method is null
            && convert.Operand.Type == Nullable.GetUnderlyingType(value.Type)
                ? convert.Operand
            : null;
    }

    // A nullable value of a value type that holds a conditional and that
    // IsNull and ValueOf take apart: a conditional with a null branch, a
    // lifted arithmetic operation or a conversion between nullable types.
    private static Expression? Nested(Expression value)
    {
        bool parts = value switch
        {
            ConditionalExpression choice => IsNullConstant(choice.IfTrue)
                || IsNullConstant(choice.IfFalse),
            BinaryExpression { IsLiftedToNull: true } lifted => Arithmetic.Contains(
                lifted.NodeType
            ),
            UnaryExpression { NodeType: ExpressionType.Convert } convert => Nullable
                .GetUnderlyingType(convert.Operand.Type)
                is not null,
            _ => false,
        };

        return parts && Nullable.GetUnderlyingType(value.Type) is not null
            && Conditionals.In(value)
            ? value
            : null;
    }

    private static bool IsNullConstant(Expression value)
    {
        return value is ConstantExpression { Value: null };
    }

    private static Expression Either(Expression first, Expression second)
    {
        return second is ConstantExpression { Value: false } ? first
            : first is ConstantExpression { Value: false } ? second
            : Expression.OrElse(first, second);
    }

    public static Expression Test(Expression condition)
    {
        return condition.Type == typeof(bool)
            ? condition
            : Expression.Equal(
                As(condition, typeof(bool?)),
                Expression.Constant(true, typeof(bool?))
            );
    }

    public static Expression All(IEnumerable<Expression> conditions)
    {
        return conditions.DefaultIfEmpty(Expression.Constant(true)).Aggregate(
            Expression.AndAlso
        );
    }

    // Finds a conditional in a tree, outside lambdas.
    private sealed class Conditionals : ExpressionVisitor
    {
        private bool _found;

        public static bool In(Expression node)
        {
            Conditionals finder = new();
            _ = finder.Visit(node);
            return finder._found;
        }

        public override Expression? Visit(Expression? node)
        {
            return _found ? node : base.Visit(node);
        }

        protected override Expression VisitConditional(ConditionalExpression node)
        {
            _found = true;
            return node;
        }

        protected override Expression VisitLambda<T>(Expression<T> node)
        {
            return node;
        }
    }
}
