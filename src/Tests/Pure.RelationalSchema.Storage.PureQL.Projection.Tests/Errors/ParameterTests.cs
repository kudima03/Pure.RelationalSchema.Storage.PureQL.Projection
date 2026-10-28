using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.SelectItems;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Errors;

// A parameter given no literal fails the query when it is read, wherever it
// stands; a literal of another type than its parameter's, and a bound
// pagination out of range, fail when the projection is built. A parameter
// missing from the literals a projection is given is to fail when it is built
// too, with ArgumentException (issue #251, skipped until fixed).
public sealed class ParameterTests
{
    private const string Issue251 =
        "Issue #251: a parameter missing from the values fails only when read, "
        + "with NotSupportedException.";

    private static readonly IStoredSchemaDataSet Names = new InlineTable(
        "t",
        "names",
        ("name", new StringColumnType())
    ).With(["Ann"], ["Bob"]);

    private static readonly IReadOnlyDictionary<string, ILiteral> LiteralsOfAnotherType =
        new Dictionary<string, ILiteral>
        {
            ["null integer"] = new LiteralIntegerNullable(),
            ["null string"] = new LiteralStringNullable(),
            ["string"] = new LiteralString("10"),
            ["decimal"] = new LiteralDecimal(10m),
            ["boolean"] = new LiteralBoolean(true),
            ["integer list"] = new ListLiteralInteger([10]),
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
            new Dictionary<string, ILiteral> { ["page"] = new LiteralInteger(1) }
        );

        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            projection.ToList()
        );
        Assert.Contains("'page_size'", error.Message, StringComparison.Ordinal);
    }

    [Fact(Skip = Issue251)]
    public void ParameterMissingFromTheValuesIsRejectedWhenTheProjectionIsBuilt()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Names],
                Paged(new Pagination(0, new ParamInteger("page_size"))),
                new Dictionary<string, ILiteral> { ["page"] = new LiteralInteger(1) }
            )
        );
        Assert.Contains("'page_size'", error.Message, StringComparison.Ordinal);
    }

    [Fact(Skip = Issue251)]
    public void ConditionParameterMissingFromNoValuesIsRejectedWhenTheProjectionIsBuilt()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Names],
                NamedAs(new ParamString("wanted")),
                new Dictionary<string, ILiteral>()
            )
        );
        Assert.Contains("'wanted'", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(IntegersOfAnotherType))]
    public void LiteralOfAnotherTypeIsRejectedWhenTheProjectionIsBuilt(string literal)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Names],
                Paged(new Pagination(0, new ParamInteger("page_size"))),
                new Dictionary<string, ILiteral>
                {
                    ["page_size"] = LiteralsOfAnotherType[literal],
                }
            )
        );
        Assert.Contains("'page_size'", error.Message, StringComparison.Ordinal);
    }

    public static TheoryData<string> IntegersOfAnotherType =>
        ["null integer", "string", "decimal", "boolean", "integer list"];

    [Fact]
    public void TypedNullIsRejectedForAParameterThatIsNotNullable()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Names],
                Paged(new Pagination(0, new ParamInteger("page_size"))),
                new Dictionary<string, ILiteral>
                {
                    ["page_size"] = new LiteralIntegerNullable(),
                }
            )
        );
        Assert.Equal(
            "Parameter 'page_size' is integer and cannot be bound to a null integer literal.",
            error.Message
        );
    }

    [Theory]
    [MemberData(nameof(StringListsOfAnotherType))]
    public void ListParameterRejectsALiteralThatIsNotAListOfItsItems(string literal)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Names],
                InList(new ListParamString("wanted")),
                new Dictionary<string, ILiteral>
                {
                    ["wanted"] = LiteralsOfAnotherType[literal],
                }
            )
        );
        Assert.Contains("'wanted'", error.Message, StringComparison.Ordinal);
    }

    public static TheoryData<string> StringListsOfAnotherType =>
        ["null string", "string", "integer list"];

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
                new Dictionary<string, ILiteral> { ["page_size"] = new LiteralString("10") }
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
                new Dictionary<string, ILiteral>
                {
                    ["skip"] = new LiteralInteger(skip),
                    ["take"] = new LiteralInteger(take),
                }
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

    private static PureQLQuery NamedAs(ParamString name)
    {
        return new PureQLQuery(
            new MainPlainQuery(
                new From(new FromEntity("t.names")),
                [Name()],
                subqueries: null,
                joins: null,
                where: new BooleanRow(
                    new ComparisonRow(
                        new EqualRow(
                            new EqualStringRow(
                                new StringNullableRow(
                                    new StringRow(new FieldString("t.names", "name"))
                                ),
                                new StringNullableRow(new StringRow(name))
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
