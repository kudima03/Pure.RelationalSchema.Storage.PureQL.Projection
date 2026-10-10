using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.SelectItems;

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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionBoolean(
                                    "listed",
                                    new BooleanProjection(
                                        new ComparisonProjection(
                                            new InProjection(
                                                new InDecimalProjection(
                                                    new DecimalNullableProjection(
                                                        new FieldAsDecimalNullable(
                                                            new FieldDecimal(
                                                                "t.items",
                                                                "weight"
                                                            )
                                                        )
                                                    ),
                                                    new ListDecimal(
                                                        new ListInteger(
                                                            new ListLiteralInteger([2, 3])
                                                        )
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                    ]
                )
            )
        );

        Assert.Equal(["True", "False", "True"], ResultColumn.Texts(projection, "listed"));
    }

    [Fact]
    public void NullIsInASubqueryColumnHoldingNullButNotInALiteralList()
    {
        PureQLProjection projection = new(
            [Items],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionBoolean(
                                    "in_codes",
                                    new BooleanProjection(
                                        new ComparisonProjection(
                                            new InProjection(
                                                new InStringProjection(
                                                    new StringNullableProjection(
                                                        new FieldAsStringNullable(
                                                            new FieldStringNullable(
                                                                "t.items",
                                                                "code"
                                                            )
                                                        )
                                                    ),
                                                    new ListString(
                                                        new ListSubqueryColumnString(
                                                            "codes",
                                                            "code",
                                                            true
                                                        )
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionBoolean(
                                    "in_literal",
                                    new BooleanProjection(
                                        new ComparisonProjection(
                                            new InProjection(
                                                new InStringProjection(
                                                    new StringNullableProjection(
                                                        new FieldAsStringNullable(
                                                            new FieldStringNullable(
                                                                "t.items",
                                                                "code"
                                                            )
                                                        )
                                                    ),
                                                    new ListString(
                                                        new ListLiteralString(["a", "c"])
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                    ],
                    subqueries:
                    [
                        new Subquery(
                            "codes",
                            new Query(
                                new PlainQuery(
                                    new From(new FromEntity("t.items")),
                                    [
                                        new SelectItemProjection(
                                            new SelectItemProjectionNullable(
                                                new SelectItemProjectionStringNullable(
                                                    "code",
                                                    new StringNullableProjection(
                                                        new FieldAsStringNullable(
                                                            new FieldStringNullable(
                                                                "t.items",
                                                                "code"
                                                            )
                                                        )
                                                    )
                                                )
                                            )
                                        ),
                                    ],
                                    joins: null,
                                    where: new BooleanRow(
                                        new ComparisonRow(
                                            new NotEqualRow(
                                                new NotEqualStringRow(
                                                    new StringNullableRow(
                                                        new FieldAsStringNullable(
                                                            new FieldStringNullable(
                                                                "t.items",
                                                                "code"
                                                            )
                                                        )
                                                    ),
                                                    new StringNullableRow(
                                                        new LiteralAsStringNullable(
                                                            new LiteralString("b")
                                                        )
                                                    )
                                                )
                                            )
                                        )
                                    ),
                                    orderBy: null,
                                    pagination: null,
                                    distinct: false
                                )
                            )
                        ),
                    ],
                    joins: null,
                    where: null,
                    orderBy: null,
                    pagination: null,
                    distinct: false
                )
            )
        );

        Assert.Equal(["True", "True", "False"], ResultColumn.Texts(projection, "in_codes"));
        Assert.Equal(
            ["True", "False", "False"],
            ResultColumn.Texts(projection, "in_literal")
        );
    }
}
