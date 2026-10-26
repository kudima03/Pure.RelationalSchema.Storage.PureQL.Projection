using System.Linq.Expressions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Tree;

// Every query of the sample catalogue compiles to a tree a SQL provider can
// translate: no node is evaluated on the client but the result rows, built
// once, by the last Select, over the query a provider is given.
public sealed class TranslatableTreeTests
{
    public static TheoryData<string> Samples =>
        [.. Sample.All.Select(sample => sample.Name)];

    [Theory]
    [MemberData(nameof(Samples))]
    public void SampleTreeHoldsOnlyTranslatableNodes(string name)
    {
        PureQLProjection projection = new(
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()],
            Sample.Named(name).Query
        );

        Assert.Empty(Untranslatable.In(projection.Expression));
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void SampleTreeBuildsResultRowsOnlyInItsLastSelect(string name)
    {
        PureQLProjection projection = new(
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()],
            Sample.Named(name).Query
        );

        MethodCallExpression last = (MethodCallExpression)projection.Expression;
        LambdaExpression selector = (LambdaExpression)
            ((UnaryExpression)last.Arguments[1]).Operand;

        Assert.Equal(nameof(Queryable.Select), last.Method.Name);
        Assert.Equal("ResultRow", selector.Body.Type.Name);
        Assert.DoesNotContain(
            "new ResultRow",
            TreeText.Of(last.Arguments[0]),
            StringComparison.Ordinal
        );
    }
}
