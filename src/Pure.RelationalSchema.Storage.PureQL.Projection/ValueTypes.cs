using Pure.RelationalSchema.Abstractions.ColumnType;
using Pure.RelationalSchema.ColumnType;
using PureQL.CSharp.Model.Types;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Maps between PureQL types and storage column types. A stored column holds
// one PureQL type: every integral column type is an integer, double and float
// are decimals. A result column is typed the way PureQL.CSharp.Model.Samples
// types its expected results: integer as long, decimal as double.
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

    // An integer expression may stand where a decimal is expected, so a value
    // is widened to the declared type of the column or key that holds it.
    public static object? Normalize(object? value, IType type)
    {
        return value is long number && type.Name == "decimal" ? (decimal)number : value;
    }
}
