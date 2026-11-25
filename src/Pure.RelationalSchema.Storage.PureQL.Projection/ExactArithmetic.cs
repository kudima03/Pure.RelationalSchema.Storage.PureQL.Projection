using System.Linq.Expressions;
using System.Reflection;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The arithmetic of a tree as the specification defines it. A tree names
// decimal operations as C# does (+, -, *, /, Math.Round of a quotient, Sum,
// Average), which a provider translates to its store's exact arithmetic;
// computed by .NET they would keep 29 digits where 96 bits hold them, round
// half to even, reach 7.9 x 10^28, round a quotient before it is rounded to
// a whole number, and fail on a partial sum. So a tree that is computed
// here, when it runs in memory or when a constant is folded, computes
// - a decimal sum, difference, product and quotient exactly, rounded once,
//   half away from zero, to 28 digits (PureQLMath);
// - Math.Round of a quotient from the exact quotient, in one rounding;
// - the power of ten a number of digits scales by without a double;
// - a remainder of the least integer by -1 as 0;
// - Sum and Average from the exact sum of the values, in any order.
internal class ExactArithmetic : ExpressionVisitor
{
    private static readonly MethodInfo PowerMethod = Functions.Method(
        typeof(Math),
        nameof(Math.Pow),
        typeof(double),
        typeof(double)
    );

    private static readonly MethodInfo RoundMethod = Functions.Method(
        typeof(Math),
        nameof(Math.Round),
        typeof(decimal),
        typeof(MidpointRounding)
    );

    private static readonly MethodInfo SelectMethod = typeof(Enumerable)
        .GetMethods()
        .Single(method =>
            method.Name == nameof(Enumerable.Select)
            && method.GetParameters()[1].ParameterType.GetGenericArguments().Length == 2
        );

    protected ExactArithmetic() { }

    public static Expression Rewrite(Expression node)
    {
        return new ExactArithmetic().Visit(node);
    }

    // Math.Round(dividend / divisor, mode) over decimals.
    public static bool IsRoundedQuotient(MethodCallExpression call)
    {
        return call.Method == RoundMethod
            && call.Arguments[0] is BinaryExpression { NodeType: ExpressionType.Divide };
    }

    public static bool IsPowerOfTen(MethodCallExpression call)
    {
        return call.Method == PowerMethod;
    }

    protected override Expression VisitBinary(BinaryExpression node)
    {
        Expression left = Visit(node.Left);
        Expression right = Visit(node.Right);
        Type type = Nullable.GetUnderlyingType(left.Type) ?? left.Type;
        string? name =
            type == typeof(long)
                ? node.NodeType == ExpressionType.Modulo ? nameof(PureQLMath.Modulo) : null
            : type != typeof(decimal) ? null
            : node.NodeType == ExpressionType.Add ? nameof(PureQLMath.Add)
            : node.NodeType == ExpressionType.Subtract ? nameof(PureQLMath.Subtract)
            : node.NodeType == ExpressionType.Multiply ? nameof(PureQLMath.Multiply)
            : node.NodeType == ExpressionType.Divide ? nameof(PureQLMath.Divide)
            : null;

        return name is null
            ? node.Update(left, VisitAndConvert(node.Conversion, nameof(VisitBinary)), right)
            : Expression.MakeBinary(
                node.NodeType,
                left,
                right,
                node.IsLiftedToNull,
                Functions.Method(typeof(PureQLMath), name, type, type)
            );
    }

    protected override Expression VisitUnary(UnaryExpression node)
    {
        return node.NodeType == ExpressionType.Convert
            && node.Type == typeof(decimal)
            && node.Operand is MethodCallExpression power
            && IsPowerOfTen(power)
            && power.Arguments[1]
                is UnaryExpression { NodeType: ExpressionType.Convert } exponent
            && exponent.Operand.Type == typeof(long)
            ? Functions.Call(
                typeof(PureQLMath),
                nameof(PureQLMath.PowerOfTen),
                Visit(exponent.Operand)
            )
            : base.VisitUnary(node);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        if (IsRoundedQuotient(node))
        {
            BinaryExpression quotient = (BinaryExpression)node.Arguments[0];

            return Functions.Call(
                typeof(PureQLMath),
                nameof(PureQLMath.RoundQuotient),
                Visit(quotient.Left),
                Visit(quotient.Right),
                node.Arguments[1]
            );
        }

        return base.VisitMethodCall(node) is MethodCallExpression call
            ? Exactly(call)
            : throw new InvalidOperationException("A call is visited as a call.");
    }

    // Enumerable.Sum and Enumerable.Average of integers or decimals over a
    // selector, as the exact aggregate over the selected values.
    protected static Expression Exactly(MethodCallExpression call)
    {
        MethodInfo method = call.Method;

        return
            method.DeclaringType != typeof(Enumerable)
            || method.Name is not (nameof(Enumerable.Sum) or nameof(Enumerable.Average))
            || call.Arguments.Count != 2
            || !method.IsGenericMethod
            || (method.ReturnType != typeof(long?) && method.ReturnType != typeof(decimal?))
            || call.Arguments[1].Type.GetGenericArguments()[^1] != method.ReturnType
            ? call
            : Expression.Call(
                Functions.Method(
                    typeof(PureQLAggregates),
                    method.Name,
                    typeof(IEnumerable<>).MakeGenericType(method.ReturnType)
                ),
                Expression.Call(
                    SelectMethod.MakeGenericMethod(
                        method.GetGenericArguments()[0],
                        method.ReturnType
                    ),
                    call.Arguments
                )
            );
    }
}
