using Pure.Primitives.Abstractions.String;
using Pure.Primitives.Bool;
using Pure.Primitives.Date;
using Pure.Primitives.Number;
using Pure.Primitives.Time;
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
using DateTime = Pure.Primitives.DateTime.DateTime;
using Decimal = Pure.Primitives.Number.Decimal;
using Guid = Pure.Primitives.Guid.Guid;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Parameters bound by name to Pure parameters read their values wherever
// they stand: in an expression, as the list of in and in pagination. A Pure
// parameter has its query parameter's type, an integer (list) standing for a
// decimal (list), a typed null binds a nullable parameter, and a datetime is
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
            new Dictionary<string, IParameter>
            {
                ["integer"] = new IntegerParameter(new Long(-7)),
                ["decimal"] = new DecimalParameter(new Decimal(2.50m)),
                ["string"] = new StringParameter(new String("x")),
                ["boolean"] = new BooleanParameter(new True()),
                ["date"] = new DateParameter(new Date(new DateOnly(2024, 2, 29))),
                ["time"] = new TimeParameter(new Time(new TimeOnly(13, 5, 0, 250))),
                ["datetime"] = new DateTimeParameter(
                    new DateTime(
                        new Date(new DateOnly(2024, 1, 1)),
                        new Time(new TimeOnly(10, 0, 0))
                    )
                ),
                ["uuid"] = new UuidParameter(
                    new Guid(new System.Guid("8C5E1A2B-0000-4000-8000-000000000001"))
                ),
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
        Assert.Equal(["-7"], ResultColumn.Texts(row, "integer"));
        Assert.Equal(["2.5"], ResultColumn.Texts(row, "decimal"));
        Assert.Equal(["x"], ResultColumn.Texts(row, "string"));
        Assert.Equal(["True"], ResultColumn.Texts(row, "boolean"));
        Assert.Equal(["2024-02-29"], ResultColumn.Texts(row, "date"));
        Assert.Equal(["13:05:00.25"], ResultColumn.Texts(row, "time"));
        Assert.Equal(["2024-01-01T10:00:00"], ResultColumn.Texts(row, "datetime"));
        Assert.Equal(
            ["8c5e1a2b-0000-4000-8000-000000000001"],
            ResultColumn.Texts(row, "uuid")
        );
    }

    [Fact]
    public void TypedNullsBindNullableParameters()
    {
        PureQLProjection projection = Selecting(
            new Dictionary<string, IParameter>
            {
                ["integer"] = new NullableIntegerParameter(),
                ["decimal"] = new NullableDecimalParameter(),
                ["string"] = new NullableStringParameter(),
                ["boolean"] = new NullableBooleanParameter(),
                ["date"] = new NullableDateParameter(),
                ["time"] = new NullableTimeParameter(),
                ["datetime"] = new NullableDateTimeParameter(),
                ["uuid"] = new NullableUuidParameter(),
            },
            new SelectItemProjectionIntegerNullable(
                "integer",
                new ExactIntegerNullableProjection(new ParamIntegerNullable("integer"))
            ),
            new SelectItemProjectionDecimalNullable(
                "decimal",
                new ExactDecimalNullableProjection(new ParamDecimalNullable("decimal"))
            ),
            new SelectItemProjectionStringNullable(
                "string",
                new ExactStringNullableProjection(new ParamStringNullable("string"))
            ),
            new SelectItemProjectionBooleanNullable(
                "boolean",
                new ExactBooleanNullableProjection(new ParamBooleanNullable("boolean"))
            ),
            new SelectItemProjectionDateNullable(
                "date",
                new ExactDateNullableProjection(new ParamDateNullable("date"))
            ),
            new SelectItemProjectionTimeNullable(
                "time",
                new ExactTimeNullableProjection(new ParamTimeNullable("time"))
            ),
            new SelectItemProjectionDatetimeNullable(
                "datetime",
                new ExactDatetimeNullableProjection(new ParamDatetimeNullable("datetime"))
            ),
            new SelectItemProjectionUuidNullable(
                "uuid",
                new ExactUuidNullableProjection(new ParamUuidNullable("uuid"))
            )
        );

        IRow[] row = [projection.First()];
        string[] names =
        [
            "integer",
            "decimal",
            "string",
            "boolean",
            "date",
            "time",
            "datetime",
            "uuid",
        ];
        Assert.All(
            names,
            name => Assert.Equal([""], ResultColumn.Texts(row, name))
        );
    }

    [Fact]
    public void IntegerParameterBindsADecimalParameter()
    {
        PureQLProjection projection = Selecting(
            new Dictionary<string, IParameter>
            {
                ["decimal"] = new IntegerParameter(new Long(3)),
            },
            new SelectItemProjectionDecimal(
                "decimal",
                new ExactDecimalProjection(new ParamDecimal("decimal"))
            )
        );

        Assert.Equal(["3", "3", "3"], ResultColumn.Texts(projection, "decimal"));
    }

    [Fact]
    public void ListParametersReadTheirBoundItems()
    {
        PureQLProjection projection = Selecting(
            new Dictionary<string, IParameter>
            {
                ["integers"] = new IntegerListParameter([new Long(1), new Long(-2)]),
                ["decimals"] = new DecimalListParameter([new Decimal(2.50m)]),
                ["booleans"] = new BooleanListParameter([new False()]),
                ["dates"] = new DateListParameter([new Date(new DateOnly(2024, 2, 29))]),
                ["times"] = new TimeListParameter([new Time(new TimeOnly(13, 5))]),
                ["datetimes"] = new DateTimeListParameter(
                    [
                        new DateTime(
                            new Date(new DateOnly(2024, 1, 1)),
                            new Time(new TimeOnly(10, 0))
                        ),
                    ]
                ),
                ["uuids"] = new UuidListParameter(
                    [new Guid(new System.Guid("8C5E1A2B-0000-4000-8000-000000000001"))]
                ),
                ["empty"] = new StringListParameter([]),
                ["integer"] = new IntegerParameter(new Long(-2)),
                ["decimal"] = new DecimalParameter(new Decimal(2.5m)),
                ["boolean"] = new BooleanParameter(new False()),
                ["date"] = new DateParameter(new Date(new DateOnly(2024, 2, 29))),
                ["time"] = new TimeParameter(new Time(new TimeOnly(13, 5))),
                ["datetime"] = new DateTimeParameter(
                    new DateTime(
                        new Date(new DateOnly(2024, 1, 1)),
                        new Time(new TimeOnly(10, 0))
                    )
                ),
                ["uuid"] = new UuidParameter(
                    new Guid(new System.Guid("8C5E1A2B-0000-4000-8000-000000000001"))
                ),
                ["string"] = new StringParameter(new String("Ann")),
            },
            In(
                "integer",
                new InProjection(
                    new InDecimalProjection(
                        new DecimalNullableProjection(
                            new IntegerProjection(new ParamInteger("integer"))
                        ),
                        new ListDecimal(new ListInteger(new ListParamInteger("integers")))
                    )
                )
            ),
            In(
                "decimal",
                new InProjection(
                    new InDecimalProjection(
                        new DecimalNullableProjection(
                            new ExactDecimalProjection(new ParamDecimal("decimal"))
                        ),
                        new ListDecimal(new ListParamDecimal("decimals"))
                    )
                )
            ),
            In(
                "boolean",
                new InProjection(
                    new InBooleanProjection(
                        new BooleanNullableProjection(
                            new BooleanProjection(new ParamBoolean("boolean"))
                        ),
                        new ListBoolean(new ListParamBoolean("booleans"))
                    )
                )
            ),
            In(
                "date",
                new InProjection(
                    new InDateProjection(
                        new DateNullableProjection(new DateProjection(new ParamDate("date"))),
                        new ListDate(new ListParamDate("dates"))
                    )
                )
            ),
            In(
                "time",
                new InProjection(
                    new InTimeProjection(
                        new TimeNullableProjection(new TimeProjection(new ParamTime("time"))),
                        new ListTime(new ListParamTime("times"))
                    )
                )
            ),
            In(
                "datetime",
                new InProjection(
                    new InDatetimeProjection(
                        new DatetimeNullableProjection(
                            new DatetimeProjection(new ParamDatetime("datetime"))
                        ),
                        new ListDatetime(new ListParamDatetime("datetimes"))
                    )
                )
            ),
            In(
                "uuid",
                new InProjection(
                    new InUuidProjection(
                        new UuidNullableProjection(new UuidProjection(new ParamUuid("uuid"))),
                        new ListUuid(new ListParamUuid("uuids"))
                    )
                )
            ),
            In(
                "empty",
                new InProjection(
                    new InStringProjection(
                        new StringNullableProjection(
                            new StringProjection(new ParamString("string"))
                        ),
                        new ListString(new ListParamString("empty"))
                    )
                )
            )
        );

        IRow[] row = [projection.First()];
        string[] found = ["integer", "decimal", "boolean", "date", "time", "datetime", "uuid"];
        Assert.All(found, name => Assert.Equal(["True"], ResultColumn.Texts(row, name)));
        Assert.Equal(["False"], ResultColumn.Texts(row, "empty"));
    }

    [Fact]
    public void IntegerListParameterBindsADecimalListParameter()
    {
        PureQLProjection projection = Selecting(
            new Dictionary<string, IParameter>
            {
                ["decimal"] = new DecimalParameter(new Decimal(2m)),
                ["integers"] = new IntegerListParameter([new Long(2)]),
            },
            In(
                "found",
                new InProjection(
                    new InDecimalProjection(
                        new DecimalNullableProjection(
                            new ExactDecimalProjection(new ParamDecimal("decimal"))
                        ),
                        new ListDecimal(new ListParamDecimal("integers"))
                    )
                )
            )
        );

        Assert.Equal(["True", "True", "True"], ResultColumn.Texts(projection, "found"));
    }

    [Fact]
    public void DatetimeTextWithAnOffsetIsTheInstantItDenotesInUtc()
    {
        PureQLProjection projection = Selecting(
            new Dictionary<string, IParameter>
            {
                ["at"] = new TextParameter("datetime", "2024-01-01T03:00:00+03:00"),
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
            new Dictionary<string, IParameter>
            {
                ["min_id"] = new IntegerParameter(new Long(1)),
            }
        );

        Assert.Contains("@min_id", TreeText.Of(projection.Expression), StringComparison.Ordinal);
        Assert.Equal(["Bob", "Cid"], ResultColumn.Texts(projection, "name"));
    }

    [Fact]
    public void InReadsTheItemsOfABoundListParameterAsTheyWereWhenBound()
    {
        List<IString> wanted = [new String("Ann"), new String("Cid")];
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
            new Dictionary<string, IParameter>
            {
                ["wanted"] = new StringListParameter(wanted),
            }
        );

        wanted.Add(new String("Bob"));

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
            new Dictionary<string, IParameter>
            {
                ["skip"] = new IntegerParameter(new Long(1)),
                ["take"] = new IntegerParameter(new Long(1)),
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
            new Dictionary<string, IParameter>
            {
                ["integer"] = new IntegerParameter(new Long(4)),
            }
        );

        Assert.Equal(["4", "4", "4"], ResultColumn.Texts(projection, "integer"));
    }

    [Fact]
    public void ValuesOfParametersTheQueryDoesNotReadAreIgnored()
    {
        PureQLProjection projection = Selecting(
            new Dictionary<string, IParameter>
            {
                ["integer"] = new IntegerParameter(new Long(1)),
                ["unused"] = new TextParameter("no such type", "anything"),
            },
            new SelectItemProjectionInteger(
                "integer",
                new IntegerProjection(new ParamInteger("integer"))
            )
        );

        Assert.Equal(["1", "1", "1"], ResultColumn.Texts(projection, "integer"));
    }

    // A boolean column that is whether its in holds.
    private static SelectItemProjectionBoolean In(string alias, InProjection condition)
    {
        return new SelectItemProjectionBoolean(
            alias,
            new BooleanProjection(new ComparisonProjection(condition))
        );
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
        IReadOnlyDictionary<string, IParameter> values,
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
            SelectItemProjectionIntegerNullable item => new SelectItemProjection(
                new SelectItemProjectionNullable(item)
            ),
            SelectItemProjectionDecimalNullable item => new SelectItemProjection(
                new SelectItemProjectionNullable(item)
            ),
            SelectItemProjectionStringNullable item => new SelectItemProjection(
                new SelectItemProjectionNullable(item)
            ),
            SelectItemProjectionBooleanNullable item => new SelectItemProjection(
                new SelectItemProjectionNullable(item)
            ),
            SelectItemProjectionDateNullable item => new SelectItemProjection(
                new SelectItemProjectionNullable(item)
            ),
            SelectItemProjectionTimeNullable item => new SelectItemProjection(
                new SelectItemProjectionNullable(item)
            ),
            SelectItemProjectionDatetimeNullable item => new SelectItemProjection(
                new SelectItemProjectionNullable(item)
            ),
            SelectItemProjectionUuidNullable item => new SelectItemProjection(
                new SelectItemProjectionNullable(item)
            ),
            _ => throw new ArgumentException($"{column.GetType().Name} is not a column."),
        };
    }
}
