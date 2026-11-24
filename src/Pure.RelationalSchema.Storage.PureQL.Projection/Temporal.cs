using System.Linq.Expressions;
using System.Reflection;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Date and time operators, lifted like every operator, also over a zone.
// dateDiffDays and the *DiffSeconds operators are left - right;
// timeAddSeconds wraps around midnight. Seconds are rounded half away from
// zero to the microsecond, the specification's precision. Months are added
// with the day clamped to the end of a shorter month. A datetime is an
// instant: an operator that reads a calendar or a clock from it reads it in
// UTC, or in the time zone it is given, and one that builds an instant from
// a local date and time gives the first instant at which the zone's clocks
// read them or later (PureQLTime). A date or datetime outside the
// representable years, and a part out of range, raise and fail the query.
internal static class Temporal
{
    private static readonly string[] DateParts = ["Year", "Month", "Day"];

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
            Operand(node, scope),
            Arithmetic.Integer(scope.Compile(ModelNode.Child(node, "Days"))),
            typeof(DateOnly?),
            (date, days) => Expression.Call(
                date,
                AddDaysMethod,
                Expression.ConvertChecked(days, typeof(int))
            )
        );
    }

    public static Expression DateAddMonths(object node, Scope scope)
    {
        return Lifted.Binary(
            Operand(node, scope),
            Arithmetic.Integer(scope.Compile(ModelNode.Child(node, "Months"))),
            typeof(DateOnly?),
            (date, months) => Functions.Call(
                date,
                nameof(DateOnly.AddMonths),
                Expression.ConvertChecked(months, typeof(int))
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

    public static Expression DateDiffMonths(object node, Scope scope)
    {
        return Lifted.Binary(
            Left(node, scope),
            Right(node, scope),
            typeof(long?),
            (left, right) => Functions.Call(
                typeof(PureQLTime),
                nameof(PureQLTime.DiffMonths),
                left,
                right
            )
        );
    }

    public static Expression TimeAddSeconds(object node, Scope scope)
    {
        return Lifted.Binary(
            Operand(node, scope),
            Arithmetic.Decimal(scope.Compile(ModelNode.Child(node, "Seconds"))),
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
            Operand(node, scope),
            Arithmetic.Decimal(scope.Compile(ModelNode.Child(node, "Seconds"))),
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

    // In UTC the instant moves as DateTime.AddMonths moves it; with a zone
    // the local date moves and the local time of day is kept.
    public static Expression DatetimeAddMonths(object node, Scope scope)
    {
        return Zoned(
            node,
            scope,
            typeof(DateTime?),
            [
                Operand(node, scope),
                Arithmetic.Integer(scope.Compile(ModelNode.Child(node, "Months"))),
            ],
            values => Functions.Call(
                values[0],
                nameof(DateTime.AddMonths),
                Expression.ConvertChecked(values[1], typeof(int))
            ),
            values => Functions.Call(
                typeof(PureQLTime),
                nameof(PureQLTime.AddMonths),
                [.. values]
            )
        );
    }

    public static Expression DatetimeDiffMonths(object node, Scope scope)
    {
        Expression difference(IReadOnlyList<Expression> values)
        {
            return Functions.Call(
                typeof(PureQLTime),
                nameof(PureQLTime.DiffMonths),
                [.. values]
            );
        }

        return Zoned(
            node,
            scope,
            typeof(long?),
            [Left(node, scope), Right(node, scope)],
            difference,
            difference
        );
    }

    // A part out of range fails as the DateOnly constructor does.
    public static Expression DateFromParts(object node, Scope scope)
    {
        return Lifted.All(
            [
                .. DateParts.Select(part =>
                    Arithmetic.Integer(scope.Compile(ModelNode.Child(node, part)))
                ),
            ],
            typeof(DateOnly?),
            values => Expression.New(
                typeof(DateOnly).GetConstructor([typeof(int), typeof(int), typeof(int)])!,
                values.Select(value => Expression.ConvertChecked(value, typeof(int)))
            )
        );
    }

    public static Expression TimeFromParts(object node, Scope scope)
    {
        return Lifted.All(
            [
                Arithmetic.Integer(scope.Compile(ModelNode.Child(node, "Hour"))),
                Arithmetic.Integer(scope.Compile(ModelNode.Child(node, "Minute"))),
                Arithmetic.Decimal(scope.Compile(ModelNode.Child(node, "Second"))),
            ],
            typeof(TimeOnly?),
            values => Functions.Call(
                typeof(PureQLTime),
                nameof(PureQLTime.TimeFromParts),
                [.. values]
            )
        );
    }

    public static Expression DatetimeFromParts(object node, Scope scope)
    {
        return Zoned(
            node,
            scope,
            typeof(DateTime?),
            [
                scope.Compile(ModelNode.Child(node, "Date")),
                scope.Compile(ModelNode.Child(node, "Time")),
            ],
            values => Instant(values[0], values[1]),
            values => Functions.Call(
                typeof(PureQLTime),
                nameof(PureQLTime.DatetimeFromParts),
                [.. values]
            )
        );
    }

    public static Expression Year(object node, Scope scope)
    {
        return DatePart(node, scope, date => Member(date, nameof(DateOnly.Year)));
    }

    public static Expression Quarter(object node, Scope scope)
    {
        return DatePart(node, scope, date => Function(nameof(PureQLTime.Quarter), date));
    }

    public static Expression Month(object node, Scope scope)
    {
        return DatePart(node, scope, date => Member(date, nameof(DateOnly.Month)));
    }

    public static Expression Day(object node, Scope scope)
    {
        return DatePart(node, scope, date => Member(date, nameof(DateOnly.Day)));
    }

    public static Expression DayOfWeek(object node, Scope scope)
    {
        return DatePart(
            node,
            scope,
            date => Function(nameof(PureQLTime.DayOfWeek), date)
        );
    }

    public static Expression DayOfYear(object node, Scope scope)
    {
        return DatePart(node, scope, date => Member(date, nameof(DateOnly.DayOfYear)));
    }

    public static Expression Week(object node, Scope scope)
    {
        return DatePart(node, scope, date => Function(nameof(PureQLTime.Week), date));
    }

    public static Expression WeekYear(object node, Scope scope)
    {
        return DatePart(node, scope, date => Function(nameof(PureQLTime.WeekYear), date));
    }

    public static Expression Hour(object node, Scope scope)
    {
        return TimePart(node, scope, time => Member(time, nameof(TimeOnly.Hour)));
    }

    public static Expression Minute(object node, Scope scope)
    {
        return TimePart(node, scope, time => Member(time, nameof(TimeOnly.Minute)));
    }

    public static Expression Second(object node, Scope scope)
    {
        return TimePart(node, scope, time => Member(time, nameof(TimeOnly.Second)));
    }

    public static Expression Microsecond(object node, Scope scope)
    {
        return TimePart(
            node,
            scope,
            time => Function(nameof(PureQLTime.Microsecond), time)
        );
    }

    public static Expression StartOfYear(object node, Scope scope)
    {
        return StartOf(node, scope, nameof(PureQLTime.StartOfYear));
    }

    public static Expression StartOfQuarter(object node, Scope scope)
    {
        return StartOf(node, scope, nameof(PureQLTime.StartOfQuarter));
    }

    public static Expression StartOfMonth(object node, Scope scope)
    {
        return StartOf(node, scope, nameof(PureQLTime.StartOfMonth));
    }

    public static Expression StartOfWeek(object node, Scope scope)
    {
        return StartOf(node, scope, nameof(PureQLTime.StartOfWeek));
    }

    public static Expression StartOfDay(object node, Scope scope)
    {
        return StartOf(node, scope, nameof(PureQLTime.StartOfDay));
    }

    public static Expression StartOfHour(object node, Scope scope)
    {
        return StartOf(node, scope, nameof(PureQLTime.StartOfHour));
    }

    public static Expression StartOfMinute(object node, Scope scope)
    {
        return StartOf(node, scope, nameof(PureQLTime.StartOfMinute));
    }

    public static Expression StartOfSecond(object node, Scope scope)
    {
        return StartOf(node, scope, nameof(PureQLTime.StartOfSecond));
    }

    // The day of an instant, in UTC or in a zone.
    public static Expression LocalDate(IReadOnlyList<Expression> values)
    {
        return values.Count == 1
            ? Functions.Call(typeof(DateOnly), nameof(DateOnly.FromDateTime), values[0])
            : Functions.Call(typeof(PureQLTime), nameof(PureQLTime.LocalDate), [.. values]);
    }

    // The time of day of an instant, in UTC or in a zone.
    public static Expression LocalTime(IReadOnlyList<Expression> values)
    {
        return values.Count == 1
            ? Functions.Call(typeof(TimeOnly), nameof(TimeOnly.FromDateTime), values[0])
            : Functions.Call(typeof(PureQLTime), nameof(PureQLTime.LocalTime), [.. values]);
    }

    // The instant of a date and a time of day in UTC.
    public static Expression Instant(Expression date, Expression time)
    {
        return Functions.Call(
            date,
            nameof(DateOnly.ToDateTime),
            time,
            Expression.Constant(DateTimeKind.Utc)
        );
    }

    // An operator over operands and, when the node has one, a zone: lifted
    // over all of them, the zone the last value.
    public static Expression Zoned(
        object node,
        Scope scope,
        Type result,
        IReadOnlyList<Expression> operands,
        Func<IReadOnlyList<Expression>, Expression> utc,
        Func<IReadOnlyList<Expression>, Expression> local
    )
    {
        return ModelNode.Optional(node, "Zone") is object zone
            ? Lifted.All([.. operands, scope.Compile(zone)], result, local)
            : Lifted.All(operands, result, utc);
    }

    public static bool IsInstant(Expression value)
    {
        return Typed.OrNull(value.Type) == typeof(DateTime?);
    }

    // A calendar part of a date, or of the day of an instant.
    private static Expression DatePart(
        object node,
        Scope scope,
        Func<Expression, Expression> part
    )
    {
        Expression operand = Operand(node, scope);

        return IsInstant(operand)
            ? Zoned(
                node,
                scope,
                typeof(long?),
                [operand],
                values => part(LocalDate(values)),
                values => part(LocalDate(values))
            )
            : Lifted.Unary(operand, typeof(long?), part);
    }

    // A clock part of a time, or of the time of day of an instant.
    private static Expression TimePart(
        object node,
        Scope scope,
        Func<Expression, Expression> part
    )
    {
        Expression operand = Operand(node, scope);

        return IsInstant(operand)
            ? Zoned(
                node,
                scope,
                typeof(long?),
                [operand],
                values => part(LocalTime(values)),
                values => part(LocalTime(values))
            )
            : Lifted.Unary(operand, typeof(long?), part);
    }

    // The start of the period that holds the operand, in the operand's type.
    private static Expression StartOf(object node, Scope scope, string name)
    {
        Expression operand = Operand(node, scope);
        Expression start(IReadOnlyList<Expression> values)
        {
            return Functions.Call(typeof(PureQLTime), name, [.. values]);
        }

        return Zoned(node, scope, Typed.OrNull(operand.Type), [operand], start, start);
    }

    private static Expression Member(Expression value, string name)
    {
        return Expression.Convert(Expression.Property(value, name), typeof(long));
    }

    private static Expression Function(string name, Expression value)
    {
        return Functions.Call(typeof(PureQLTime), name, value);
    }

    private static Expression Operand(object node, Scope scope)
    {
        return scope.Compile(ModelNode.Child(node, "Operand"));
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
