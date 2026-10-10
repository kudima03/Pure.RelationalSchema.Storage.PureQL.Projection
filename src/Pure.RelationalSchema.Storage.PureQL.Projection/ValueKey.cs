namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A tuple of runtime values compared with the specification's equality, so
// groupBy keys and distinct rows put null with null and 1 with 1.0.
internal sealed class ValueKey(IReadOnlyList<object?> values) : IEquatable<ValueKey>
{
    public IReadOnlyList<object?> Items { get; } = values;

    public bool Equals(ValueKey? other)
    {
        return other is not null
            && Items.Count == other.Items.Count
            && Items
                .Zip(other.Items)
                .All(pair => Values.Equal(pair.First, pair.Second));
    }

    public override bool Equals(object? obj)
    {
        return Equals(obj as ValueKey);
    }

    public override int GetHashCode()
    {
        HashCode hash = new();

        foreach (object? value in Items)
        {
            hash.Add(Values.Hash(value));
        }

        return hash.ToHashCode();
    }
}
