using System.Linq.Expressions;
using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The conversions, one operator per result type, lifted. asString writes a
// value as a literal of its type is spelled; as<Type> of a string reads
// exactly that spelling and fails on any other text, where tryAs<Type> gives
// null. asDate and asTime read an instant in UTC or in a zone, asDatetime
// gives the first instant of a day, asDecimal widens an integer and
// asNullable only declares its operand nullable.
internal static class Conversions
{
    public static Expression AsString(object node, Scope scope)
    {
        return Lifted.Unary(
            Operand(node, scope),
            typeof(string),
            value => Read(nameof(PureQLConversions.AsString), value)
        );
    }

    public static Expression AsDate(object node, Scope scope)
    {
        Expression operand = Operand(node, scope);

        return Temporal.IsInstant(operand)
            ? Temporal.Zoned(
                node,
                scope,
                typeof(DateOnly?),
                [operand],
                Temporal.LocalDate,
                Temporal.LocalDate
            )
            : FromText(operand, typeof(DateOnly?), nameof(PureQLConversions.AsDate));
    }

    public static Expression AsTime(object node, Scope scope)
    {
        Expression operand = Operand(node, scope);

        return Temporal.IsInstant(operand)
            ? Temporal.Zoned(
                node,
                scope,
                typeof(TimeOnly?),
                [operand],
                Temporal.LocalTime,
                Temporal.LocalTime
            )
            : FromText(operand, typeof(TimeOnly?), nameof(PureQLConversions.AsTime));
    }

    // Midnight of a date, in UTC or in a zone, where it is the first instant
    // of the local day.
    public static Expression AsDatetime(object node, Scope scope)
    {
        Expression operand = Operand(node, scope);
        Expression midnight = Expression.Constant(TimeOnly.MinValue);

        return operand.Type == typeof(string)
            ? FromText(operand, typeof(DateTime?), nameof(PureQLConversions.AsDatetime))
            : Temporal.Zoned(
                node,
                scope,
                typeof(DateTime?),
                [operand],
                values => Temporal.Instant(values[0], midnight),
                values => Functions.Call(
                    typeof(PureQLTime),
                    nameof(PureQLTime.DatetimeFromParts),
                    values[0],
                    midnight,
                    values[1]
                )
            );
    }

    public static Expression AsInteger(object node, Scope scope)
    {
        return FromText(
            Operand(node, scope),
            typeof(long?),
            nameof(PureQLConversions.AsInteger)
        );
    }

    public static Expression AsDecimal(object node, Scope scope)
    {
        Expression operand = Operand(node, scope);

        return operand.Type == typeof(string)
            ? FromText(operand, typeof(decimal?), nameof(PureQLConversions.AsDecimal))
            : Arithmetic.Decimal(operand);
    }

    public static Expression AsBoolean(object node, Scope scope)
    {
        return FromText(
            Operand(node, scope),
            typeof(bool?),
            nameof(PureQLConversions.AsBoolean)
        );
    }

    public static Expression AsUuid(object node, Scope scope)
    {
        return FromText(
            Operand(node, scope),
            typeof(Guid?),
            nameof(PureQLConversions.AsUuid)
        );
    }

    // The same value, declared nullable.
    public static Expression AsNullable(object node, Scope scope)
    {
        return Typed.As(Operand(node, scope), ValueTypes.Clr(((IOperator)node).Type));
    }

    public static Expression TryAsInteger(object node, Scope scope)
    {
        return Read(nameof(PureQLConversions.TryAsInteger), Operand(node, scope));
    }

    public static Expression TryAsDecimal(object node, Scope scope)
    {
        return Read(nameof(PureQLConversions.TryAsDecimal), Operand(node, scope));
    }

    public static Expression TryAsBoolean(object node, Scope scope)
    {
        return Read(nameof(PureQLConversions.TryAsBoolean), Operand(node, scope));
    }

    public static Expression TryAsDate(object node, Scope scope)
    {
        return Read(nameof(PureQLConversions.TryAsDate), Operand(node, scope));
    }

    public static Expression TryAsTime(object node, Scope scope)
    {
        return Read(nameof(PureQLConversions.TryAsTime), Operand(node, scope));
    }

    public static Expression TryAsDatetime(object node, Scope scope)
    {
        return Read(nameof(PureQLConversions.TryAsDatetime), Operand(node, scope));
    }

    public static Expression TryAsUuid(object node, Scope scope)
    {
        return Read(nameof(PureQLConversions.TryAsUuid), Operand(node, scope));
    }

    private static Expression FromText(Expression operand, Type result, string name)
    {
        return Lifted.Unary(operand, result, value => Read(name, value));
    }

    private static Expression Read(string name, Expression value)
    {
        return Functions.Call(typeof(PureQLConversions), name, value);
    }

    private static Expression Operand(object node, Scope scope)
    {
        return scope.Compile(ModelNode.Child(node, "Operand"));
    }
}
