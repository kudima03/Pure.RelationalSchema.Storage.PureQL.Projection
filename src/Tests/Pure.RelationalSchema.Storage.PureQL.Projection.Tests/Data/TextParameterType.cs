using Pure.Primitives.Abstractions.Bool;
using Pure.Primitives.Abstractions.String;
using Pure.Primitives.Bool;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.ParameterType;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// A parameter type of any name.
internal sealed record TextParameterType : IParameterType
{
    private readonly string _name;

    private readonly bool _nullable;

    public TextParameterType(string name, bool nullable)
    {
        _name = name;
        _nullable = nullable;
    }

    public IString Name => new String(_name);

    public IBool Nullable => _nullable ? new True() : new False();
}
