using PureQL.CSharp.Model.Types;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A column of a query's result: its alias and its type.
internal sealed record QueryColumn(string Alias, IType Type);
