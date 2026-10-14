using System.Linq.Expressions;
using System.Reflection;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Date and time arithmetic, lifted like every operator. dateDiffDays and the
// *DiffSeconds operators are left - right; timeAddSeconds wraps around
// midnight. Seconds are rounded half away from zero to the microsecond, the
// specification's precision. A date or datetime outside the representable
// years raises and fails the query.
internal static class Temporal
{
    private static readonly MethodInfo AddDaysMethod = typeof(DateOnly).GetMethod(
        nameof(DateOnly.AddDays),
        [typeof(int)]
    )!;

    private static readonly MethodInfo TimeAddMethod = typeof(TimeOnly).GetMethod(
        nameof(TimeOnly.Add),
        [typeof(TimeSpan)]
    )!;

    private static readonly MethodInfo FromTicksMethod = typeof(TimeSpan).GetMethod(
        nameof(TimeSpan.FromTicks),
        [typeof(long)]
    )!;

    private static readonly MethodInfo AddTicksMethod = typeof(DateTime).GetMethod(
        nameof(DateTime.AddTicks),
        [typeof(long)]
    )!;

    public static Expression DateAddDays(object node, Scope scope)
    {
        return Lifted.Binary(
            Left(node, scope),
            Typed.As(Right(node, scope), typeof(long?)),
            typeof(DateOnly?),
            (date, days) => Expression.Call(
                date,
                AddDaysMethod,
                Expression.ConvertChecked(days, typeof(int))
            )
        );
    }

    public static Expression DateDiffDays(object node, Scope scope)
    {
        return Lifted.Binary(
            Left(node, scope),
            Right(node, scope),
            typeof(long?),
            (left, right) => Expression.Convert(
                Expression.Subtract(
                    Expression.Property(left, nameof(DateOnly.DayNumber)),
                    Expression.Property(right, nameof(DateOnly.DayNumber))
                ),
                typeof(long)
            )
        );
    }

    public static Expression TimeAddSeconds(object node, Scope scope)
    {
        return Lifted.Binary(
            Left(node, scope),
            Arithmetic.Decimal(Right(node, scope)),
            typeof(TimeOnly?),
            (time, seconds) => Expression.Call(
                time,
                TimeAddMethod,
                Expression.Call(FromTicksMethod, Ticks(seconds))
            )
        );
    }

    public static Expression TimeDiffSeconds(object node, Scope scope)
    {
        return Lifted.Binary(
            Left(node, scope),
            Right(node, scope),
            typeof(decimal?),
            (left, right) => Seconds(
                Expression.Subtract(
                    Expression.Property(left, nameof(TimeOnly.Ticks)),
                    Expression.Property(right, nameof(TimeOnly.Ticks))
                )
            )
        );
    }

    public static Expression DatetimeAddSeconds(object node, Scope scope)
    {
        return Lifted.Binary(
            Left(node, scope),
            Arithmetic.Decimal(Right(node, scope)),
            typeof(DateTime?),
            (instant, seconds) => Expression.Call(instant, AddTicksMethod, Ticks(seconds))
        );
    }

    public static Expression DatetimeDiffSeconds(object node, Scope scope)
    {
        return Lifted.Binary(
            Left(node, scope),
            Right(node, scope),
            typeof(decimal?),
            (left, right) => Seconds(
                Expression.Subtract(
                    Expression.Property(left, nameof(DateTime.Ticks)),
                    Expression.Property(right, nameof(DateTime.Ticks))
                )
            )
        );
    }

    // Decimal microseconds, rounded to whole ones, as ticks.
    public static Expression MicrosecondTicks(Expression microseconds)
    {
        return Expression.Multiply(
            Expression.Convert(Arithmetic.AwayFromZero(microseconds), typeof(long)),
            Expression.Constant(TimeSpan.TicksPerMicrosecond)
        );
    }

    public static Expression Microseconds(Expression ticks)
    {
        return Expression.Divide(
            Expression.Convert(ticks, typeof(decimal)),
            Expression.Constant((decimal)TimeSpan.TicksPerMicrosecond)
        );
    }

    private static Expression Ticks(Expression seconds)
    {
        return MicrosecondTicks(
            Expression.Multiply(seconds, Expression.Constant(1_000_000m))
        );
    }

    private static Expression Seconds(Expression ticks)
    {
        return Expression.Divide(
            Expression.Convert(ticks, typeof(decimal)),
            Expression.Constant((decimal)TimeSpan.TicksPerSecond)
        );
    }

    private static Expression Left(object node, Scope scope)
    {
        return scope.Compile(ModelNode.Child(node, "Left"));
    }

    private static Expression Right(object node, Scope scope)
    {
        return scope.Compile(ModelNode.Child(node, "Right"));
    }
}
