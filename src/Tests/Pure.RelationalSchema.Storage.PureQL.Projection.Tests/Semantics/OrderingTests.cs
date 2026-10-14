using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.SelectItems;
using PureQL.CSharp.Model.TotalExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Ordering and equality as the specification defines them for every type:
// strings by code point, uuids by their hexadecimal digits, false before
// true, null first ascending and last descending, and null equal to null.
// distinctBy keeps the first row in orderBy order, before pagination.
public sealed class OrderingTests
{
    private static readonly IStoredSchemaDataSet Words = new InlineTable(
        "t",
        "words",
        ("word", new StringColumnType()),
        ("id", new UuidColumnType()),
        ("flag", new BoolColumnType())
    ).With(
        ["\U0001F600", "00000000-0000-0000-0000-000000000003", "True"],
        ["ab", "00000000-0000-0000-0000-000000000001", ""],
        ["￿", "10000000-0000-0000-0000-000000000000", "False"],
        ["B", "00000000-0000-0000-0000-000000000002", "True"],
        ["", "0000000a-0000-0000-0000-000000000000", "False"],
        ["a", "00000000-0000-0000-0000-00000000000a", ""]
    );

    [Fact]
    public void StringsSortByCodePointWithNullFirst()
    {
        PureQLProjection projection = new(
            [Words],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.words")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionStringNullable(
                                    "word",
                                    new ExactStringNullableProjection(
                                        new FieldStringNullable("t.words", "word")
                                    )
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins: null,
                    where: null,
                    orderBy:
                    [
                        new OrderItemProjection(
                            new ExprProjection(
                                new StringNullableProjection(
                                    new ExactStringNullableProjection(
                                        new FieldStringNullable("t.words", "word")
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

        Assert.Equal(
            ["", "B", "a", "ab", "￿", "\U0001F600"],
            ResultColumn.Texts(projection, "word")
        );
    }

    [Fact]
    public void UuidsSortByTheirHexadecimalDigitsDescending()
    {
        PureQLProjection projection = new(
            [Words],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.words")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionUuid(
                                    "id",
                                    new UuidProjection(new FieldUuid("t.words", "id"))
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins: null,
                    where: null,
                    orderBy:
                    [
                        new OrderItemProjection(
                            new ExprProjection(
                                new UuidNullableProjection(
                                    new UuidProjection(new FieldUuid("t.words", "id"))
                                )
                            ),
                            SortDirection.Desc
                        ),
                    ],
                    pagination: null,
                    distinctBy: null
                )
            )
        );

        Assert.Equal(
            [
                "10000000-0000-0000-0000-000000000000",
                "0000000a-0000-0000-0000-000000000000",
                "00000000-0000-0000-0000-00000000000a",
                "00000000-0000-0000-0000-000000000003",
                "00000000-0000-0000-0000-000000000002",
                "00000000-0000-0000-0000-000000000001",
            ],
            ResultColumn.Texts(projection, "id")
        );
    }

    [Fact]
    public void BooleansSortFalseBeforeTrueWithNullLastDescendingAndKeysBreakTies()
    {
        PureQLProjection projection = new(
            [Words],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.words")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionStringNullable(
                                    "word",
                                    new ExactStringNullableProjection(
                                        new FieldStringNullable("t.words", "word")
                                    )
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins: null,
                    where: null,
                    orderBy:
                    [
                        new OrderItemProjection(
                            new ExprProjection(
                                new BooleanNullableProjection(
                                    new ExactBooleanNullableProjection(
                                        new FieldBooleanNullable("t.words", "flag")
                                    )
                                )
                            ),
                            SortDirection.Desc
                        ),
                        new OrderItemProjection(
                            new ExprProjection(
                                new StringNullableProjection(
                                    new ExactStringNullableProjection(
                                        new FieldStringNullable("t.words", "word")
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

        Assert.Equal(
            ["B", "\U0001F600", "", "￿", "a", "ab"],
            ResultColumn.Texts(projection, "word")
        );
    }

    [Fact]
    public void DistinctByEveryColumnTreatsNullsAsEqual()
    {
        PureQLProjection projection = new(
            [Words],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.words")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionBooleanNullable(
                                    "flag",
                                    new ExactBooleanNullableProjection(
                                        new FieldBooleanNullable("t.words", "flag")
                                    )
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins: null,
                    where: null,
                    orderBy:
                    [
                        new OrderItemProjection(
                            new ExprProjection(
                                new BooleanNullableProjection(
                                    new ExactBooleanNullableProjection(
                                        new FieldBooleanNullable("t.words", "flag")
                                    )
                                )
                            )
                        ),
                    ],
                    pagination: null,
                    distinctBy: ["flag"]
                )
            )
        );

        Assert.Equal(["", "False", "True"], ResultColumn.Texts(projection, "flag"));
    }

    [Fact]
    public void DistinctByKeepsTheFirstRowInOrderBeforeThePageIsTaken()
    {
        PureQLProjection projection = new(
            [Words],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.words")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionStringNullable(
                                    "word",
                                    new ExactStringNullableProjection(
                                        new FieldStringNullable("t.words", "word")
                                    )
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionBooleanNullable(
                                    "flag",
                                    new ExactBooleanNullableProjection(
                                        new FieldBooleanNullable("t.words", "flag")
                                    )
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins: null,
                    where: null,
                    orderBy:
                    [
                        new OrderItemProjection(
                            new ExprProjection(
                                new StringNullableProjection(
                                    new ExactStringNullableProjection(
                                        new FieldStringNullable("t.words", "word")
                                    )
                                )
                            )
                        ),
                    ],
                    pagination: new Pagination(1, 2),
                    distinctBy: ["flag"]
                )
            )
        );

        Assert.Equal(["B", "a"], ResultColumn.Texts(projection, "word"));
        Assert.Equal(["True", ""], ResultColumn.Texts(projection, "flag"));
    }

    [Fact]
    public void UuidsAndBooleansCompareAndAggregateInTheirOrder()
    {
        PureQLProjection projection = new(
            [Words],
            new PureQLQuery(
                new MainTotalQuery(
                    new From(new FromEntity("t.words")),
                    [
                        new SelectItemTotal(
                            new SelectItemTotalNullable(
                                new SelectItemTotalUuidNullable(
                                    "last",
                                    new ExactUuidNullableTotal(
                                        new AggregateUuidNullableTotal(
                                            new MaxUuidNullableTotal(
                                                new UuidNullableRow(
                                                    new UuidRow(
                                                        new FieldUuid("t.words", "id")
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
                                new SelectItemTotalBooleanNullable(
                                    "least",
                                    new ExactBooleanNullableTotal(
                                        new AggregateBooleanNullableTotal(
                                            new MinBooleanNullableTotal(
                                                new BooleanNullableRow(
                                                    new ExactBooleanNullableRow(
                                                        new FieldBooleanNullable(
                                                            "t.words",
                                                            "flag"
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
                                new SelectItemTotalInteger(
                                    "above",
                                    new IntegerTotal(
                                        new AggregateIntegerTotal(
                                            new CountTotal(
                                                new BooleanRow(
                                                    new ComparisonRow(
                                                        new GreaterThanRow(
                                                            new GreaterThanUuidRow(
                                                                new UuidNullableRow(
                                                                    new UuidRow(
                                                                        new FieldUuid(
                                                                            "t.words",
                                                                            "id"
                                                                        )
                                                                    )
                                                                ),
                                                                new UuidNullableRow(
                                                                    new UuidRow(
                                                                        new LiteralUuid(
                                                                            Guid.Parse(
                                                                                "00000000-0000-0000-0000-000000000003"
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
                                )
                            )
                        ),
                        new SelectItemTotal(
                            new SelectItemTotalNonNullable(
                                new SelectItemTotalInteger(
                                    "truths",
                                    new IntegerTotal(
                                        new AggregateIntegerTotal(
                                            new CountTotal(
                                                new BooleanRow(
                                                    new ComparisonRow(
                                                        new GreaterThanRow(
                                                            new GreaterThanBooleanRow(
                                                                new BooleanNullableRow(
                                                                    new ExactBooleanNullableRow(
                                                                        new FieldBooleanNullable(
                                                                            "t.words",
                                                                            "flag"
                                                                        )
                                                                    )
                                                                ),
                                                                new BooleanNullableRow(
                                                                    new BooleanRow(
                                                                        new LiteralBoolean(false)
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
                    ]
                )
            )
        );

        Assert.Equal(
            ["10000000-0000-0000-0000-000000000000"],
            ResultColumn.Texts(projection, "last")
        );
        Assert.Equal(["False"], ResultColumn.Texts(projection, "least"));
        Assert.Equal(["3"], ResultColumn.Texts(projection, "above"));
        Assert.Equal(["2"], ResultColumn.Texts(projection, "truths"));
    }

    [Fact]
    public void OrderingComparisonAgainstNullIsFalseAndNullEqualsNull()
    {
        PureQLProjection projection = new(
            [Words],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.words")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionBoolean(
                                    "below",
                                    new BooleanProjection(
                                        new ComparisonProjection(
                                            new LessThanProjection(
                                                new LessThanStringProjection(
                                                    new StringNullableProjection(
                                                        new ExactStringNullableProjection(
                                                            new FieldStringNullable(
                                                                "t.words",
                                                                "word"
                                                            )
                                                        )
                                                    ),
                                                    new StringNullableProjection(
                                                        new StringProjection(
                                                            new LiteralString("b")
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
                                    "missing",
                                    new BooleanProjection(
                                        new ComparisonProjection(
                                            new EqualProjection(
                                                new EqualBooleanProjection(
                                                    new BooleanNullableProjection(
                                                        new ExactBooleanNullableProjection(
                                                            new FieldBooleanNullable(
                                                                "t.words",
                                                                "flag"
                                                            )
                                                        )
                                                    ),
                                                    new BooleanNullableProjection(
                                                        new ExactBooleanNullableProjection(
                                                            new LiteralBooleanNullable()
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

        Assert.Equal(
            ["False", "True", "False", "True", "False", "True"],
            ResultColumn.Texts(projection, "below")
        );
        Assert.Equal(
            ["False", "True", "False", "False", "False", "True"],
            ResultColumn.Texts(projection, "missing")
        );
    }

    [Fact]
    public void GreaterThanOrEqualAndLessThanOrEqualIncludeEqualValues()
    {
        PureQLProjection projection = new(
            [Words],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.words")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionUuid(
                                    "id",
                                    new UuidProjection(new FieldUuid("t.words", "id"))
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins: null,
                    where: new BooleanRow(
                        new LogicalRow(
                            new AndRow(
                                [
                                    new BooleanRow(
                                        new ComparisonRow(
                                            new GreaterThanOrEqualRow(
                                                new GreaterThanOrEqualStringRow(
                                                    new StringNullableRow(
                                                        new ExactStringNullableRow(
                                                            new FieldStringNullable(
                                                                "t.words",
                                                                "word"
                                                            )
                                                        )
                                                    ),
                                                    new StringNullableRow(
                                                        new StringRow(
                                                            new LiteralString("a")
                                                        )
                                                    )
                                                )
                                            )
                                        )
                                    ),
                                    new BooleanRow(
                                        new ComparisonRow(
                                            new LessThanOrEqualRow(
                                                new LessThanOrEqualStringRow(
                                                    new StringNullableRow(
                                                        new ExactStringNullableRow(
                                                            new FieldStringNullable(
                                                                "t.words",
                                                                "word"
                                                            )
                                                        )
                                                    ),
                                                    new StringNullableRow(
                                                        new StringRow(
                                                            new LiteralString("ab")
                                                        )
                                                    )
                                                )
                                            )
                                        )
                                    ),
                                ]
                            )
                        )
                    ),
                    orderBy: null,
                    pagination: null,
                    distinctBy: null
                )
            )
        );

        Assert.Equal(
            ["00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-00000000000a"],
            ResultColumn.Texts(projection, "id")
        );
    }
}
