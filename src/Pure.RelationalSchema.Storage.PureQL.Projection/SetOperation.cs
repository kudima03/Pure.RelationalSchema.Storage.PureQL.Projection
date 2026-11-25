namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The operation of a combined query, named as the model's property is.
internal enum SetOperation
{
    UnionAll,
    Union,
    Intersect,
    Except,
}
