using PureQL.CSharp.Model.Types;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A PureQL type as a column carries it: its name and whether it may be null.
// A column of a set operation or of inline rows has no expression of its
// own: it takes the least type that accepts the column in every input, an
// integer and a decimal giving a decimal, and is nullable if any input is.
internal sealed record DeclaredType(string Name, bool Nullable) : IType
{
    public static DeclaredType Of(IType type)
    {
        return new DeclaredType(type.Name, type.Nullable);
    }

    // The least type that accepts both, or null when the two do not meet.
    public static DeclaredType? Least(IType left, IType right)
    {
        string? name =
            left.Name == right.Name ? left.Name
            : (left.Name, right.Name) is ("integer", "decimal") or ("decimal", "integer")
                ? "decimal"
            : null;

        return name is null
            ? null
            : new DeclaredType(name, left.Nullable || right.Nullable);
    }

    // Whether a value of the other type stands where this one is expected.
    public bool Accepts(IType other)
    {
        return (Name == other.Name || (Name, other.Name) is ("decimal", "integer"))
            && (Nullable || !other.Nullable);
    }
}
