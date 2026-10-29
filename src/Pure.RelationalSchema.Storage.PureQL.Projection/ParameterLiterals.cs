using System.Globalization;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter;
using PureQL.CSharp.Model.Literals;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Pure parameters as the literals they stand for, so they are bound and
// checked as literals are (see ParameterValues). A parameter's type names a
// PureQL type; a nullable one is that type's typed null, whatever its text.
// Otherwise its value is the invariant text a stored cell holds, parsed as
// StoredColumn parses a cell, a datetime without an offset read as UTC. A
// type that is not a scalar PureQL type, or text that is not a value of the
// type, fails the projection with ArgumentException, as a literal of another
// type than its parameter's does.
internal static class ParameterLiterals
{
    public static IReadOnlyDictionary<string, ILiteral> Of(
        IReadOnlyDictionary<string, IParameter> parameters
    )
    {
        return parameters.ToDictionary(
            parameter => parameter.Key,
            parameter => Of(parameter.Key, parameter.Value),
            StringComparer.Ordinal
        );
    }

    private static ILiteral Of(string name, IParameter parameter)
    {
        string type = parameter.Type.Name.TextValue;

        try
        {
            return parameter.Type.Nullable.BoolValue
                ? Null(name, type)
                : Value(name, type, parameter.Value.TextValue);
        }
        catch (FormatException exception)
        {
            throw new ArgumentException(
                $"Parameter '{name}' is {type} and its value "
                    + $"'{parameter.Value.TextValue}' is not a {type}.",
                exception
            );
        }
        catch (OverflowException exception)
        {
            throw new ArgumentException(
                $"Parameter '{name}' is {type} and its value "
                    + $"'{parameter.Value.TextValue}' is out of its range.",
                exception
            );
        }
    }

    private static ILiteral Null(string name, string type)
    {
        return type switch
        {
            "integer" => new LiteralIntegerNullable(),
            "decimal" => new LiteralDecimalNullable(),
            "string" => new LiteralStringNullable(),
            "boolean" => new LiteralBooleanNullable(),
            "date" => new LiteralDateNullable(),
            "time" => new LiteralTimeNullable(),
            "datetime" => new LiteralDatetimeNullable(),
            "uuid" => new LiteralUuidNullable(),
            _ => throw Unsupported(name, type),
        };
    }

    private static ILiteral Value(string name, string type, string text)
    {
        CultureInfo invariant = CultureInfo.InvariantCulture;

        return type switch
        {
            "integer" => new LiteralInteger(
                long.Parse(text, NumberStyles.Integer, invariant)
            ),
            "decimal" => new LiteralDecimal(
                decimal.Parse(text, NumberStyles.Float, invariant)
            ),
            "string" => new LiteralString(text),
            "boolean" => new LiteralBoolean(bool.Parse(text)),
            "date" => new LiteralDate(DateOnly.Parse(text, invariant)),
            "time" => new LiteralTime(TimeOnly.Parse(text, invariant)),
            "datetime" => new LiteralDatetime(
                DateTime.Parse(
                    text,
                    invariant,
                    DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
                )
            ),
            "uuid" => new LiteralUuid(Guid.Parse(text)),
            _ => throw Unsupported(name, type),
        };
    }

    private static ArgumentException Unsupported(string name, string type)
    {
        return new ArgumentException(
            $"Parameter '{name}' has the type '{type}', "
                + "which is not a scalar PureQL type."
        );
    }
}
