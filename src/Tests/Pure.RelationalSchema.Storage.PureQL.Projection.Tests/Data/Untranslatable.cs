using System.Linq.Expressions;
using System.Reflection;
using Pure.Primitives.Abstractions.String;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// The nodes of a tree a SQL provider could not translate. A translatable tree
// holds standard query operators (Enumerable ones only over a group or a
// list, never over a query); members and methods of the BCL's value types,
// strings and Math; group keys; the read of a stored cell; value tuples;
// constants of values, stored tables and columns; captured parameters; and, in its last
// Select, the result rows. Nothing is invoked, compiled or run as a block.
internal sealed class Untranslatable : ExpressionVisitor
{
    private static readonly Assembly CoreLibrary = typeof(object).Assembly;

    private static readonly HashSet<ExpressionType> Operations =
    [
        ExpressionType.Constant,
        ExpressionType.Parameter,
        ExpressionType.Lambda,
        ExpressionType.Quote,
        ExpressionType.Convert,
        ExpressionType.ConvertChecked,
        ExpressionType.Not,
        ExpressionType.Negate,
        ExpressionType.Add,
        ExpressionType.AddChecked,
        ExpressionType.Subtract,
        ExpressionType.SubtractChecked,
        ExpressionType.Multiply,
        ExpressionType.MultiplyChecked,
        ExpressionType.Divide,
        ExpressionType.Modulo,
        ExpressionType.Equal,
        ExpressionType.NotEqual,
        ExpressionType.GreaterThan,
        ExpressionType.GreaterThanOrEqual,
        ExpressionType.LessThan,
        ExpressionType.LessThanOrEqual,
        ExpressionType.AndAlso,
        ExpressionType.OrElse,
        ExpressionType.Coalesce,
        ExpressionType.Conditional,
        ExpressionType.MemberAccess,
        ExpressionType.Call,
        ExpressionType.New,
        ExpressionType.NewArrayInit,
        ExpressionType.Default,
    ];

    private static readonly HashSet<string> ClientSide =
    [
        nameof(Enumerable.AsEnumerable),
        nameof(Enumerable.ToList),
        nameof(Enumerable.ToArray),
        nameof(Queryable.AsQueryable),
    ];

    private readonly List<string> _found = [];

    private Untranslatable() { }

    public static IReadOnlyList<string> In(Expression expression)
    {
        Untranslatable untranslatable = new();
        _ = untranslatable.Visit(expression);
        return untranslatable._found;
    }

    public override Expression? Visit(Expression? node)
    {
        if (node is not null && !Operations.Contains(node.NodeType))
        {
            _found.Add($"{node.NodeType} node");
        }

        return base.Visit(node);
    }

    protected override Expression VisitMethodCall(MethodCallExpression node)
    {
        MethodInfo method = node.Method;
        Type owner = method.DeclaringType!;

        bool translatable =
            !ClientSide.Contains(method.Name)
            && (
                owner == typeof(Queryable)
                || (
                    owner == typeof(Enumerable)
                    && !typeof(IQueryable).IsAssignableFrom(node.Arguments[0].Type)
                )
                || (
                    owner.Assembly == CoreLibrary
                    && !owner.IsAssignableTo(typeof(Delegate))
                )
                || (
                    method.Name == "get_Item"
                    && owner == typeof(IReadOnlyDictionary<IColumn, ICell>)
                )
            );

        if (!translatable)
        {
            _found.Add($"call of {owner.Name}.{method.Name}");
        }

        return base.VisitMethodCall(node);
    }

    protected override Expression VisitConstant(ConstantExpression node)
    {
        object? value = node.Value;
        Type type = Nullable.GetUnderlyingType(node.Type) ?? node.Type;
        Type item = type.IsArray ? type.GetElementType()! : type;
        item = Nullable.GetUnderlyingType(item) ?? item;

        bool translatable =
            value is null
            || item.IsPrimitive
            || item.IsEnum
            || item == typeof(decimal)
            || item == typeof(string)
            || item == typeof(DateOnly)
            || item == typeof(TimeOnly)
            || item == typeof(DateTime)
            || item == typeof(TimeSpan)
            || item == typeof(Guid)
            || value is IStoredTableDataSet
            || value is IColumn
            || value is IReadOnlyList<IColumn>
            || value is IQueryable<int>
            || (value is StringComparer comparer && comparer == StringComparer.Ordinal)
            || value
                .GetType()
                .Name.StartsWith("QueryParameter", StringComparison.Ordinal);

        if (!translatable)
        {
            _found.Add($"constant of {value!.GetType().Name}");
        }

        return base.VisitConstant(node);
    }

    protected override Expression VisitNew(NewExpression node)
    {
        Type type = node.Type;

        if (
            !type.Name.StartsWith("ValueTuple", StringComparison.Ordinal)
            && !(type.IsValueType && type.Assembly == CoreLibrary)
            && type.Name != "ResultRow"
        )
        {
            _found.Add($"new {type.Name}");
        }

        return base.VisitNew(node);
    }

    protected override Expression VisitMember(MemberExpression node)
    {
        Type owner = node.Member.DeclaringType!;

        bool translatable =
            owner.Assembly == CoreLibrary
            || (
                owner.IsGenericType
                && owner.GetGenericTypeDefinition() == typeof(IGrouping<,>)
            )
            || owner == typeof(IRow)
            || owner == typeof(ICell)
            || owner == typeof(IString)
            || owner.Name.StartsWith("QueryParameter", StringComparison.Ordinal);

        if (!translatable)
        {
            _found.Add($"member {owner.Name}.{node.Member.Name}");
        }

        return base.VisitMember(node);
    }
}
