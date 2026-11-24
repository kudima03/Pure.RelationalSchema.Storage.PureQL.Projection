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
            new SelectItemProjection("integer", new ParamInteger("integer")),
            new SelectItemProjection("decimal", new ParamDecimal("decimal")),
            new SelectItemProjection("string", new ParamString("string")),
            new SelectItemProjection("boolean", new ParamBoolean("boolean")),
            new SelectItemProjection("date", new ParamDate("date")),
            new SelectItemProjection("time", new ParamTime("time")),
            new SelectItemProjection("datetime", new ParamDatetime("datetime")),
            new SelectItemProjection("uuid", new ParamUuid("uuid"))
        );

        IRow[] row = [projection.First()];
        Assert.Equal(["-7"], ResultColumn.Texts(row, "integer"));
        Assert.Equal(["2.5"], ResultColumn.Texts(row, "decimal"));
        Assert.Equal(["x"], ResultColumn.Texts(row, "string"));
        Assert.Equal(["True"], ResultColumn.Texts(row, "boolean"));
        Assert.Equal(["2024-02-29"], ResultColumn.Texts(row, "date"));
        Assert.Equal(["13:05:00.25"], ResultColumn.Texts(row, "time"));
        Assert.Equal(["2024-01-01T10:00:00Z"], ResultColumn.Texts(row, "datetime"));
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
            new SelectItemProjection("integer", new ParamIntegerNullable("integer")),
            new SelectItemProjection("decimal", new ParamDecimalNullable("decimal")),
            new SelectItemProjection("string", new ParamStringNullable("string")),
            new SelectItemProjection("boolean", new ParamBooleanNullable("boolean")),
            new SelectItemProjection("date", new ParamDateNullable("date")),
            new SelectItemProjection("time", new ParamTimeNullable("time")),
            new SelectItemProjection("datetime", new ParamDatetimeNullable("datetime")),
            new SelectItemProjection("uuid", new ParamUuidNullable("uuid"))
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
        Assert.All(names, name => Assert.Equal([""], ResultColumn.Texts(row, name)));
    }

    [Fact]
    public void IntegerParameterBindsADecimalParameter()
    {
        PureQLProjection projection = Selecting(
            new Dictionary<string, IParameter>
            {
                ["decimal"] = new IntegerParameter(new Long(3)),
            },
            new SelectItemProjection("decimal", new ParamDecimal("decimal"))
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
                ["datetimes"] = new DateTimeListParameter([
                    new DateTime(
                        new Date(new DateOnly(2024, 1, 1)),
                        new Time(new TimeOnly(10, 0))
                    ),
                ]),
                ["uuids"] = new UuidListParameter([
                    new Guid(new System.Guid("8C5E1A2B-0000-4000-8000-000000000001")),
                ]),
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
                new InDecimalProjection(
                    new ParamInteger("integer"),
                    new ListParamInteger("integers")
                )
            ),
            In(
                "decimal",
                new InDecimalProjection(
                    new ParamDecimal("decimal"),
                    new ListParamDecimal("decimals")
                )
            ),
            In(
                "boolean",
                new InBooleanProjection(
                    new ParamBoolean("boolean"),
                    new ListParamBoolean("booleans")
                )
            ),
            In(
                "date",
                new InDateProjection(new ParamDate("date"), new ListParamDate("dates"))
            ),
            In(
                "time",
                new InTimeProjection(new ParamTime("time"), new ListParamTime("times"))
            ),
            In(
                "datetime",
                new InDatetimeProjection(
                    new ParamDatetime("datetime"),
                    new ListParamDatetime("datetimes")
                )
            ),
            In(
                "uuid",
                new InUuidProjection(new ParamUuid("uuid"), new ListParamUuid("uuids"))
            ),
            In(
                "empty",
                new InStringProjection(
                    new ParamString("string"),
                    new ListParamString("empty")
                )
            )
        );

        IRow[] row = [projection.First()];
        string[] found =
        [
            "integer",
            "decimal",
            "boolean",
            "date",
            "time",
            "datetime",
            "uuid",
        ];
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
                new InDecimalProjection(
                    new ParamDecimal("decimal"),
                    new ListParamDecimal("integers")
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
            new SelectItemProjection("at", new ParamDatetime("at"))
        );

        Assert.Equal("2024-01-01T00:00:00Z", ResultColumn.Texts(projection, "at")[0]);
    }

    [Fact]
    public void WhereReadsABoundParameterHeldAsACapturedVariable()
    {
        PureQLProjection projection = new(
            [Names],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.names", "n"),
                where: new GreaterThanDecimalRow(
                    new FieldInteger("n", "id"),
                    new ParamInteger("min_id")
                ),
                select: [Column("name", new FieldString("n", "name"))]
            ),
            new Dictionary<string, IParameter>
            {
                ["min_id"] = new IntegerParameter(new Long(1)),
            }
        );

        Assert.Contains(
            "@min_id",
            TreeText.Of(projection.Expression),
            StringComparison.Ordinal
        );
        Assert.Equal(["Bob", "Cid"], ResultColumn.Texts(projection, "name"));
    }

    [Fact]
    public void InReadsTheItemsOfABoundListParameterAsTheyWereWhenBound()
    {
        List<IString> wanted = [new String("Ann"), new String("Cid")];
        PureQLProjection projection = new(
            [Names],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.names"),
                where: new InStringRow(
                    new FieldString("t.names", "name"),
                    new ListParamString("wanted")
                ),
                select: [Column("name", new FieldString("t.names", "name"))]
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
            new MainPlainQueryWithPagination(
                from: new FromEntity("t.names"),
                select: [Column("name", new FieldString("t.names", "name"))],
                pagination: new Pagination(
                    skip: new ParamInteger("skip"),
                    take: new ParamInteger("take")
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
            new PlainQueryWithoutPaginationWithoutOrderBy(
                from: new FromEntity("t.names"),
                select: [new SelectItemProjection("integer", new ParamInteger("integer"))]
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
            new SelectItemProjection("integer", new ParamInteger("integer"))
        );

        Assert.Equal(["1", "1", "1"], ResultColumn.Texts(projection, "integer"));
    }

    // A boolean column that is whether its in holds.
    private static SelectItemProjection In(string alias, BooleanProjection condition)
    {
        return new SelectItemProjection(alias, condition);
    }

    private static SelectItemProjection Column(string alias, StringProjection expression)
    {
        return new SelectItemProjection(alias, expression);
    }

    private static PureQLProjection Selecting(
        IReadOnlyDictionary<string, IParameter> values,
        params object[] columns
    )
    {
        return new PureQLProjection(
            [Names],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.names"),
                select: [.. columns.Select(Item)]
            ),
            values
        );
    }

    private static SelectItemProjection Item(object column)
    {
        return column switch
        {
            SelectItemProjection item => item,
            SelectItemProjection item => item,
            SelectItemProjection item => item,
            SelectItemProjection item => item,
            SelectItemProjection item => item,
            SelectItemProjection item => item,
            SelectItemProjection item => item,
            SelectItemProjection item => item,
            SelectItemProjection item => item,
            SelectItemProjection item => item,
            SelectItemProjection item => item,
            SelectItemProjection item => item,
            SelectItemProjection item => item,
            SelectItemProjection item => item,
            SelectItemProjection item => item,
            SelectItemProjection item => item,
            _ => throw new ArgumentException($"{column.GetType().Name} is not a column."),
        };
    }
}
