namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Where the last enumeration of a projection ran its query.
public enum QueryExecution
{
    // The projection has not been enumerated.
    NotRun,

    // The provider of the stored tables ran the query, below the result
    // rows, as one query of its own (one SQL statement over one database).
    Translated,

    // The tree ran in memory: the tables are held in memory, or their
    // provider could not run the query (see PureQLProjection.FallbackReason).
    InMemory,
}
