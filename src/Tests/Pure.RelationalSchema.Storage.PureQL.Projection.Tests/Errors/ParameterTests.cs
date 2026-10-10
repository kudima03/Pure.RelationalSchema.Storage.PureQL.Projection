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

// PureQLProjection has no way to bind parameter values, so a query fails
// when it evaluates a parameter, wherever the parameter stands.
public sealed class ParameterTests
{
    private static readonly IStoredSchemaDataSet Names = new InlineTable(
        "t",
        "names",
        ("name", new StringColumnType())
    ).With(["Ann"], ["Bob"]);

    [Fact]
    public void ListParameterCannotBeBound()
    {
        PureQLProjection projection = new(
            [Names],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.names")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionString(
                                    "name",
                                    new StringProjection(new FieldString("t.names", "name"))
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins: null,
                    where: new BooleanRow(
                        new ComparisonRow(
                            new InRow(
                                new InStringRow(
                                    new StringNullableRow(
                                        new FieldAsStringNullable(
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
                    distinct: false
                )
            )
        );

        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            projection.ToList()
        );
        Assert.Contains("'wanted'", error.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void PaginationParameterCannotBeBound()
    {
        PureQLProjection projection = new(
            [Names],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.names")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionString(
                                    "name",
                                    new StringProjection(new FieldString("t.names", "name"))
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins: null,
                    where: null,
                    orderBy: null,
                    pagination: new Pagination(0, new ParamInteger("page_size")),
                    distinct: false
                )
            )
        );

        NotSupportedException error = Assert.Throws<NotSupportedException>(() =>
            projection.ToList()
        );
        Assert.Contains("'page_size'", error.Message, StringComparison.Ordinal);
    }
}
