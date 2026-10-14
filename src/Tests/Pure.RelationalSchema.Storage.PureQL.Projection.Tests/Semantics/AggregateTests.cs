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
using PureQL.CSharp.Model.TotalExpressions;

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
    public void TotalQueryOverNoRowsGivesTheEmptyValuesInOneRow()
    {
        PureQLProjection projection = new(
            [Sales],
            new PureQLQuery(
                new MainTotalQuery(
                    new From(new FromEntity("t.sales")),
                    [
                        new SelectItemTotal(
                            new SelectItemTotalNonNullable(
                                new SelectItemTotalInteger(
                                    "count",
                                    new IntegerTotal(
                                        new AggregateIntegerTotal(new CountTotal())
                                    )
                                )
                            )
                        ),
                        new SelectItemTotal(
                            new SelectItemTotalNonNullable(
                                new SelectItemTotalInteger(
                                    "units",
                                    new IntegerTotal(
                                        new AggregateIntegerTotal(
                                            new SumIntegerTotal(
                                                new IntegerNullableRow(
                                                    new IntegerRow(
                                                        new FieldInteger("t.sales", "amount")
                                                    )
                                                )
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                        new SelectItemTotal(
                            new SelectItemTotalNonNullable(
                                new SelectItemTotalDecimal(
                                    "revenue",
                                    new ExactDecimalTotal(
                                        new SumDecimalTotal(
                                            new DecimalNullableRow(
                                                new ExactDecimalNullableRow(
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
                        ),
                        new SelectItemTotal(
                            new SelectItemTotalNullable(
                                new SelectItemTotalStringNullable(
                                    "first",
                                    new ExactStringNullableTotal(
                                        new AggregateStringNullableTotal(
                                            new MinStringNullableTotal(
                                                new StringNullableRow(
                                                    new ExactStringNullableRow(
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
                        new SelectItemTotal(
                            new SelectItemTotalNullable(
                                new SelectItemTotalDecimalNullable(
                                    "mean",
                                    new ExactDecimalNullableTotal(
                                        new AggregateDecimalNullableTotal(
                                            new AverageDecimalNullableTotal(
                                                new DecimalNullableRow(
                                                    new ExactDecimalNullableRow(
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
                        new SelectItemTotal(
                            new SelectItemTotalNonNullable(
                                new SelectItemTotalBoolean(
                                    "any",
                                    new BooleanTotal(
                                        new AggregateBooleanTotal(
                                            new AnyTotal(
                                                new BooleanRow(new LiteralBoolean(true))
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                        new SelectItemTotal(
                            new SelectItemTotalNonNullable(
                                new SelectItemTotalBoolean(
                                    "all",
                                    new BooleanTotal(
                                        new AggregateBooleanTotal(
                                            new AllTotal(
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
                    where: new BooleanRow(new LiteralBoolean(false))
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
    public void PlainQueryRepeatsAggregatesOverAllRowsOnEveryRow()
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
                                        new AggregateIntegerProjection(
                                            new CountProjection()
                                        )
                                    )
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "smallest",
                                    new IntegerProjection(
                                        new AggregateIntegerProjection(
                                            new MinIntegerProjection(
                                                new IntegerRow(
                                                    new FieldInteger("t.sales", "amount")
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

        Assert.Equal(
            ["5", "5", "5", "5", "5"],
            ResultColumn.Texts(projection, "count")
        );
        Assert.Equal(
            ["1", "1", "1", "1", "1"],
            ResultColumn.Texts(projection, "smallest")
        );
    }

    [Fact]
    public void TotalSubqueryIsAOneRowSourceJoinedToEveryRow()
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
                                    "amount",
                                    new IntegerProjection(
                                        new FieldInteger("t.sales", "amount")
                                    )
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "sales",
                                    new IntegerProjection(
                                        new FieldInteger("totals", "sales")
                                    )
                                )
                            )
                        ),
                    ],
                    subqueries:
                    [
                        new Subquery(
                            "totals",
                            new Query(
                                new TotalQuery(
                                    new From(new FromEntity("t.sales")),
                                    [
                                        new SelectItemTotal(
                                            new SelectItemTotalNonNullable(
                                                new SelectItemTotalInteger(
                                                    "sales",
                                                    new IntegerTotal(
                                                        new AggregateIntegerTotal(
                                                            new CountTotal()
                                                        )
                                                    )
                                                )
                                            )
                                        ),
                                    ]
                                )
                            )
                        ),
                    ],
                    joins:
                    [
                        new Join(
                            new JoinSubquery(
                                JoinKind.Inner,
                                "totals",
                                new BooleanRow(new LiteralBoolean(true))
                            )
                        ),
                    ],
                    where: null,
                    orderBy: null,
                    pagination: null,
                    distinctBy: null
                )
            )
        );

        Assert.Equal(
            ["5", "2", "3", "9223372036854775807", "1"],
            ResultColumn.Texts(projection, "amount")
        );
        Assert.Equal(
            ["5", "5", "5", "5", "5"],
            ResultColumn.Texts(projection, "sales")
        );
    }

    [Fact]
    public void IntegerSumBeyondSixtyFourBitsFailsTheQuery()
    {
        PureQLProjection projection = new(
            [Sales],
            new PureQLQuery(
                new MainTotalQuery(
                    new From(new FromEntity("t.sales")),
                    [
                        new SelectItemTotal(
                            new SelectItemTotalNonNullable(
                                new SelectItemTotalInteger(
                                    "units",
                                    new IntegerTotal(
                                        new AggregateIntegerTotal(
                                            new SumIntegerTotal(
                                                new IntegerNullableRow(
                                                    new IntegerRow(
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
                                    "region",
                                    new ExactStringNullableRow(
                                        new FieldStringNullable("t.sales", "region")
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
                                    new ExactStringNullableGroup(
                                        new KeyStringNullable("region")
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
                                                                    new IntegerRow(
                                                                        new FieldInteger(
                                                                            "t.sales",
                                                                            "amount"
                                                                        )
                                                                    )
                                                                ),
                                                                new DecimalNullableRow(
                                                                    new IntegerRow(
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
                                                                    new ExactDecimalNullableRow(
                                                                        new FieldDecimalNullable(
                                                                            "t.sales",
                                                                            "price"
                                                                        )
                                                                    )
                                                                ),
                                                                new DecimalNullableRow(
                                                                    new ExactDecimalNullableRow(
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
                            new ExprGroup(
                                new StringNullableGroup(
                                    new ExactStringNullableGroup(
                                        new KeyStringNullable("region")
                                    )
                                )
                            )
                        ),
                    ],
                    pagination: null,
                    distinctBy: null
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
                                    "region",
                                    new ExactStringNullableRow(
                                        new FieldStringNullable("t.sales", "region")
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
                    distinctBy: null
                )
            )
        );

        Assert.Empty(projection.ToList());
    }
}
