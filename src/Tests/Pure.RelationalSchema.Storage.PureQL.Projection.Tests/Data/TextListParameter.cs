using Pure.Primitives.Abstractions.String;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.ListParameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.ParameterType;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// A list parameter of any type name holding any items, as a list not built
// by Pure.RelationalSchema.Storage.PureQL.Projection.Parameters may.
internal sealed record TextListParameter : IListParameter
{
    public TextListParameter(string type, params IParameter[] items)
    {
        Type = new TextParameterType(type, false);
        Items = items;
    }

    public IParameterType Type { get; }

    public IEnumerable<IParameter> Items { get; }

    public IString Value => new String("");
}
