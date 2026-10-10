using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.GroupExpressions;
using PureQL.CSharp.Model.GroupKeys;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.SelectItems;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Aggregates over all rows and over groups: what they give over no rows,
// how predicates and over: all choose their rows, and that groupBy puts every
// null key in one group.
public sealed class AggregateTests
{
    private static readonly IStoredSchemaDataSet Sales = new InlineTable(
        "t",
        "sales",
        ("region", new StringColumnType()),
        ("amount", new LongColumnType()),
        ("price", new DoubleColumnType())
    ).With(
        ["north", "5", "1.5"],
        ["", "2", "4"],
        ["north", "3", ""],
        ["south", "9223372036854775807", "2.5"],
        ["", "1", "1"]
    );

    [Fact]
    public void AggregatesOverNoRowsGiveTheirEmptyValuesInOneRow()
    {
        PureQLProjection projection = new(
            [Sales],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.sales")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "count",
                                    new IntegerProjection(
                                        new AggregateIntegerProjection(new CountProjection())
                                    )
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "units",
                                    new IntegerProjection(
                                        new AggregateIntegerProjection(
                                            new SumIntegerProjection(
                                                new IntegerNullableRow(
                                                    new FieldAsIntegerNullable(
                                                        new FieldInteger("t.sales", "amount")
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
                                new SelectItemProjectionDecimal(
                                    "revenue",
                                    new DecimalProjection(
                                        new AggregateDecimalProjection(
                                            new SumDecimalProjection(
                                                new DecimalNullableRow(
                                                    new FieldAsDecimalNullable(
                                                        new FieldDecimalNullable(
                                                            "t.sales",
                                                            "price"
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
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionStringNullable(
                                    "first",
                                    new StringNullableProjection(
                                        new AggregateStringProjection(
                                            new MinStringNullableProjection(
                                                new StringNullableRow(
                                                    new FieldAsStringNullable(
                                                        new FieldStringNullable(
                                                            "t.sales",
                                                            "region"
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
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionDecimalNullable(
                                    "mean",
                                    new DecimalNullableProjection(
                                        new AggregateDecimalNullableProjection(
                                            new AverageDecimalNullableProjection(
                                                new DecimalNullableRow(
                                                    new FieldAsDecimalNullable(
                                                        new FieldDecimalNullable(
                                                            "t.sales",
                                                            "price"
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
                                    "any",
                                    new BooleanProjection(
                                        new AggregateBooleanProjection(
                                            new AnyProjection(
                                                new BooleanRow(new LiteralBoolean(true))
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionBoolean(
                                    "all",
                                    new BooleanProjection(
                                        new AggregateBooleanProjection(
                                            new AllProjection(
                                                new BooleanRow(new LiteralBoolean(false))
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins: null,
                    where: new BooleanRow(new LiteralBoolean(false)),
                    orderBy: null,
                    pagination: null,
                    distinct: false
                )
            )
        );

        Assert.Equal(["0"], ResultColumn.Texts(projection, "count"));
        Assert.Equal(["0"], ResultColumn.Texts(projection, "units"));
        Assert.Equal(["0"], ResultColumn.Texts(projection, "revenue"));
        Assert.Equal([""], ResultColumn.Texts(projection, "first"));
        Assert.Equal([""], ResultColumn.Texts(projection, "mean"));
        Assert.Equal(["False"], ResultColumn.Texts(projection, "any"));
        Assert.Equal(["True"], ResultColumn.Texts(projection, "all"));
    }

    [Fact]
    public void IntegerSumBeyondSixtyFourBitsFailsTheQuery()
    {
        PureQLProjection projection = new(
            [Sales],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.sales")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "units",
                                    new IntegerProjection(
                                        new AggregateIntegerProjection(
                                            new SumIntegerProjection(
                                                new IntegerNullableRow(
                                                    new FieldAsIntegerNullable(
                                                        new FieldInteger("t.sales", "amount")
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

        _ = Assert.Throws<OverflowException>(() => projection.ToList());
    }

    [Fact]
    public void GroupsPutNullKeysTogetherAndAggregateOverTheirOwnRowsOrAllRows()
    {
        PureQLProjection projection = new(
            [Sales],
            new PureQLQuery(
                new MainGroupedQuery(
                    new From(new FromEntity("t.sales")),
                    [
                        new GroupKey(
                            new GroupKeyNullable(
                                new GroupKeyStringNullable(
                                    new StringNullableRow(
                                        new FieldAsStringNullable(
                                            new FieldStringNullable("t.sales", "region")
                                        )
                                    )
                                )
                            )
                        ),
                    ],
                    [
                        new SelectItemGroup(
                            new SelectItemGroupNullable(
                                new SelectItemGroupStringNullable(
                                    "region",
                                    new StringNullableGroup(
                                        new KeyAsStringNullable(new KeyStringNullable(0))
                                    )
                                )
                            )
                        ),
                        new SelectItemGroup(
                            new SelectItemGroupNonNullable(
                                new SelectItemGroupInteger(
                                    "smallest",
                                    new IntegerGroup(
                                        new AggregateIntegerGroup(
                                            new MinIntegerGroup(
                                                new IntegerRow(
                                                    new FieldInteger("t.sales", "amount")
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                        new SelectItemGroup(
                            new SelectItemGroupNonNullable(
                                new SelectItemGroupInteger(
                                    "largest_overall",
                                    new IntegerGroup(
                                        new AggregateIntegerGroup(
                                            new CountGroup(
                                                new BooleanRow(
                                                    new ComparisonRow(
                                                        new LessThanRow(
                                                            new LessThanDecimalRow(
                                                                new DecimalNullableRow(
                                                                    new FieldAsDecimalNullable(
                                                                        new FieldInteger(
                                                                            "t.sales",
                                                                            "amount"
                                                                        )
                                                                    )
                                                                ),
                                                                new DecimalNullableRow(
                                                                    new LiteralAsDecimalNullable(
                                                                        new LiteralInteger(4)
                                                                    )
                                                                )
                                                            )
                                                        )
                                                    )
                                                ),
                                                AggregateOver.All
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                        new SelectItemGroup(
                            new SelectItemGroupNonNullable(
                                new SelectItemGroupBoolean(
                                    "all_priced",
                                    new BooleanGroup(
                                        new AggregateBooleanGroup(
                                            new AllGroup(
                                                new BooleanRow(
                                                    new ComparisonRow(
                                                        new NotEqualRow(
                                                            new NotEqualDecimalRow(
                                                                new DecimalNullableRow(
                                                                    new FieldAsDecimalNullable(
                                                                        new FieldDecimalNullable(
                                                                            "t.sales",
                                                                            "price"
                                                                        )
                                                                    )
                                                                ),
                                                                new DecimalNullableRow(
                                                                    new LiteralAsDecimalNullable(
                                                                        new LiteralDecimalNullable()
                                                                    )
                                                                )
                                                            )
                                                        )
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins: null,
                    where: null,
                    having: null,
                    orderBy:
                    [
                        new OrderItemGroup(
                            new ValueGroup(
                                new StringNullableGroup(
                                    new KeyAsStringNullable(new KeyStringNullable(0))
                                )
                            )
                        ),
                    ],
                    pagination: null,
                    distinct: false
                )
            )
        );

        Assert.Equal(["", "north", "south"], ResultColumn.Texts(projection, "region"));
        Assert.Equal(["1", "3", "9223372036854775807"], ResultColumn.Texts(projection, "smallest"));
        Assert.Equal(["3", "3", "3"], ResultColumn.Texts(projection, "largest_overall"));
        Assert.Equal(["True", "False", "True"], ResultColumn.Texts(projection, "all_priced"));
    }

    [Fact]
    public void GroupedQueryOverNoRowsReturnsNoRows()
    {
        PureQLProjection projection = new(
            [Sales],
            new PureQLQuery(
                new MainGroupedQuery(
                    new From(new FromEntity("t.sales")),
                    [
                        new GroupKey(
                            new GroupKeyNullable(
                                new GroupKeyStringNullable(
                                    new StringNullableRow(
                                        new FieldAsStringNullable(
                                            new FieldStringNullable("t.sales", "region")
                                        )
                                    )
                                )
                            )
                        ),
                    ],
                    [
                        new SelectItemGroup(
                            new SelectItemGroupNonNullable(
                                new SelectItemGroupInteger(
                                    "count",
                                    new IntegerGroup(
                                        new AggregateIntegerGroup(new CountGroup())
                                    )
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins: null,
                    where: new BooleanRow(new LiteralBoolean(false)),
                    having: null,
                    orderBy: null,
                    pagination: null,
                    distinct: false
                )
            )
        );

        Assert.Empty(projection.ToList());
    }
}
