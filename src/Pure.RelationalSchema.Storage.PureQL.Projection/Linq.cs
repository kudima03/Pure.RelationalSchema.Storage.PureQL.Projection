using System.Collections;
using System.Collections.Concurrent;
using System.Linq.Expressions;
using System.Reflection;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The standard query operators the tree is made of. A call over an
// IQueryable is a Queryable call with quoted lambdas, as a LINQ provider
// expects; a call over a group's rows or a list is the Enumerable
// counterpart, as C# writes it inside a lambda.
internal static class Linq
{
    private static readonly MethodInfo WhereMethod = Definition<
        Func<IQueryable<object>, Expression<Func<object, bool>>, IQueryable<object>>
    >(Queryable.Where);

    private static readonly MethodInfo SelectMethod = Definition<
        Func<IQueryable<object>, Expression<Func<object, object>>, IQueryable<object>>
    >(Queryable.Select);

    private static readonly MethodInfo SelectManyMethod = Definition<
        Func<
            IQueryable<object>,
            Expression<Func<object, IEnumerable<object>>>,
            Expression<Func<object, object, object>>,
            IQueryable<object>
        >
    >(Queryable.SelectMany);

    private static readonly MethodInfo JoinMethod = Definition<
        Func<
            IQueryable<object>,
            IEnumerable<object>,
            Expression<Func<object, object>>,
            Expression<Func<object, object>>,
            Expression<Func<object, object, object>>,
            IQueryable<object>
        >
    >(Queryable.Join);

    private static readonly MethodInfo GroupJoinMethod = Definition<
        Func<
            IQueryable<object>,
            IEnumerable<object>,
            Expression<Func<object, object>>,
            Expression<Func<object, object>>,
            Expression<Func<object, IEnumerable<object>, object>>,
            IQueryable<object>
        >
    >(Queryable.GroupJoin);

    private static readonly MethodInfo DefaultIfEmptyMethod = Definition<
        Func<IQueryable<object>, IQueryable<object?>>
    >(Queryable.DefaultIfEmpty);

    private static readonly MethodInfo ConcatMethod = Definition<
        Func<IQueryable<object>, IEnumerable<object>, IQueryable<object>>
    >(Queryable.Concat);

    private static readonly MethodInfo AnyMethod = Definition<
        Func<IQueryable<object>, bool>
    >(Queryable.Any);

    private static readonly MethodInfo AnyWhereMethod = Definition<
        Func<IQueryable<object>, Expression<Func<object, bool>>, bool>
    >(Queryable.Any);

    private static readonly MethodInfo AllMethod = Definition<
        Func<IQueryable<object>, Expression<Func<object, bool>>, bool>
    >(Queryable.All);

    private static readonly MethodInfo ContainsMethod = Definition<
        Func<IQueryable<object>, object, bool>
    >(Queryable.Contains);

    private static readonly MethodInfo LongCountMethod = Definition<
        Func<IQueryable<object>, long>
    >(Queryable.LongCount);

    private static readonly MethodInfo LongCountWhereMethod = Definition<
        Func<IQueryable<object>, Expression<Func<object, bool>>, long>
    >(Queryable.LongCount);

    private static readonly MethodInfo SumIntegerMethod = Definition<
        Func<IQueryable<object>, Expression<Func<object, long?>>, long?>
    >(Queryable.Sum);

    private static readonly MethodInfo SumDecimalMethod = Definition<
        Func<IQueryable<object>, Expression<Func<object, decimal?>>, decimal?>
    >(Queryable.Sum);

    private static readonly MethodInfo AverageMethod = Definition<
        Func<IQueryable<object>, Expression<Func<object, decimal?>>, decimal?>
    >(Queryable.Average);

    private static readonly MethodInfo MinMethod = Definition<
        Func<IQueryable<object>, Expression<Func<object, object>>, object?>
    >(Queryable.Min);

    private static readonly MethodInfo MaxMethod = Definition<
        Func<IQueryable<object>, Expression<Func<object, object>>, object?>
    >(Queryable.Max);

    private static readonly MethodInfo MinComparedMethod = Definition<
        Func<IQueryable<object>, IComparer<object>, object?>
    >(Queryable.Min);

    private static readonly MethodInfo MaxComparedMethod = Definition<
        Func<IQueryable<object>, IComparer<object>, object?>
    >(Queryable.Max);

    private static readonly MethodInfo GroupByMethod = Definition<
        Func<
            IQueryable<object>,
            Expression<Func<object, object>>,
            IQueryable<IGrouping<object, object>>
        >
    >(Queryable.GroupBy);

    private static readonly MethodInfo OrderByMethod = Definition<
        Func<
            IQueryable<object>,
            Expression<Func<object, object>>,
            IOrderedQueryable<object>
        >
    >(Queryable.OrderBy);

    private static readonly MethodInfo OrderByComparedMethod = Definition<
        Func<
            IQueryable<object>,
            Expression<Func<object, object>>,
            IComparer<object>?,
            IOrderedQueryable<object>
        >
    >(Queryable.OrderBy);

    private static readonly MethodInfo OrderByDescendingMethod = Definition<
        Func<
            IQueryable<object>,
            Expression<Func<object, object>>,
            IOrderedQueryable<object>
        >
    >(Queryable.OrderByDescending);

    private static readonly MethodInfo OrderByDescendingComparedMethod = Definition<
        Func<
            IQueryable<object>,
            Expression<Func<object, object>>,
            IComparer<object>?,
            IOrderedQueryable<object>
        >
    >(Queryable.OrderByDescending);

    private static readonly MethodInfo ThenByMethod = Definition<
        Func<
            IOrderedQueryable<object>,
            Expression<Func<object, object>>,
            IOrderedQueryable<object>
        >
    >(Queryable.ThenBy);

    private static readonly MethodInfo ThenByComparedMethod = Definition<
        Func<
            IOrderedQueryable<object>,
            Expression<Func<object, object>>,
            IComparer<object>?,
            IOrderedQueryable<object>
        >
    >(Queryable.ThenBy);

    private static readonly MethodInfo ThenByDescendingMethod = Definition<
        Func<
            IOrderedQueryable<object>,
            Expression<Func<object, object>>,
            IOrderedQueryable<object>
        >
    >(Queryable.ThenByDescending);

    private static readonly MethodInfo ThenByDescendingComparedMethod = Definition<
        Func<
            IOrderedQueryable<object>,
            Expression<Func<object, object>>,
            IComparer<object>?,
            IOrderedQueryable<object>
        >
    >(Queryable.ThenByDescending);

    private static readonly MethodInfo DistinctByMethod = Definition<
        Func<IQueryable<object>, Expression<Func<object, object>>, IQueryable<object>>
    >(Queryable.DistinctBy);

    private static readonly MethodInfo SkipMethod = Definition<
        Func<IQueryable<object>, int, IQueryable<object>>
    >(Queryable.Skip);

    private static readonly MethodInfo TakeMethod = Definition<
        Func<IQueryable<object>, int, IQueryable<object>>
    >(Queryable.Take);

    private static readonly ConcurrentDictionary<MethodInfo, MethodInfo> Counterparts =
        new();

    public static Expression Where(Expression source, LambdaExpression predicate)
    {
        return Call(WhereMethod, [ElementType(source.Type)], source, predicate);
    }

    public static Expression Select(Expression source, LambdaExpression selector)
    {
        return Call(
            SelectMethod,
            [ElementType(source.Type), selector.ReturnType],
            source,
            selector
        );
    }

    public static Expression SelectMany(
        Expression source,
        LambdaExpression collection,
        LambdaExpression result
    )
    {
        return Call(
            SelectManyMethod,
            [
                ElementType(source.Type),
                ElementType(collection.ReturnType),
                result.ReturnType,
            ],
            source,
            collection,
            result
        );
    }

    // The pairs of an outer and an inner row whose keys are equal.
    public static Expression Join(
        Expression outer,
        Expression inner,
        LambdaExpression outerKey,
        LambdaExpression innerKey,
        LambdaExpression result
    )
    {
        return Call(
            JoinMethod,
            [
                ElementType(outer.Type),
                ElementType(inner.Type),
                outerKey.ReturnType,
                result.ReturnType,
            ],
            outer,
            inner,
            outerKey,
            innerKey,
            result
        );
    }

    // Each outer row with the inner rows whose keys equal its key.
    public static Expression GroupJoin(
        Expression outer,
        Expression inner,
        LambdaExpression outerKey,
        LambdaExpression innerKey,
        LambdaExpression result
    )
    {
        return Call(
            GroupJoinMethod,
            [
                ElementType(outer.Type),
                ElementType(inner.Type),
                outerKey.ReturnType,
                result.ReturnType,
            ],
            outer,
            inner,
            outerKey,
            innerKey,
            result
        );
    }

    public static Expression DefaultIfEmpty(Expression source)
    {
        return Call(DefaultIfEmptyMethod, [ElementType(source.Type)], source);
    }

    public static Expression Concat(Expression first, Expression second)
    {
        return Call(ConcatMethod, [ElementType(first.Type)], first, second);
    }

    public static Expression Any(Expression source)
    {
        return Call(AnyMethod, [ElementType(source.Type)], source);
    }

    public static Expression Any(Expression source, LambdaExpression predicate)
    {
        return Call(AnyWhereMethod, [ElementType(source.Type)], source, predicate);
    }

    public static Expression All(Expression source, LambdaExpression predicate)
    {
        return Call(AllMethod, [ElementType(source.Type)], source, predicate);
    }

    public static Expression Contains(Expression source, Expression value)
    {
        return Call(ContainsMethod, [ElementType(source.Type)], source, value);
    }

    public static Expression LongCount(Expression source)
    {
        return Call(LongCountMethod, [ElementType(source.Type)], source);
    }

    public static Expression LongCount(Expression source, LambdaExpression predicate)
    {
        return Call(LongCountWhereMethod, [ElementType(source.Type)], source, predicate);
    }

    public static Expression Sum(Expression source, LambdaExpression selector)
    {
        return Call(
            selector.ReturnType == typeof(long?) ? SumIntegerMethod : SumDecimalMethod,
            [ElementType(source.Type)],
            source,
            selector
        );
    }

    public static Expression Average(Expression source, LambdaExpression selector)
    {
        return Call(AverageMethod, [ElementType(source.Type)], source, selector);
    }

    public static Expression Min(Expression source, LambdaExpression selector)
    {
        return Call(
            MinMethod,
            [ElementType(source.Type), selector.ReturnType],
            source,
            selector
        );
    }

    public static Expression Max(Expression source, LambdaExpression selector)
    {
        return Call(
            MaxMethod,
            [ElementType(source.Type), selector.ReturnType],
            source,
            selector
        );
    }

    public static Expression Min(Expression source, Expression comparer)
    {
        return Call(MinComparedMethod, [ElementType(source.Type)], source, comparer);
    }

    public static Expression Max(Expression source, Expression comparer)
    {
        return Call(MaxComparedMethod, [ElementType(source.Type)], source, comparer);
    }

    public static Expression GroupBy(Expression source, LambdaExpression key)
    {
        return Call(
            GroupByMethod,
            [ElementType(source.Type), key.ReturnType],
            source,
            key
        );
    }

    // The first key orders, every later key breaks the ties of the previous.
    public static Expression Order(
        Expression source,
        LambdaExpression key,
        bool descending,
        bool first,
        Expression? comparer
    )
    {
        MethodInfo method = (first, descending, comparer is null) switch
        {
            (true, false, true) => OrderByMethod,
            (true, false, false) => OrderByComparedMethod,
            (true, true, true) => OrderByDescendingMethod,
            (true, true, false) => OrderByDescendingComparedMethod,
            (false, false, true) => ThenByMethod,
            (false, false, false) => ThenByComparedMethod,
            (false, true, true) => ThenByDescendingMethod,
            (false, true, false) => ThenByDescendingComparedMethod,
        };

        return comparer is null
            ? Call(method, [ElementType(source.Type), key.ReturnType], source, key)
            : Call(
                method,
                [ElementType(source.Type), key.ReturnType],
                source,
                key,
                comparer
            );
    }

    public static Expression DistinctBy(Expression source, LambdaExpression key)
    {
        return Call(
            DistinctByMethod,
            [ElementType(source.Type), key.ReturnType],
            source,
            key
        );
    }

    public static Expression Skip(Expression source, Expression count)
    {
        return Call(SkipMethod, [ElementType(source.Type)], source, count);
    }

    public static Expression Take(Expression source, Expression count)
    {
        return Call(TakeMethod, [ElementType(source.Type)], source, count);
    }

    public static bool IsQueryable(Type type)
    {
        return typeof(IQueryable).IsAssignableFrom(type);
    }

    public static bool Is(MethodCallExpression call, string name)
    {
        return call.Method.DeclaringType == typeof(Queryable) && call.Method.Name == name;
    }

    public static Type ElementType(Type sequence)
    {
        return (
            sequence.IsGenericType
            && sequence.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                ? sequence
                : sequence
                    .GetInterfaces()
                    .First(type =>
                        type.IsGenericType
                        && type.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                    )
        ).GetGenericArguments()[0];
    }

    // A lambda as the argument of a Queryable call: unquoted.
    public static LambdaExpression Lambda(Expression argument)
    {
        return (LambdaExpression)(
            argument is UnaryExpression { NodeType: ExpressionType.Quote } quote
                ? quote.Operand
                : argument
        );
    }

    // A Queryable call as the Enumerable call over the same arguments, for a
    // source that is no longer an IQueryable: the counterpart method, with
    // the lambdas unquoted.
    public static Expression OverEnumerable(
        MethodInfo queryable,
        IReadOnlyList<Expression> arguments
    )
    {
        MethodInfo definition = Counterparts.GetOrAdd(
            queryable.GetGenericMethodDefinition(),
            Counterpart
        );

        return Expression.Call(
            definition.MakeGenericMethod(queryable.GetGenericArguments()),
            [.. arguments.Select(Unquoted)]
        );
    }

    private static Expression Unquoted(Expression argument)
    {
        return argument is UnaryExpression { NodeType: ExpressionType.Quote } quote
            ? quote.Operand
            : argument;
    }

    private static MethodInfo Definition<T>(T method)
        where T : Delegate
    {
        return method.Method.GetGenericMethodDefinition();
    }

    private static Expression Call(
        MethodInfo queryable,
        Type[] typeArguments,
        Expression source,
        params Expression[] arguments
    )
    {
        bool isQueryable = IsQueryable(source.Type);
        MethodInfo definition = isQueryable
            ? queryable
            : Counterparts.GetOrAdd(queryable, Counterpart);

        return Expression.Call(
            definition.MakeGenericMethod(typeArguments),
            [
                source,
                .. arguments.Select(argument =>
                    isQueryable && argument is LambdaExpression
                        ? Expression.Quote(argument)
                        : argument
                ),
            ]
        );
    }

    // The Enumerable method a Queryable method stands for: the same name and
    // generic arity, with IQueryable read as IEnumerable and Expression<F> as F.
    private static MethodInfo Counterpart(MethodInfo queryable)
    {
        Type[] parameters = [.. queryable.GetParameters().Select(p => p.ParameterType)];

        return typeof(Enumerable)
            .GetMethods(BindingFlags.Public | BindingFlags.Static)
            .Single(method =>
                method.Name == queryable.Name
                && method.IsGenericMethodDefinition
                && method.GetGenericArguments().Length
                    == queryable.GetGenericArguments().Length
                && method.GetParameters().Length == parameters.Length
                && method
                    .GetParameters()
                    .Zip(parameters)
                    .All(pair => Same(pair.Second, pair.First.ParameterType))
            );
    }

    private static bool Same(Type queryable, Type enumerable)
    {
        Type type = queryable.IsGenericType
            && queryable.GetGenericTypeDefinition() == typeof(Expression<>)
            ? queryable.GetGenericArguments()[0]
            : queryable;

        if (type.IsGenericParameter || enumerable.IsGenericParameter)
        {
            return type.IsGenericParameter
                && enumerable.IsGenericParameter
                && type.GenericParameterPosition == enumerable.GenericParameterPosition;
        }

        if (!type.IsGenericType)
        {
            return type == enumerable
                || (type == typeof(IQueryable) && enumerable == typeof(IEnumerable));
        }

        if (
            !enumerable.IsGenericType
            || type.GetGenericArguments().Length
                != enumerable.GetGenericArguments().Length
        )
        {
            return false;
        }

        Type definition = type.GetGenericTypeDefinition();
        Type expected =
            definition == typeof(IQueryable<>) ? typeof(IEnumerable<>)
            : definition == typeof(IOrderedQueryable<>) ? typeof(IOrderedEnumerable<>)
            : definition;

        return enumerable.GetGenericTypeDefinition() == expected
            && type.GetGenericArguments()
                .Zip(enumerable.GetGenericArguments())
                .All(pair => Same(pair.First, pair.Second));
    }
}
