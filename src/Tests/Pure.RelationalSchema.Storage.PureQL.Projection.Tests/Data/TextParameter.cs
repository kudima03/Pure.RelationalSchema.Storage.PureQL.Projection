using Pure.Primitives.Abstractions.String;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.ParameterType;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// A parameter that holds any text as a value of any type name, as a
// parameter not built by Pure.RelationalSchema.Storage.PureQL.Projection.Parameters
// may.
internal sealed record TextParameter : IParameter
{
    private readonly string _text;

    public TextParameter(string type, string text)
        : this(new TextParameterType(type, false), text) { }

    public TextParameter(IParameterType type, string text)
    {
        Type = type;
        _text = text;
    }

    public IParameterType Type { get; }

    public IString Value => new String(_text);
}
