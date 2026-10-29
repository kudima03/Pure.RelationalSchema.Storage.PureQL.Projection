using Pure.Primitives.Bool;
using Pure.Primitives.Number;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.ListParameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Parameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.SelectItems;
using Decimal = Pure.Primitives.Number.Decimal;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Errors;

// A parameter given no value, wherever it stands, a Pure parameter of
// another type than its parameter's, text that is not a value of its type, a
// list item of another type, and a bound pagination out of range fail when
// the projection is built, with ArgumentException (issue #251).
public sealed class ParameterTests
{
    private static readonly IStoredSchemaDataSet Names = new InlineTable(
        "t",
        "names",
        ("name", new StringColumnType())
    ).With(["Ann"], ["Bob"]);

    private static readonly IReadOnlyDictionary<string, IParameter> OfAnotherType =
        new Dictionary<string, IParameter>
        {
            ["null integer"] = new NullableIntegerParameter(),
            ["null string"] = new NullableStringParameter(),
            ["string"] = new StringParameter(new String("10")),
            ["decimal"] = new DecimalParameter(new Decimal(10m)),
            ["boolean"] = new BooleanParameter(new True()),
            ["integer list"] = new IntegerListParameter([new Long(10)]),
            ["unknown"] = new TextParameter("no such type", "10"),
        };

    [Fact]
    public void ListParameterGivenNoValuesIsRejectedWhenTheProjectionIsBuilt()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection([Names], InList(new ListParamString("wanted")))
        );
        Assert.Equal("Parameter 'wanted' is not bound to a value.", error.Message);
    }

    [Fact]
    public void PaginationParameterGivenNoValuesIsRejectedWhenTheProjectionIsBuilt()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Names],
                Paged(new Pagination(0, new ParamInteger("page_size")))
            )
        );
        Assert.Contains("'page_size'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ParameterMissingFromTheValuesIsRejectedWhenTheProjectionIsBuilt()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Names],
                Paged(new Pagination(0, new ParamInteger("page_size"))),
                new Dictionary<string, IParameter>
                {
                    ["page"] = new IntegerParameter(new Long(1)),
                }
            )
        );
        Assert.Contains("'page_size'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void ConditionParameterMissingFromNoValuesIsRejectedWhenTheProjectionIsBuilt()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Names],
                NamedAs(new ParamString("wanted")),
                new Dictionary<string, IParameter>()
            )
        );
        Assert.Contains("'wanted'", error.Message, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(IntegersOfAnotherType))]
    public void ParameterOfAnotherTypeIsRejectedWhenTheProjectionIsBuilt(string parameter)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Names],
                Paged(new Pagination(0, new ParamInteger("page_size"))),
                new Dictionary<string, IParameter>
                {
                    ["page_size"] = OfAnotherType[parameter],
                }
            )
        );
        Assert.Contains("'page_size'", error.Message, StringComparison.Ordinal);
    }

    public static TheoryData<string> IntegersOfAnotherType =>
        ["null integer", "string", "decimal", "boolean", "integer list", "unknown"];

    [Fact]
    public void TypedNullIsRejectedForAParameterThatIsNotNullable()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Names],
                Paged(new Pagination(0, new ParamInteger("page_size"))),
                new Dictionary<string, IParameter>
                {
                    ["page_size"] = new NullableIntegerParameter(),
                }
            )
        );
        Assert.Equal(
            "Parameter 'page_size' is integer and cannot be bound to a null integer parameter.",
            error.Message
        );
    }

    [Theory]
    [MemberData(nameof(StringListsOfAnotherType))]
    public void ListParameterRejectsAParameterThatIsNotAListOfItsItems(string parameter)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Names],
                InList(new ListParamString("wanted")),
                new Dictionary<string, IParameter>
                {
                    ["wanted"] = OfAnotherType[parameter],
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
                new Dictionary<string, IParameter>
                {
                    ["page_size"] = new StringParameter(new String("10")),
                }
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
                new Dictionary<string, IParameter>
                {
                    ["skip"] = new IntegerParameter(new Long(skip)),
                    ["take"] = new IntegerParameter(new Long(take)),
                }
            )
        );
        Assert.Equal(message, error.Message);
    }

    [Theory]
    [InlineData("integer", "seven", "'seven', which is not a integer.")]
    [InlineData("integer", "", "'', which is not a integer.")]
    [InlineData(
        "integer",
        "9223372036854775808",
        "'9223372036854775808', which is out of the range of a integer."
    )]
    [InlineData("decimal", "1e400", "'1e400', which is out of the range of a decimal.")]
    [InlineData("boolean", "yes", "'yes', which is not a boolean.")]
    [InlineData("date", "2024-02-30", "'2024-02-30', which is not a date.")]
    [InlineData("time", "25:00", "'25:00', which is not a time.")]
    [InlineData("datetime", "noon", "'noon', which is not a datetime.")]
    [InlineData("uuid", "8c5e1a2b", "'8c5e1a2b', which is not a uuid.")]
    public void ValueThatIsNotOfItsTypeIsRejected(string type, string text, string message)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Names],
                Reading(type),
                new Dictionary<string, IParameter> { ["p"] = new TextParameter(type, text) }
            )
        );
        Assert.Equal($"Parameter 'p' is bound to {message}", error.Message);
    }

    [Fact]
    public void ListParameterRejectsAScalarOfItsListType()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Names],
                InList(new ListParamString("wanted")),
                new Dictionary<string, IParameter>
                {
                    ["wanted"] = new TextParameter("stringList", "[\"Ann\"]"),
                }
            )
        );
        Assert.Equal(
            "Parameter 'wanted' is stringList and cannot be bound to a stringList "
                + "parameter that is not a list.",
            error.Message
        );
    }

    [Theory]
    [MemberData(nameof(ItemsOfAnotherType))]
    public void ListParameterRejectsAnItemOfAnotherType(string item, string message)
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Names],
                InList(new ListParamString("wanted")),
                new Dictionary<string, IParameter>
                {
                    ["wanted"] = new TextListParameter(
                        "stringList",
                        new StringParameter(new String("Ann")),
                        OfAnotherType[item]
                    ),
                }
            )
        );
        Assert.Equal(
            $"Parameter 'wanted' is stringList and cannot hold a {message} item.",
            error.Message
        );
    }

    public static TheoryData<string, string> ItemsOfAnotherType =>
        new TheoryData<string, string>
        {
            { "null string", "null string" },
            { "decimal", "decimal" },
            { "integer list", "integerList" },
        };

    [Fact]
    public void ListItemThatIsNotOfItsTypeIsRejected()
    {
        ArgumentException error = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [Names],
                InDecimalList(new ListParamDecimal("wanted")),
                new Dictionary<string, IParameter>
                {
                    ["d"] = new DecimalParameter(new Decimal(1.5m)),
                    ["wanted"] = new TextListParameter(
                        "decimalList",
                        new TextParameter("decimal", "1.5"),
                        new TextParameter("integer", "one")
                    ),
                }
            )
        );
        Assert.Equal(
            "Parameter 'wanted' is bound to 'one', which is not a integer.",
            error.Message
        );
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

    // A query whose only column reads the parameter p of the type.
    private static PureQLQuery Reading(string type)
    {
        SelectItemProjection column = type switch
        {
            "integer" => new SelectItemProjection(
                new SelectItemProjectionNonNullable(
                    new SelectItemProjectionInteger("p", new IntegerProjection(new ParamInteger("p")))
                )
            ),
            "decimal" => new SelectItemProjection(
                new SelectItemProjectionNonNullable(
                    new SelectItemProjectionDecimal("p", new ExactDecimalProjection(new ParamDecimal("p")))
                )
            ),
            "boolean" => new SelectItemProjection(
                new SelectItemProjectionNonNullable(
                    new SelectItemProjectionBoolean("p", new BooleanProjection(new ParamBoolean("p")))
                )
            ),
            "date" => new SelectItemProjection(
                new SelectItemProjectionNonNullable(
                    new SelectItemProjectionDate("p", new DateProjection(new ParamDate("p")))
                )
            ),
            "time" => new SelectItemProjection(
                new SelectItemProjectionNonNullable(
                    new SelectItemProjectionTime("p", new TimeProjection(new ParamTime("p")))
                )
            ),
            "datetime" => new SelectItemProjection(
                new SelectItemProjectionNonNullable(
                    new SelectItemProjectionDatetime("p", new DatetimeProjection(new ParamDatetime("p")))
                )
            ),
            _ => new SelectItemProjection(
                new SelectItemProjectionNonNullable(
                    new SelectItemProjectionUuid("p", new UuidProjection(new ParamUuid("p")))
                )
            ),
        };

        return new PureQLQuery(
            new MainPlainQuery(new From(new FromEntity("t.names")), [column])
        );
    }

    private static PureQLQuery InDecimalList(ListParamDecimal list)
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
                            new InDecimalRow(
                                new DecimalNullableRow(new ExactDecimalRow(new ParamDecimal("d"))),
                                new ListDecimal(list)
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
