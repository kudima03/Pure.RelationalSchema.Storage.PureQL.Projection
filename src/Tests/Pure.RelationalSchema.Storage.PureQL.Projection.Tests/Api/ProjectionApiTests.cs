using System.Collections;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.SelectItems;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Api;

// PureQLProjection as a stored table dataset: its schema, its enumerators
// and its LINQ provider.
public sealed class ProjectionApiTests
{
    private static readonly IStoredSchemaDataSet Counters = new InlineTable(
        "t",
        "counters",
        ("small", new IntColumnType()),
        ("large", new ULongColumnType()),
        ("ratio", new FloatColumnType()),
        ("on", new BoolColumnType())
    ).With(["1", "2", "0.5", "true"], ["3", "4", "1E-3", "False"]);

    [Fact]
    public void TableSchemaIsAnUnnamedTableOfTheSelectedColumns()
    {
        PureQLProjection projection = new(
            [Counters],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.counters")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "small",
                                    new IntegerProjection(
                                        new FieldInteger("t.counters", "small")
                                    )
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionDecimal(
                                    "ratio",
                                    new DecimalProjection(
                                        new FieldAsDecimal(
                                            new FieldDecimal("t.counters", "ratio")
                                        )
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        Assert.Equal(string.Empty, projection.TableSchema.Name.TextValue);
        Assert.Empty(projection.TableSchema.Indexes);
        Assert.Equal(
            ["small:long", "ratio:double"],
            projection.TableSchema.Columns.Select(column =>
                $"{column.Name.TextValue}:{column.Type.Name.TextValue}"
            )
        );
    }

    [Fact]
    public void IntegralFloatingAndBooleanColumnsAreRead()
    {
        PureQLProjection projection = new(
            [Counters],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.counters")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "large",
                                    new IntegerProjection(
                                        new FieldInteger("t.counters", "large")
                                    )
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionDecimal(
                                    "ratio",
                                    new DecimalProjection(
                                        new FieldAsDecimal(
                                            new FieldDecimal("t.counters", "ratio")
                                        )
                                    )
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionBoolean(
                                    "on",
                                    new BooleanProjection(
                                        new FieldBoolean("t.counters", "on")
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        Assert.Equal(["2", "4"], ResultColumn.Texts(projection, "large"));
        Assert.Equal(["0.5", "0.001"], ResultColumn.Texts(projection, "ratio"));
        Assert.Equal(["True", "False"], ResultColumn.Texts(projection, "on"));
    }

    [Fact]
    public void MalformedCellFailsTheQuery()
    {
        PureQLProjection projection = new(
            [
                new InlineTable("t", "broken", ("n", new LongColumnType())).With(
                    ["seven"]
                ),
            ],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.broken")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "n",
                                    new IntegerProjection(new FieldInteger("t.broken", "n"))
                                )
                            )
                        ),
                    ]
                )
            )
        );

        _ = Assert.Throws<FormatException>(() => projection.ToList());
    }

    [Fact]
    public void SubqueryTypedQueryRunsAsAMainQuery()
    {
        PureQLProjection projection = new(
            [Counters],
            new Query(
                new PlainQuery(
                    new From(new FromEntity("t.counters", "c")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "small",
                                    new IntegerProjection(new FieldInteger("c", "small"))
                                )
                            )
                        ),
                    ]
                )
            )
        );

        Assert.Equal(["1", "3"], ResultColumn.Texts(projection, "small"));
    }

    [Fact]
    public async Task AsyncEnumerationYieldsTheSameRows()
    {
        PureQLProjection projection = new(
            [Counters],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.counters")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "small",
                                    new IntegerProjection(
                                        new FieldInteger("t.counters", "small")
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        List<IRow> rows = [];

        await foreach (IRow row in projection)
        {
            rows.Add(row);
        }

        Assert.Equal(["1", "3"], ResultColumn.Texts(rows, "small"));
    }

    [Fact]
    public void LinqOverTheProjectionComposesWithItsProvider()
    {
        PureQLProjection projection = new(
            [Counters],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.counters")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionString(
                                    "label",
                                    new StringProjection(new LiteralString("x"))
                                )
                            )
                        ),
                    ]
                )
            )
        );

        Assert.Equal(typeof(IRow), projection.ElementType);
        Assert.Equal(1, projection.Provider.Execute<int>(
            System.Linq.Expressions.Expression.Call(
                typeof(Queryable),
                nameof(Queryable.Count),
                [typeof(IRow)],
                projection.Expression
            )
        ));

        IEnumerator untyped = ((IEnumerable)projection).GetEnumerator();
        Assert.True(untyped.MoveNext());
        Assert.False(untyped.MoveNext());
    }
}
