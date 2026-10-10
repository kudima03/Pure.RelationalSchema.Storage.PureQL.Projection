using System.Reflection;
using Pure.RelationalSchema.Storage.Abstractions;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Select;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// One query of PureQL.CSharp.Model.Samples: its PureQL document and, when the
// catalogue computed one, the result the specification's semantics give over
// the Pure.RelationalSchema.Storage.Samples fixtures. A sample has no common
// interface, only a Value and an optional Result property, so the catalogue
// is read by reflection.
public sealed class Sample
{
    private readonly Type _type;

    private Sample(Type type)
    {
        _type = type;
    }

    public static IReadOnlyList<Sample> All { get; } =
    [
        .. typeof(UserNameColumnQuery)
            .Assembly.GetExportedTypes()
            .Where(type => type.GetProperty("Value")?.PropertyType == typeof(PureQLQuery))
            .OrderBy(type => type.FullName, StringComparer.Ordinal)
            .Select(type => new Sample(type)),
    ];

    public string Name => _type.Name;

    public PureQLQuery Query => (PureQLQuery)Read("Value")!;

    public IStoredTableDataSet? Result => (IStoredTableDataSet?)Read("Result");

    public bool IsOrdered =>
        Query.Match(grouped => grouped.OrderBy is not null, plain => plain.OrderBy is not null);

    public static Sample Named(string name)
    {
        return All.Single(sample => sample.Name == name);
    }

    public override string ToString()
    {
        return Name;
    }

    private object? Read(string property)
    {
        PropertyInfo? info = _type.GetProperty(property);
        return info?.GetValue(Activator.CreateInstance(_type));
    }
}
