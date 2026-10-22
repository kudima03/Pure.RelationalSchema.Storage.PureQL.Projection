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

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Parameters bound by name to literals read their values wherever they
// stand: in an expression, as the list of in and in pagination. A literal has
// its parameter's type, an integer standing for a decimal, and a datetime is
// the instant it denotes in UTC; the tree still holds the parameter as a
// captured variable.
public sealed class ParameterBindingTests
{
    private static readonly IStoredSchemaDataSet Names = new InlineTable(
        "t",
        "names",
        ("id", new LongColumnType()),
        ("name", new StringColumnType())
    ).With(["1", "Ann"], ["2", "Bob"], ["3", "Cid"]);

    [Fact]
    public void ScalarParametersReadTheirBoundValues()
    {
        PureQLProjection projection = Selecting(
            new Dictionary<string, ILiteral>
            {
                ["integer"] = new LiteralInteger(7),
                ["decimal"] = new LiteralDecimal(2.5m),
                ["string"] = new LiteralString("x"),
                ["boolean"] = new LiteralBoolean(true),
                ["date"] = new LiteralDate(new DateOnly(2024, 2, 29)),
                ["time"] = new LiteralTime(new TimeOnly(13, 5, 0)),
                ["datetime"] = new LiteralDatetime(
                    new DateTimeOffset(2024, 1, 1, 10, 0, 0, TimeSpan.Zero)
                ),
                ["uuid"] = new LiteralUuid(new Guid("8C5E1A2B-0000-4000-8000-000000000001")),
            },
            new SelectItemProjectionInteger(
                "integer",
                new IntegerProjection(new ParamInteger("integer"))
            ),
            new SelectItemProjectionDecimal(
                "decimal",
                new ExactDecimalProjection(new ParamDecimal("decimal"))
            ),
            new SelectItemProjectionString(
                "string",
                new StringProjection(new ParamString("string"))
            ),
            new SelectItemProjectionBoolean(
                "boolean",
                new BooleanProjection(new ParamBoolean("boolean"))
            ),
            new SelectItemProjectionDate("date", new DateProjection(new ParamDate("date"))),
            new SelectItemProjectionTime("time", new TimeProjection(new ParamTime("time"))),
            new SelectItemProjectionDatetime(
                "datetime",
                new DatetimeProjection(new ParamDatetime("datetime"))
            ),
            new SelectItemProjectionUuid("uuid", new UuidProjection(new ParamUuid("uuid")))
        );

        IRow[] row = [projection.First()];
        Assert.Equal(["7"], ResultColumn.Texts(row, "integer"));
        Assert.Equal(["2.5"], ResultColumn.Texts(row, "decimal"));
        Assert.Equal(["x"], ResultColumn.Texts(row, "string"));
        Assert.Equal(["True"], ResultColumn.Texts(row, "boolean"));
        Assert.Equal(["2024-02-29"], ResultColumn.Texts(row, "date"));
        Assert.Equal(["13:05:00"], ResultColumn.Texts(row, "time"));
        Assert.Equal(["2024-01-01T10:00:00"], ResultColumn.Texts(row, "datetime"));
        Assert.Equal(
            ["8c5e1a2b-0000-4000-8000-000000000001"],
            ResultColumn.Texts(row, "uuid")
        );
    }

    [Fact]
    public void IntegerLiteralBindsADecimalParameter()
    {
        PureQLProjection projection = Selecting(
            new Dictionary<string, ILiteral> { ["decimal"] = new LiteralInteger(3) },
            new SelectItemProjectionDecimal(
                "decimal",
                new ExactDecimalProjection(new ParamDecimal("decimal"))
            )
        );

        Assert.Equal(["3", "3", "3"], ResultColumn.Texts(projection, "decimal"));
    }

    [Fact]
    public void DatetimeParameterIsTheInstantItDenotesInUtc()
    {
        PureQLProjection projection = Selecting(
            new Dictionary<string, ILiteral>
            {
                ["at"] = new LiteralDatetime(
                    new DateTimeOffset(2024, 1, 1, 3, 0, 0, TimeSpan.FromHours(3))
                ),
            },
            new SelectItemProjectionDatetime(
                "at",
                new DatetimeProjection(new ParamDatetime("at"))
            )
        );

        Assert.Equal("2024-01-01T00:00:00", ResultColumn.Texts(projection, "at")[0]);
    }

    [Fact]
    public void WhereReadsABoundParameterHeldAsACapturedVariable()
    {
        PureQLProjection projection = new(
            [Names],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.names", "n")),
                    [Column("name", new StringProjection(new FieldString("n", "name")))],
                    subqueries: null,
                    joins: null,
                    where: new BooleanRow(
                        new ComparisonRow(
                            new GreaterThanRow(
                                new GreaterThanDecimalRow(
                                    new DecimalNullableRow(
                                        new IntegerRow(new FieldInteger("n", "id"))
                                    ),
                                    new DecimalNullableRow(
                                        new IntegerRow(new ParamInteger("min_id"))
                                    )
                                )
                            )
                        )
                    ),
                    orderBy: null,
                    pagination: null,
                    distinctBy: null
                )
            ),
            new Dictionary<string, ILiteral> { ["min_id"] = new LiteralInteger(1) }
        );

        Assert.Contains("@min_id", TreeText.Of(projection.Expression), StringComparison.Ordinal);
        Assert.Equal(["Bob", "Cid"], ResultColumn.Texts(projection, "name"));
    }

    [Fact]
    public void InReadsTheItemsOfABoundListParameterAsTheyWereWhenBound()
    {
        List<string> wanted = ["Ann", "Cid"];
        PureQLProjection projection = new(
            [Names],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.names")),
                    [
                        Column(
                            "name",
                            new StringProjection(new FieldString("t.names", "name"))
                        ),
                    ],
                    subqueries: null,
                    joins: null,
                    where: new BooleanRow(
                        new ComparisonRow(
                            new InRow(
                                new InStringRow(
                                    new StringNullableRow(
                                        new StringRow(
                                            new FieldString("t.names", "name")
                                        )
                                    ),
                                    new ListString(new ListParamString("wanted"))
                                )
                            )
                        )
                    ),
                    orderBy: null,
                    pagination: null,
                    distinctBy: null
                )
            ),
            new Dictionary<string, ILiteral> { ["wanted"] = new ListLiteralString(wanted) }
        );

        wanted.Add("Bob");

        Assert.Equal(["Ann", "Cid"], ResultColumn.Texts(projection, "name"));
    }

    [Fact]
    public void PaginationReadsBoundParameters()
    {
        PureQLProjection projection = new(
            [Names],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.names")),
                    [
                        Column(
                            "name",
                            new StringProjection(new FieldString("t.names", "name"))
                        ),
                    ],
                    subqueries: null,
                    joins: null,
                    where: null,
                    orderBy: null,
                    pagination: new Pagination(
                        new ParamInteger("skip"),
                        new ParamInteger("take")
                    ),
                    distinctBy: null
                )
            ),
            new Dictionary<string, ILiteral>
            {
                ["skip"] = new LiteralInteger(1),
                ["take"] = new LiteralInteger(1),
            }
        );

        Assert.Equal(["Bob"], ResultColumn.Texts(projection, "name"));
    }

    [Fact]
    public void QueryWithoutSubqueriesBindsParameters()
    {
        PureQLProjection projection = new(
            [Names],
            new Query(
                new PlainQuery(
                    new From(new FromEntity("t.names")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "integer",
                                    new IntegerProjection(new ParamInteger("integer"))
                                )
                            )
                        ),
                    ]
                )
            ),
            new Dictionary<string, ILiteral> { ["integer"] = new LiteralInteger(4) }
        );

        Assert.Equal(["4", "4", "4"], ResultColumn.Texts(projection, "integer"));
    }

    [Fact]
    public void NullableParameterBindsNull()
    {
        PureQLProjection projection = Selecting(
            new Dictionary<string, ILiteral> { ["name"] = new LiteralStringNullable() },
            new SelectItemProjectionStringNullable(
                "name",
                new ExactStringNullableProjection(new ParamStringNullable("name"))
            )
        );

        Assert.Equal(["", "", ""], ResultColumn.Texts(projection, "name"));
    }

    [Fact]
    public void ValuesOfParametersTheQueryDoesNotReadAreIgnored()
    {
        PureQLProjection projection = Selecting(
            new Dictionary<string, ILiteral>
            {
                ["integer"] = new LiteralInteger(1),
                ["unused"] = new LiteralString("anything"),
            },
            new SelectItemProjectionInteger(
                "integer",
                new IntegerProjection(new ParamInteger("integer"))
            )
        );

        Assert.Equal(["1", "1", "1"], ResultColumn.Texts(projection, "integer"));
    }

    private static SelectItemProjection Column(string alias, StringProjection expression)
    {
        return new SelectItemProjection(
            new SelectItemProjectionNonNullable(
                new SelectItemProjectionString(alias, expression)
            )
        );
    }

    private static PureQLProjection Selecting(
        IReadOnlyDictionary<string, ILiteral> values,
        params object[] columns
    )
    {
        return new PureQLProjection(
            [Names],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.names")),
                    [.. columns.Select(Item)]
                )
            ),
            values
        );
    }

    private static SelectItemProjection Item(object column)
    {
        return column switch
        {
            SelectItemProjectionInteger item => new SelectItemProjection(
                new SelectItemProjectionNonNullable(item)
            ),
            SelectItemProjectionDecimal item => new SelectItemProjection(
                new SelectItemProjectionNonNullable(item)
            ),
            SelectItemProjectionString item => new SelectItemProjection(
                new SelectItemProjectionNonNullable(item)
            ),
            SelectItemProjectionBoolean item => new SelectItemProjection(
                new SelectItemProjectionNonNullable(item)
            ),
            SelectItemProjectionDate item => new SelectItemProjection(
                new SelectItemProjectionNonNullable(item)
            ),
            SelectItemProjectionTime item => new SelectItemProjection(
                new SelectItemProjectionNonNullable(item)
            ),
            SelectItemProjectionDatetime item => new SelectItemProjection(
                new SelectItemProjectionNonNullable(item)
            ),
            SelectItemProjectionUuid item => new SelectItemProjection(
                new SelectItemProjectionNonNullable(item)
            ),
            SelectItemProjectionStringNullable item => new SelectItemProjection(
                new SelectItemProjectionNullable(item)
            ),
            _ => throw new ArgumentException($"{column.GetType().Name} is not a column."),
        };
    }
}
