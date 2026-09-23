using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Select;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// Scalar constants, and single-value Arithmetic whose operands are all
// literal constants, are the only supported non-aggregate
// SingleValueReturning select projections. Parameters (no binding API),
// Arithmetic containing a parameter or aggregate operand, and boolean
// composites still have no defined result through this entry point, so the
// translator fails fast with NotSupportedException instead of silently
// producing wrong cells. These tests pin that explicit-failure contract.
[Trait("Clause", "Select")]
[Trait("Feature", "ScalarProjection")]
public sealed class ScalarUnsupportedTests
{
    [Fact]
    public void NumberParameterInSelectFailsFastWithoutBinding()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new NumberParameterInSelectQuery().Value;

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(datasets, query)
        );
    }

    [Fact]
    public void SingleValueArithmeticWithParameterOperandInSelectFailsFast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new SingleValueArithmeticWithParameterOperandInSelectQuery().Value;

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(datasets, query)
        );
    }

    [Fact]
    public void BooleanCompositeInSelectFailsFast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new BooleanCompositeInSelectQuery().Value;

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(datasets, query)
        );
    }

    [Fact]
    public void ParameterAlongsideAggregateFailsFastInGroupMode()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new ParameterAlongsideAggregateQuery().Value;

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(datasets, query)
        );
    }
}
