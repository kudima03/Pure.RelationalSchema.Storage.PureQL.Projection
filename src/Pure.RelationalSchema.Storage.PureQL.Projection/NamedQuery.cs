namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// A subquery: the clauses of its query under the name sources read it by.
internal sealed record NamedQuery(string Name, QueryClauses Clauses);
