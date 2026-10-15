namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// How a query's rows are read: as rows, where order and duplicates count; by
// a query that only aggregates them or reads them in its own order, where
// duplicates count but order does not; or by in, where only membership does.
internal enum QueryUse
{
    Rows,
    Aggregated,
    Membership,
}
