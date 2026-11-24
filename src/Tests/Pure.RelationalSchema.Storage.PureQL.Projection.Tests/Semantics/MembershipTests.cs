using Pure.Primitives.Number;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.ListParameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// in compares with equal: integer and decimal by value, and a null value is
// in a list exactly when the list holds a null, which only a nullable
// subquery column can.
public sealed class MembershipTests
{
    private static readonly IStoredSchemaDataSet Items = new InlineTable(
        "t",
        "items",
        ("code", new StringColumnType()),
        ("weight", new DoubleColumnType())
    ).With(["a", "2"], ["", "2.5"], ["b", "3"]);

    [Fact]
    public void DecimalValueIsInAnIntegerListByNumericValue()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items"),
                select:
                [
                    new SelectItemProjection(
                        "listed",
                        new InDecimalProjection(
                            new FieldDecimal("t.items", "weight"),
                            new ListLiteralInteger([2, 3])
                        )
                    ),
                ]
            )
        );

        Assert.Equal(["True", "False", "True"], ResultColumn.Texts(projection, "listed"));
    }

    [Fact]
    public void DecimalValueIsInABoundIntegerListParameterByNumericValue()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items"),
                select:
                [
                    new SelectItemProjection(
                        "listed",
                        new InDecimalProjection(
                            new FieldDecimal("t.items", "weight"),
                            new ListParamInteger("weights")
                        )
                    ),
                ]
            ),
            new Dictionary<string, IParameter>
            {
                ["weights"] = new IntegerListParameter([new Long(2), new Long(3)]),
            }
        );

        Assert.Equal(["True", "False", "True"], ResultColumn.Texts(projection, "listed"));
    }

    [Fact]
    public void NullIsInASubqueryColumnHoldingNullButNotInALiteralList()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "codes",
                        new PlainQueryWithoutPaginationWithoutOrderBy(
                            from: new FromEntity("t.items"),
                            where: new NotEqualStringRow(
                                new FieldStringNullable("t.items", "code"),
                                new LiteralString("b")
                            ),
                            select:
                            [
                                new SelectItemProjection(
                                    "code",
                                    new FieldStringNullable("t.items", "code")
                                ),
                            ]
                        )
                    ),
                ],
                from: new FromEntity("t.items"),
                select:
                [
                    new SelectItemProjection(
                        "in_codes",
                        new InStringProjection(
                            new FieldStringNullable("t.items", "code"),
                            new ListSubqueryColumnStringNullable("codes", "code")
                        )
                    ),
                    new SelectItemProjection(
                        "in_literal",
                        new InStringProjection(
                            new FieldStringNullable("t.items", "code"),
                            new ListLiteralString(["a", "c"])
                        )
                    ),
                ]
            )
        );

        Assert.Equal(
            ["True", "True", "False"],
            ResultColumn.Texts(projection, "in_codes")
        );
        Assert.Equal(
            ["True", "False", "False"],
            ResultColumn.Texts(projection, "in_literal")
        );
    }
}
