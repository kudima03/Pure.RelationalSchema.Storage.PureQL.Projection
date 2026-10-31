using System.Globalization;
using System.Linq.Expressions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.ListParameter;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.Types;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;
using ModelParameter = PureQL.CSharp.Model.Parameters.IParameter;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The Pure parameters a query's parameters are bound to, by name. A query
// parameter is a captured variable in the tree, the Value of a
// QueryParameter constant, as C# closures are, so a provider can pass it as
// a SQL parameter; it holds its Pure parameter's value. A Pure parameter
// binds a query parameter of its type (Type.Name, a PureQL type name), a
// typed null (Type.Nullable) only a nullable one, and an integer (list) a
// decimal (list), as an integer stands for a decimal in a decimal
// expression. Its value is the invariant text a stored cell holds, parsed as
// StoredColumn parses a cell, a datetime without an offset read as UTC; a
// list's is its items', each a scalar parameter of its item type, copied
// when the projection is built. The value of a date, time or datetime
// parameter may instead be date math, such as now-10d (see DateMath): it is
// checked and bound at the clock's instant when the projection is built, and
// bound again at the instant each run starts (see Execution). A query
// parameter given no Pure parameter, one of another type, or text that is not
// a value of its type fails the query when it is validated (issue #251),
// instead of guessing a value.
internal sealed class ParameterValues(
    IReadOnlyDictionary<string, IParameter> values,
    TimeProvider clock
)
{
    private const string List = "List";

    private readonly IReadOnlyDictionary<string, IParameter> _values =
        new Dictionary<string, IParameter>(values, StringComparer.Ordinal);

    public static ParameterValues None { get; } =
        new(new Dictionary<string, IParameter>(), TimeProvider.System);

    // A scalar parameter has its type's value type, a list parameter is a
    // sequence of its item type.
    public Expression Value(ModelParameter parameter)
    {
        Type item = ValueTypes.Clr(parameter.Type);
        Type type = typeof(QueryParameter<>).MakeGenericType(
            IsList(parameter.Type.Name)
                ? typeof(IEnumerable<>).MakeGenericType(item)
                : item
        );
        object? bound = Bound(parameter);

        return Expression.Property(
            Expression.Constant(
                bound is DateMath dateMath
                    ? Activator.CreateInstance(type, parameter.Name, dateMath, Now())
                    : Activator.CreateInstance(type, parameter.Name, bound)
            ),
            nameof(QueryParameter<>.Value)
        );
    }

    // Rejects a parameter given no value, or a value of another type than
    // the parameter's.
    public void Check(ModelParameter parameter)
    {
        _ = Bound(parameter);
    }

    public long Integer(ParamInteger parameter)
    {
        return (long)Bound(parameter)!;
    }

    private static bool IsList(string type)
    {
        return type.EndsWith(List, StringComparison.Ordinal);
    }

    private static bool Binds(string parameter, string value)
    {
        return value == parameter
            || (parameter, value) is ("decimal", "integer") or ("decimalList", "integerList");
    }

    private static string Describe(string type, bool nullable, string prefix)
    {
        return nullable ? $"{prefix} {type}" : type;
    }

    // A parameter's value as the tree holds it: a value of its type's value
    // type, its DateMath, or the items of a list in an array.
    private object? Bound(ModelParameter parameter)
    {
        if (!_values.TryGetValue(parameter.Name, out IParameter? bound))
        {
            throw new ArgumentException(
                $"Parameter '{parameter.Name}' is not bound to a value."
            );
        }

        IType type = parameter.Type;
        string name = bound.Type.Name.TextValue;
        bool nullable = bound.Type.Nullable.BoolValue;

        if (!Binds(type.Name, name) || (nullable && !type.Nullable))
        {
            throw new ArgumentException(
                $"Parameter '{parameter.Name}' is "
                    + $"{Describe(type.Name, type.Nullable, "nullable")} and cannot be "
                    + $"bound to a {Describe(name, nullable, "null")} parameter."
            );
        }

        Type item = ValueTypes.Clr(type);

        return IsList(type.Name)
            ? Items(parameter.Name, type.Name, bound, item)
            : nullable
            ? null
            : Literals.As(Value(parameter.Name, name, bound.Value.TextValue), item);
    }

    private static Array Items(string parameter, string type, IParameter bound, Type item)
    {
        if (bound is not IListParameter list)
        {
            throw new ArgumentException(
                $"Parameter '{parameter}' is {type} and cannot be bound to a "
                    + $"{bound.Type.Name.TextValue} parameter that is not a list."
            );
        }

        string itemType = type[..^List.Length];
        IParameter[] items = [.. list.Items];
        Array array = Array.CreateInstance(item, items.Length);

        for (int i = 0; i < items.Length; i++)
        {
            string name = items[i].Type.Name.TextValue;
            bool nullable = items[i].Type.Nullable.BoolValue;

            if (!Binds(itemType, name) || nullable)
            {
                throw new ArgumentException(
                    $"Parameter '{parameter}' is {type} and cannot hold a "
                        + $"{Describe(name, nullable, "null")} item."
                );
            }

            array.SetValue(
                Literals.As(Parsed(parameter, name, items[i].Value.TextValue), item),
                i
            );
        }

        return array;
    }

    // The date math of a date, time or datetime value that is date math,
    // checked at the clock's instant; otherwise the value parsed.
    private object Value(string parameter, string type, string text)
    {
        if (!DateMath.Is(type, text))
        {
            return Parsed(parameter, type, text);
        }

        try
        {
            DateMath dateMath = DateMath.Parse(type, text);
            _ = dateMath.At(Now());
            return dateMath;
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                $"Parameter '{parameter}' is bound to '{text}', which is not date "
                    + $"math of a {type}, such as now-10d.",
                exception
            );
        }
        catch (Exception exception)
            when (exception is OverflowException or ArgumentOutOfRangeException)
        {
            throw new ArgumentException(
                $"Parameter '{parameter}' is bound to '{text}', which is out of the "
                    + $"range of a {type}.",
                exception
            );
        }
    }

    private DateTime Now()
    {
        return clock.GetUtcNow().UtcDateTime;
    }

    // The value of a stored cell's text, parsed as StoredColumn parses it.
    private static object Parsed(string parameter, string type, string text)
    {
        try
        {
            return Parse(type, text);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                $"Parameter '{parameter}' is bound to '{text}', which is not a {type}.",
                exception
            );
        }
        catch (OverflowException exception)
        {
            throw new ArgumentException(
                $"Parameter '{parameter}' is bound to '{text}', which is out of the "
                    + $"range of a {type}.",
                exception
            );
        }
    }

    private static object Parse(string type, string text)
    {
        CultureInfo invariant = CultureInfo.InvariantCulture;

        return type switch
        {
            "integer" => long.Parse(text, NumberStyles.Integer, invariant),
            "decimal" => decimal.Parse(text, NumberStyles.Float, invariant),
            "boolean" => bool.Parse(text),
            "date" => DateOnly.Parse(text, invariant),
            "time" => TimeOnly.Parse(text, invariant),
            "datetime" => DateTime.Parse(
                text,
                invariant,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
            ),
            "uuid" => Guid.Parse(text),
            _ => text,
        };
    }
}
