using System.Collections;
using System.Globalization;
using System.Linq.Expressions;
using System.Reflection;
using System.Text;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// An expression tree as C#-like text, to assert on its shape. A query
// operator reads as a method of its source (rows.Where(row => ...)), a stored
// table root as [table], a tuple as (a, b), a cast as (type)value, a captured
// parameter as @name; quotes are not shown and every binary operation is
// parenthesized. The read of a stored cell the query declares nullable (null
// for empty text, the text parsed otherwise) reads as row[column], and of one
// it declares non-null (the text parsed, whatever it is) as row[column]!;
// Full keeps them as built.
internal static class TreeText
{
    private static readonly Dictionary<ExpressionType, string> Symbols = new()
    {
        [ExpressionType.Add] = "+",
        [ExpressionType.AddChecked] = "+",
        [ExpressionType.Subtract] = "-",
        [ExpressionType.SubtractChecked] = "-",
        [ExpressionType.Multiply] = "*",
        [ExpressionType.MultiplyChecked] = "*",
        [ExpressionType.Divide] = "/",
        [ExpressionType.Modulo] = "%",
        [ExpressionType.AndAlso] = "&&",
        [ExpressionType.OrElse] = "||",
        [ExpressionType.Equal] = "==",
        [ExpressionType.NotEqual] = "!=",
        [ExpressionType.GreaterThan] = ">",
        [ExpressionType.GreaterThanOrEqual] = ">=",
        [ExpressionType.LessThan] = "<",
        [ExpressionType.LessThanOrEqual] = "<=",
        [ExpressionType.Coalesce] = "??",
    };

    [ThreadStatic]
    private static bool _full;

    public static string Of(Expression expression)
    {
        _full = false;
        return Text(expression);
    }

    public static string Full(Expression expression)
    {
        _full = true;
        return Text(expression);
    }

    private static string Text(Expression? node)
    {
        return node switch
        {
            null => "",
            ConditionalExpression read when !_full && StoredRead(read) is string cell =>
                cell,
            UnaryExpression read when !_full && RequiredRead(read) is string cell =>
                cell + "!",
            MemberExpression read when !_full && CellText(read) is string cell =>
                cell + "!",
            ConstantExpression constant => Constant(constant.Value),
            ParameterExpression parameter => parameter.Name ?? "_",
            LambdaExpression lambda => Lambda(lambda),
            UnaryExpression unary => Unary(unary),
            BinaryExpression binary => Binary(binary),
            ConditionalExpression conditional =>
                $"({Text(conditional.Test)} ? {Text(conditional.IfTrue)} : "
                    + $"{Text(conditional.IfFalse)})",
            MemberExpression member => Member(member),
            MethodCallExpression call => Call(call),
            NewExpression created => New(created),
            NewArrayExpression array =>
                $"new[] {{ {string.Join(", ", array.Expressions.Select(Text))} }}",
            DefaultExpression empty => $"default({TypeName(empty.Type)})",
            _ => throw new NotSupportedException($"{node.NodeType} has no text."),
        };
    }

    // IsNullOrEmpty(row.Cells[column].Value.TextValue) ? null : parse(...)
    private static string? StoredRead(ConditionalExpression conditional)
    {
        return conditional.Test is MethodCallExpression
        {
            Method.Name: nameof(string.IsNullOrEmpty),
            Arguments: [Expression text],
        }
            ? CellText(text)
            : null;
    }

    // (type?)parse(row.Cells[column].Value.TextValue), the parse of a type's
    // Parse method converted to the nullable type; a string is the text itself.
    private static string? RequiredRead(UnaryExpression conversion)
    {
        return conversion.NodeType == ExpressionType.Convert
            && conversion.Operand is MethodCallExpression
            {
                Method.Name: "Parse",
                Object: null,
                Arguments: [Expression text, ..],
            }
            ? CellText(text)
            : null;
    }

    // row[column] for row.Cells[column].Value.TextValue.
    private static string? CellText(Expression text)
    {
        return text
            is MemberExpression
        {
            Expression: MemberExpression
            {
                Expression: MethodCallExpression
                {
                    Method.Name: "get_Item",
                    Object: MemberExpression { Expression: Expression row },
                    Arguments: [ConstantExpression { Value: IColumn column }],
                },
            },
        }
            ? $"{Text(row)}[{column.Name.TextValue}]"
            : null;
    }

    private static string Constant(object? value)
    {
        return value switch
        {
            null => "null",
            IStoredTableDataSet dataset => $"[{dataset.TableSchema.Name.TextValue}]",
            IColumn column => column.Name.TextValue,
            IReadOnlyList<IColumn> columns =>
                $"[{string.Join(", ", columns.Select(column => column.Name.TextValue))}]",
            IQueryable => "[one row]",
            StringComparer comparer when comparer == StringComparer.Ordinal => "Ordinal",
            string text => $"\"{text}\"",
            bool flag => flag ? "true" : "false",
            decimal number => number.ToString(CultureInfo.InvariantCulture) + "m",
            long number => number.ToString(CultureInfo.InvariantCulture) + "L",
            Enum choice => $"{choice.GetType().Name}.{choice}",
            IFormattable formattable => formattable.ToString(
                null,
                CultureInfo.InvariantCulture
            ),
            IEnumerable items =>
                $"[{string.Join(", ", items.Cast<object?>().Select(Constant))}]",
            _ => value.GetType().Name,
        };
    }

    private static string Lambda(LambdaExpression lambda)
    {
        string parameters = lambda.Parameters.Count == 1
            ? Text(lambda.Parameters[0])
            : $"({string.Join(", ", lambda.Parameters.Select(Text))})";

        return $"{parameters} => {Text(lambda.Body)}";
    }

    private static string Unary(UnaryExpression unary)
    {
        string operand = Text(unary.Operand);

        return unary.NodeType is ExpressionType.Quote ? operand
            : unary.NodeType is ExpressionType.Not ? $"!{operand}"
            : unary.NodeType is ExpressionType.Negate ? $"-{operand}"
            : unary.NodeType is ExpressionType.Convert
                ? $"({TypeName(unary.Type)}){operand}"
            : unary.NodeType is ExpressionType.ConvertChecked
                ? $"checked(({TypeName(unary.Type)}){operand})"
            : throw new NotSupportedException($"{unary.NodeType} has no text.");
    }

    private static string Binary(BinaryExpression binary)
    {
        string symbol = Symbols.TryGetValue(binary.NodeType, out string? found)
            ? found
            : throw new NotSupportedException($"{binary.NodeType} has no text.");

        return $"({Text(binary.Left)} {symbol} {Text(binary.Right)})";
    }

    private static string Member(MemberExpression member)
    {
        if (
            member.Expression is ConstantExpression { Value: object owner }
            && owner.GetType().Name.StartsWith("QueryParameter", StringComparison.Ordinal)
        )
        {
            return "@" + owner.GetType().GetProperty("Name")!.GetValue(owner);
        }

        string owned = member.Expression is null
            ? member.Member.DeclaringType!.Name
            : Text(member.Expression);

        return $"{owned}.{member.Member.Name}";
    }

    private static string Call(MethodCallExpression call)
    {
        MethodInfo method = call.Method;

        if (method.Name == "get_Item")
        {
            return $"{Text(call.Object)}[{Arguments(call.Arguments)}]";
        }

        if (
            method.DeclaringType == typeof(Queryable)
            || method.DeclaringType == typeof(Enumerable)
        )
        {
            return $"{Text(call.Arguments[0])}.{method.Name}"
                + $"({Arguments(call.Arguments.Skip(1))})";
        }

        string owner = call.Object is null
            ? method.DeclaringType!.Name
            : Text(call.Object);

        return $"{owner}.{method.Name}({Arguments(call.Arguments)})";
    }

    private static string New(NewExpression created)
    {
        return created.Type.Name.StartsWith("ValueTuple", StringComparison.Ordinal)
            ? $"({Arguments(created.Arguments)})"
            : $"new {TypeName(created.Type)}({Arguments(created.Arguments)})";
    }

    private static string Arguments(IEnumerable<Expression> arguments)
    {
        return string.Join(", ", arguments.Select(Text));
    }

    private static string TypeName(Type type)
    {
        Type? underlying = Nullable.GetUnderlyingType(type);

        if (underlying is not null)
        {
            return TypeName(underlying) + "?";
        }

        string name = type switch
        {
            _ when type == typeof(long) => "long",
            _ when type == typeof(int) => "int",
            _ when type == typeof(decimal) => "decimal",
            _ when type == typeof(double) => "double",
            _ when type == typeof(bool) => "bool",
            _ when type == typeof(string) => "string",
            _ when type == typeof(object) => "object",
            _ => type.Name,
        };

        if (!type.IsGenericType)
        {
            return name;
        }

        StringBuilder generic = new(name[..name.IndexOf('`', StringComparison.Ordinal)]);
        _ = generic.Append('<');
        _ = generic.Append(
            string.Join(", ", type.GetGenericArguments().Select(TypeName))
        );
        _ = generic.Append('>');
        return generic.ToString();
    }
}
