using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.SelectItems;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Errors;

// A parameter given no value fails the query when it is read, wherever it
// stands; a value that is not of its parameter's type, and a bound pagination
// out of range, fail when the projection is built.
public sealed class ParameterTests
{
    private static readonly IStoredSchemaDataSet Names = new InlineTable(
        "t",
        "names",
        ("name", new StringColumnType())
    ).With(["Ann"], ["Bob"]);

    private static readonly IReadOnlyDictionary<string, object?> ValuesOfAnotherType =
        new Dictionary<string, object?>
        {
            ["null"] = null,
            ["string"] = "10",
            ["decimal"] = 10m,
            ["double"] = 10.0,
            ["ulong over long"] = ulong.MaxValue,
            ["boolean"] = true,
            ["list with null"] = new[] { "Ann", null },
            ["list with integer"] = new object[] { "Ann", 1L },
        };

    [Fact]
    public void UnboundListParameterFailsWhenRead()
    {
        PureQLProjection projection = new([Names], InList(new ListParamString("wanted")));

        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            projection.ToList()
        );
        Assert.Contains("'wanted'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void UnboundPaginationParameterFailsWhenRead()
    {
        PureQLProjection projection = new(
            [Names],
            Paged(new Pagination(0, new ParamInteger("page_size")))
        );

        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            projection.ToList()
        );
        Assert.Contains("'page_size'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParameterMissingFromTheValuesFailsWhenRead()
    {
        PureQLProjection projection = new(
            [Names],
            Paged(new Pagination(0, new ParamInteger("page_size"))),
            new Dictionary<string, object?> { ["page"] = 1L }
        );

        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            projection.ToList()
        );
        Assert.Contains("'page_size'", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(IntegersOfAnotherType))]
    public void ValueOfAnotherTypeIsRejectedWhenTheProjectionIsBuilt(string value)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Names],
                Paged(new Pagination(0, new ParamInteger("page_size"))),
                new Dictionary<string, object?>
                {
                    ["page_size"] = ValuesOfAnotherType[value],
                }
            )
        );
        Assert.Contains("'page_size'", error.Message, StringComparison.Ordinal);
    }

    public static TheoryData<string> IntegersOfAnotherType =>
        ["null", "string", "decimal", "double", "ulong over long", "boolean"];

    [Fact]
    public void DecimalParameterRejectsABinaryFloatingPointValue()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Names],
                Selecting(new ParamDecimal("ratio")),
                new Dictionary<string, object?> { ["ratio"] = 0.1 }
            )
        );
    }

    [Theory]
    [MemberData(nameof(StringListsOfAnotherType))]
    public void ListParameterRejectsAValueThatIsNotAListOfItsItems(string value)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Names],
                InList(new ListParamString("wanted")),
                new Dictionary<string, object?> { ["wanted"] = ValuesOfAnotherType[value] }
            )
        );
        Assert.Contains("'wanted'", error.Message, StringComparison.Ordinal);
    }

    public static TheoryData<string> StringListsOfAnotherType =>
        ["null", "string", "list with null", "list with integer"];

    [Fact]
    public void ParameterInASubqueryNobodyReadsIsChecked()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Names],
                new PureQLQuery(
                    new MainPlainQuery(
                        new From(new FromEntity("t.names")),
                        [Name()],
                        subqueries:
                        [
                            new Subquery(
                                "unread",
                                new Query(
                                    new PlainQuery(
                                        new From(new FromEntity("t.names")),
                                        [Name()],
                                        joins: null,
                                        where: null,
                                        orderBy: null,
                                        pagination: new Pagination(
                                            0,
                                            new ParamInteger("page_size")
                                        ),
                                        distinctBy: null
                                    )
                                )
                            ),
                        ],
                        joins: null,
                        where: null,
                        orderBy: null,
                        pagination: null,
                        distinctBy: null
                    )
                ),
                new Dictionary<string, object?> { ["page_size"] = "10" }
            )
        );
    }

    [Theory]
    [InlineData(-1L, 1L, "Pagination skip -1 is negative.")]
    [InlineData(0L, 0L, "Pagination take 0 is less than 1.")]
    public void BoundPaginationOutOfRangeIsRejected(long skip, long take, string message)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Names],
                Paged(new Pagination(new ParamInteger("skip"), new ParamInteger("take"))),
                new Dictionary<string, object?> { ["skip"] = skip, ["take"] = take }
            )
        );
        Assert.Equal(message, error.Message);
    }

    private static SelectItemProjection Name()
    {
        return new SelectItemProjection(
            new SelectItemProjectionNonNullable(
                new SelectItemProjectionString(
                    "name",
                    new StringProjection(new FieldString("t.names", "name"))
                )
            )
        );
    }

    private static PureQLQuery Selecting(ParamDecimal parameter)
    {
        return new PureQLQuery(
            new MainPlainQuery(
                new From(new FromEntity("t.names")),
                [
                    new SelectItemProjection(
                        new SelectItemProjectionNonNullable(
                            new SelectItemProjectionDecimal(
                                parameter.Name,
                                new ExactDecimalProjection(parameter)
                            )
                        )
                    ),
                ]
            )
        );
    }

    private static PureQLQuery Paged(Pagination pagination)
    {
        return new PureQLQuery(
            new MainPlainQuery(
                new From(new FromEntity("t.names")),
                [Name()],
                subqueries: null,
                joins: null,
                where: null,
                orderBy: null,
                pagination: pagination,
                distinctBy: null
            )
        );
    }

    private static PureQLQuery InList(ListParamString list)
    {
        return new PureQLQuery(
            new MainPlainQuery(
                new From(new FromEntity("t.names")),
                [Name()],
                subqueries: null,
                joins: null,
                where: new BooleanRow(
                    new ComparisonRow(
                        new InRow(
                            new InStringRow(
                                new StringNullableRow(
                                    new StringRow(new FieldString("t.names", "name"))
                                ),
                                new ListString(list)
                            )
                        )
                    )
                ),
                orderBy: null,
                pagination: null,
                distinctBy: null
            )
        );
    }
}
