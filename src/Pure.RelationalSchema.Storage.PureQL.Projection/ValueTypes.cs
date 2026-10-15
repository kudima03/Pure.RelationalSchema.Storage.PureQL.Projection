using Pure.RelationalSchema.Abstractions.ColumnType;
using Pure.RelationalSchema.ColumnType;
using PureQL.CSharp.Model.Types;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Maps between PureQL types, storage column types and the CLR types values
// have in the tree. A stored column holds one PureQL type: every integral
// column type is an integer, double and float are decimals. A result column
// is typed the way PureQL.CSharp.Model.Samples types its expected results:
// integer as long, decimal as double. In the tree every value may be null:
// integer is long?, decimal decimal?, string string, boolean bool?, date
// DateOnly?, time TimeOnly?, datetime DateTime? (a UTC instant), uuid Guid?.
internal static class ValueTypes
{
    public static string? OfColumn(IColumnType columnType)
    {
        return columnType.Name.TextValue switch
        {
            "long" or "int" or "uint" or "ulong" or "ushort" => "integer",
            "double" or "float" => "decimal",
            "string" => "string",
            "bool" => "boolean",
            "date" => "date",
            "time" => "time",
            "datetime" => "datetime",
            "uuid" => "uuid",
            _ => null,
        };
    }

    public static IColumnType ColumnOf(IType type)
    {
        return type.Name switch
        {
            "integer" => new LongColumnType(),
            "decimal" => new DoubleColumnType(),
            "string" => new StringColumnType(),
            "boolean" => new BoolColumnType(),
            "date" => new DateColumnType(),
            "time" => new TimeColumnType(),
            "datetime" => new DateTimeColumnType(),
            "uuid" => new UuidColumnType(),
            _ => throw new NotSupportedException(
                $"PureQL type '{type.Name}' has no column type."
            ),
        };
    }

    public static Type Clr(IType type)
    {
        return Clr(type.Name);
    }

    // The CLR type of a PureQL type; a list type gives its item type.
    public static Type Clr(string type)
    {
        string item = type.EndsWith("List", StringComparison.Ordinal)
            ? type[..^4]
            : type;

        return item switch
        {
            "integer" => typeof(long?),
            "decimal" => typeof(decimal?),
            "string" => typeof(string),
            "boolean" => typeof(bool?),
            "date" => typeof(DateOnly?),
            "time" => typeof(TimeOnly?),
            "datetime" => typeof(DateTime?),
            "uuid" => typeof(Guid?),
            _ => throw new NotSupportedException(
                $"PureQL type '{type}' has no value type."
            ),
        };
    }
}
