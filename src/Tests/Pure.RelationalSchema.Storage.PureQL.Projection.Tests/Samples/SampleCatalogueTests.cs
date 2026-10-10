using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model.Samples.Queries.GroupBy;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Samples;

// Every query of PureQL.CSharp.Model.Samples that carries an expected result
// returns exactly that result over the fixtures it was computed on.
public sealed class SampleCatalogueTests
{
    // Samples whose expected result disagrees with the specification. Each is
    // asserted against the specification by a test of its own below.
    private static readonly IReadOnlySet<string> ContradictSpecification =
        new HashSet<string>([nameof(GroupedRevenueQuery)]);

    public static TheoryData<string> SamplesWithResult =>
        [
            .. Sample
                .All.Where(sample => sample.Result is not null)
                .Where(sample => !ContradictSpecification.Contains(sample.Name))
                .Select(sample => sample.Name),
        ];

    public static TheoryData<string> SamplesWithParameters =>
        [
            .. Sample
                .All.Where(sample => sample.Result is null)
                .Select(sample => sample.Name),
        ];

    [Fact]
    public void CatalogueHoldsFiftySixSamples()
    {
        Assert.Equal(56, Sample.All.Count);
    }

    [Theory]
    [MemberData(nameof(SamplesWithResult))]
    public void SampleReturnsItsExpectedResult(string name)
    {
        Sample sample = Sample.Named(name);
        ExpectedTable expected = new(sample.Result!);

        PureQLProjection actual = new(
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()],
            sample.Query
        );

        if (sample.IsOrdered)
        {
            expected.AssertOrdered(actual);
        }
        else
        {
            expected.AssertUnordered(actual);
        }
    }

    [Theory]
    [MemberData(nameof(SamplesWithParameters))]
    public void SampleWithParameterFailsBecauseParametersCannotBeBound(string name)
    {
        PureQLProjection projection = new(
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()],
            Sample.Named(name).Query
        );

        _ = Assert.Throws<NotSupportedException>(() => projection.ToList());
    }

    // "An aggregate sees the rows of its group, or every row left by where
    // with over: all": the four shipped order lines, before having drops the
    // groups at or below the revenue threshold. The catalogue's expected
    // share was computed with SQL's sum(count(*)) over (), a window that runs
    // after HAVING and so sees only the three lines of the kept groups.
    [Fact]
    public void GroupedRevenueSharesAreOfEveryLineLeftByWhere()
    {
        PureQLProjection projection = new(
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()],
            new GroupedRevenueQuery().Value
        );

        Assert.Equal(["2", "1"], ResultColumn.Texts(projection, "line_items"));
        Assert.Equal(["0.5", "0.25"], ResultColumn.Texts(projection, "share_of_all_lines"));
    }
}
