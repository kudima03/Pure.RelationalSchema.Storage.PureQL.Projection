using System.Collections;
using System.Linq.Expressions;
using PureQL.CSharp.Model.Parameters;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The values a query's parameters are bound to, by name. A parameter is a
// captured variable in the tree, the Value of a QueryParameter constant, as
// C# closures are, so a provider can pass it as a SQL parameter. A value has
// its parameter's type: an integral number for integer, a decimal or an
// integral number for decimal, a string, bool, DateOnly, TimeOnly or Guid,
// a DateTimeOffset or DateTime for datetime (a DateTime that is not local is
// read as UTC, as stored datetimes are) and a sequence of non-null items for
// a list; null only for a nullable parameter. A parameter given no value
// fails the query when it is read instead of guessing a value.
internal sealed class ParameterValues(IReadOnlyDictionary<string, object?> values)
{
    private readonly IReadOnlyDictionary<string, object?> _values =
        new Dictionary<string, object?>(values, StringComparer.Ordinal);

    public static ParameterValues None { get; } = new(new Dictionary<string, object?>());

    public static NotSupportedException Unbound(string name)
    {
        return new NotSupportedException($"Parameter '{name}' is not bound to a value.");
    }

    // A scalar parameter has its type's value type, a list parameter is a
    // sequence of its item type.
    public Expression Value(IParameter parameter)
    {
        Type item = ValueTypes.Clr(parameter.Type);
        Type type = IsList(parameter)
            ? typeof(IEnumerable<>).MakeGenericType(item)
            : item;
        Type holder = typeof(QueryParameter<>).MakeGenericType(type);

        return Expression.Property(
            Expression.Constant(
                _values.ContainsKey(parameter.Name)
                    ? Activator.CreateInstance(holder, parameter.Name, Bound(parameter))
                    : Activator.CreateInstance(holder, parameter.Name)
            ),
            nameof(QueryParameter<>.Value)
        );
    }

    // Rejects a value that is not of the parameter's type.
    public void Check(IParameter parameter)
    {
        if (_values.ContainsKey(parameter.Name))
        {
            _ = Bound(parameter);
        }
    }

    // The integer a parameter is bound to, null when it is not bound.
    public long? Integer(ParamInteger parameter)
    {
        return _values.ContainsKey(parameter.Name) ? (long?)Bound(parameter) : null;
    }

    private static bool IsList(IParameter parameter)
    {
        return parameter.Type.Name.EndsWith("List", StringComparison.Ordinal);
    }

    // A parameter's value as the tree holds it: a value of its type's value
    // type, or the items of a list in an array.
    private object? Bound(IParameter parameter)
    {
        object? value = _values[parameter.Name];

        if (!IsList(parameter))
        {
            return value is null && parameter.Type.Nullable ? null : Of(parameter, value);
        }

        object?[] items = value is IEnumerable sequence and not string
            ? [.. sequence.Cast<object?>().Select(each => Of(parameter, each))]
            : throw Mismatch(parameter, value);

        Array array = Array.CreateInstance(ValueTypes.Clr(parameter.Type), items.Length);

        for (int i = 0; i < items.Length; i++)
        {
            array.SetValue(items[i], i);
        }

        return array;
    }

    private static object Of(IParameter parameter, object? value)
    {
        string type = IsList(parameter)
            ? parameter.Type.Name[..^"List".Length]
            : parameter.Type.Name;

        object? converted = (type, value) switch
        {
            ("integer", long or int or short or sbyte or byte or uint or ushort) =>
                Convert.ToInt64(value),
            ("integer", ulong number) when number <= long.MaxValue => (long)number,
            ("decimal", decimal) => value,
            (
                "decimal",
                long or int or short or sbyte or byte or ulong or uint or ushort
            ) => Convert.ToDecimal(value),
            ("string", string) => value,
            ("boolean", bool) => value,
            ("date", DateOnly) => value,
            ("time", TimeOnly) => value,
            ("datetime", DateTimeOffset instant) => instant.UtcDateTime,
            ("datetime", DateTime { Kind: DateTimeKind.Local } local) =>
                local.ToUniversalTime(),
            ("datetime", DateTime instant) => DateTime.SpecifyKind(
                instant,
                DateTimeKind.Utc
            ),
            ("uuid", Guid) => value,
            _ => null,
        };

        return converted ?? throw Mismatch(parameter, value);
    }

    private static ArgumentException Mismatch(IParameter parameter, object? value)
    {
        string what = value is null ? "null" : $"a value of type {value.GetType().Name}";

        return new ArgumentException(
            $"Parameter '{parameter.Name}' is {parameter.Type.Name} and cannot be "
                + $"bound to {what}."
        );
    }
}
