using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.GroupExpressions;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
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
            new MainTotalQuery(
                from: new FromEntity("t.sales"),
                where: new LiteralBoolean(false),
                aggregate:
                [
                    new SelectItemTotal("count", new CountTotal()),
                    new SelectItemTotal(
                        "units",
                        new SumIntegerTotal(new FieldInteger("t.sales", "amount"))
                    ),
                    new SelectItemTotal(
                        "revenue",
                        new SumDecimalTotal(new FieldDecimalNullable("t.sales", "price"))
                    ),
                    new SelectItemTotal(
                        "first",
                        new MinStringNullableTotal(
                            new FieldStringNullable("t.sales", "region")
                        )
                    ),
                    new SelectItemTotal(
                        "mean",
                        new AverageDecimalNullableTotal(
                            new FieldDecimalNullable("t.sales", "price")
                        )
                    ),
                    new SelectItemTotal("any", new AnyTotal(new LiteralBoolean(true))),
                    new SelectItemTotal("all", new AllTotal(new LiteralBoolean(false))),
                ]
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
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.sales"),
                select:
                [
                    new SelectItemProjection("count", new CountProjection()),
                    new SelectItemProjection(
                        "smallest",
                        new MinIntegerProjection(new FieldInteger("t.sales", "amount"))
                    ),
                ]
            )
        );

        Assert.Equal(["5", "5", "5", "5", "5"], ResultColumn.Texts(projection, "count"));
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
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "totals",
                        new TotalQuery(
                            from: new FromEntity("t.sales"),
                            aggregate: [new SelectItemTotal("sales", new CountTotal())]
                        )
                    ),
                ],
                from: new FromEntity("t.sales"),
                joins:
                [
                    new JoinSubquery(JoinKind.Inner, "totals", new LiteralBoolean(true)),
                ],
                select:
                [
                    new SelectItemProjection(
                        "amount",
                        new FieldInteger("t.sales", "amount")
                    ),
                    new SelectItemProjection(
                        "sales",
                        new FieldInteger("totals", "sales")
                    ),
                ]
            )
        );

        Assert.Equal(
            ["5", "2", "3", "9223372036854775807", "1"],
            ResultColumn.Texts(projection, "amount")
        );
        Assert.Equal(["5", "5", "5", "5", "5"], ResultColumn.Texts(projection, "sales"));
    }

    [Fact]
    public void IntegerSumBeyondSixtyFourBitsFailsTheQuery()
    {
        PureQLProjection projection = new(
            [Sales],
            new MainTotalQuery(
                from: new FromEntity("t.sales"),
                aggregate:
                [
                    new SelectItemTotal(
                        "units",
                        new SumIntegerTotal(new FieldInteger("t.sales", "amount"))
                    ),
                ]
            )
        );

        _ = Assert.Throws<OverflowException>(() => projection.ToList());
    }

    [Fact]
    public void GroupsPutNullKeysTogetherAndAggregateOverTheirOwnRowsOrAllRows()
    {
        PureQLProjection projection = new(
            [Sales],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.sales"),
                groupBy:
                [
                    new GroupKey("region", new FieldStringNullable("t.sales", "region")),
                ],
                select:
                [
                    new SelectItemGroup("region", new KeyStringNullable("region")),
                    new SelectItemGroup(
                        "smallest",
                        new MinIntegerGroup(new FieldInteger("t.sales", "amount"))
                    ),
                    new SelectItemGroup(
                        "largest_overall",
                        new CountGroup(
                            new LessThanDecimalRow(
                                new FieldInteger("t.sales", "amount"),
                                new LiteralInteger(4)
                            ),
                            AggregateOver.All
                        )
                    ),
                    new SelectItemGroup(
                        "all_priced",
                        new AllGroup(
                            new NotEqualDecimalRow(
                                new FieldDecimalNullable("t.sales", "price"),
                                new LiteralDecimalNullable()
                            )
                        )
                    ),
                ],
                orderBy: [new OrderItemGroupExpression(new KeyStringNullable("region"))]
            )
        );

        Assert.Equal(["", "north", "south"], ResultColumn.Texts(projection, "region"));
        Assert.Equal(
            ["1", "3", "9223372036854775807"],
            ResultColumn.Texts(projection, "smallest")
        );
        Assert.Equal(["3", "3", "3"], ResultColumn.Texts(projection, "largest_overall"));
        Assert.Equal(
            ["True", "False", "True"],
            ResultColumn.Texts(projection, "all_priced")
        );
    }

    [Fact]
    public void GroupedQueryOverNoRowsReturnsNoRows()
    {
        PureQLProjection projection = new(
            [Sales],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.sales"),
                where: new LiteralBoolean(false),
                groupBy:
                [
                    new GroupKey("region", new FieldStringNullable("t.sales", "region")),
                ],
                select: [new SelectItemGroup("count", new CountGroup())]
            )
        );

        Assert.Empty(projection.ToList());
    }
}
