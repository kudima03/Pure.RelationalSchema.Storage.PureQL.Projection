using Pure.Primitives.Abstractions.Bool;
using Pure.Primitives.Abstractions.String;
using Pure.Primitives.Bool;
using Pure.Primitives.Date;
using Pure.Primitives.Number;
using Pure.Primitives.Time;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.ParameterType;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Parameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.SelectItems;
using DateTime = Pure.Primitives.DateTime.DateTime;
using Decimal = Pure.Primitives.Number.Decimal;
using Guid = Pure.Primitives.Guid.Guid;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Pure parameters bind a query's parameters as the literals they stand for
// do: each reads its value, a typed null binds a nullable parameter, an
// integer a decimal one, and a datetime is a UTC instant.
public sealed class PureParameterBindingTests
{
    private static readonly IStoredSchemaDataSet Names = new InlineTable(
        "t",
        "names",
        ("name", new StringColumnType())
    ).With(["Ann"]);

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

        Assert.Equal(["3"], ResultColumn.Texts(projection, "decimal"));
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
                        Item(
                            new SelectItemProjectionInteger(
                                "integer",
                                new IntegerProjection(new ParamInteger("integer"))
                            )
                        ),
                    ]
                )
            ),
            new Dictionary<string, IParameter>
            {
                ["integer"] = new IntegerParameter(new Long(42)),
            }
        );

        Assert.Equal(["42"], ResultColumn.Texts(projection, "integer"));
    }

    [Fact]
    public void ParameterOfAnotherTypeIsRejectedWhenTheProjectionIsBuilt()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            Selecting(
                new Dictionary<string, IParameter>
                {
                    ["integer"] = new StringParameter(new String("7")),
                },
                new SelectItemProjectionInteger(
                    "integer",
                    new IntegerProjection(new ParamInteger("integer"))
                )
            )
        );

        Assert.Equal(
            "Parameter 'integer' is integer and cannot be bound to a string literal.",
            exception.Message
        );
    }

    [Fact]
    public void TypedNullIsRejectedForAParameterThatIsNotNullable()
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            Selecting(
                new Dictionary<string, IParameter>
                {
                    ["integer"] = new NullableIntegerParameter(),
                },
                new SelectItemProjectionInteger(
                    "integer",
                    new IntegerProjection(new ParamInteger("integer"))
                )
            )
        );

        Assert.Equal(
            "Parameter 'integer' is integer and cannot be bound to a null integer literal.",
            exception.Message
        );
    }

    [Theory]
    [InlineData("integer", "seven", "'seven' is not a integer")]
    [InlineData("integer", "", "'' is not a integer")]
    [InlineData("integer", "9223372036854775808", "is out of its range")]
    [InlineData("decimal", "1e400", "is out of its range")]
    [InlineData("boolean", "yes", "'yes' is not a boolean")]
    [InlineData("date", "2024-02-30", "'2024-02-30' is not a date")]
    [InlineData("time", "25:00", "'25:00' is not a time")]
    [InlineData("datetime", "noon", "'noon' is not a datetime")]
    [InlineData("uuid", "8c5e1a2b", "'8c5e1a2b' is not a uuid")]
    public void ValueThatIsNotOfItsTypeIsRejected(string type, string text, string message)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            Selecting(
                new Dictionary<string, IParameter>
                {
                    ["p"] = new TextParameter(type, false, text),
                },
                Named("p")
            )
        );

        Assert.Contains($"Parameter 'p' is {type} and its value ", exception.Message);
        Assert.Contains(message, exception.Message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void TypeThatIsNotAScalarPureQLTypeIsRejected(bool nullable)
    {
        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            Selecting(
                new Dictionary<string, IParameter>
                {
                    ["p"] = new TextParameter("integerList", nullable, "[1]"),
                },
                Named("p")
            )
        );

        Assert.Equal(
            "Parameter 'p' has the type 'integerList', which is not a scalar PureQL type.",
            exception.Message
        );
    }

    [Fact]
    public void DatetimeTextWithAnOffsetIsTheInstantItDenotesInUtc()
    {
        PureQLProjection projection = Selecting(
            new Dictionary<string, IParameter>
            {
                ["at"] = new TextParameter(
                    "datetime",
                    false,
                    "2024-01-01T03:00:00+03:00"
                ),
            },
            new SelectItemProjectionDatetime(
                "at",
                new DatetimeProjection(new ParamDatetime("at"))
            )
        );

        Assert.Equal(["2024-01-01T00:00:00"], ResultColumn.Texts(projection, "at"));
    }

    // A column reading the string parameter of the name, so a parameter whose
    // value cannot be read is bound.
    private static SelectItemProjectionString Named(string name)
    {
        return new SelectItemProjectionString(
            name,
            new StringProjection(new ParamString(name))
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

    // A parameter that holds any text as a value of any type name, as a
    // parameter not built by Pure.RelationalSchema.Storage.PureQL.Projection.Parameters
    // may.
    private sealed record TextParameter(string Name, bool IsNullable, string Text)
        : IParameter
    {
        public IParameterType Type => new TextParameterType(Name, IsNullable);

        public IString Value => new String(Text);
    }

    private sealed record TextParameterType(string TypeName, bool IsNullable)
        : IParameterType
    {
        public IString Name => new String(TypeName);

        public IBool Nullable => IsNullable ? new True() : new False();
    }
}
