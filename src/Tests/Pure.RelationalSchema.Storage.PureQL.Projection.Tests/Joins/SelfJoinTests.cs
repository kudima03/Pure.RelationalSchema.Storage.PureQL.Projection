using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Joins;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// Joins have no per-join alias (see UndeclaredAliasEntityTests), so a join
// whose entity equals the from entity gives both sides of the ON condition
// the identical entity string. CellValueExtractor.GetCell always prefers the
// QualifiedColumn matching the reference's entity, so both left.field and
// right.field resolve to the same (joined-side) cell, making any equi-join
// condition tautological and silently returning the full cross product
// instead of a correct self-join (issue #109). This is rejected fast rather
// than producing a wrong answer.
[Trait("Clause", "Join")]
[Trait("Feature", "SelfJoin")]
public sealed class SelfJoinTests
{
    [Fact]
    public void JoinOnSameEntityAsFromFailsFast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new JoinOnSameEntityAsFromQuery().Value;

        _ = Assert.Throws<NotSupportedException>(() => new ProjectionResult(
            new PureQLProjection(datasets, query)
        ));
    }
}
