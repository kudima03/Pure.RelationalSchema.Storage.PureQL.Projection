using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
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
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.words"),
                select:
                [
                    new SelectItemProjection(
                        "word",
                        new FieldStringNullable("t.words", "word")
                    ),
                ],
                orderBy:
                [
                    new OrderItemProjectionExpression(
                        new FieldStringNullable("t.words", "word")
                    ),
                ]
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
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.words"),
                select: [new SelectItemProjection("id", new FieldUuid("t.words", "id"))],
                orderBy:
                [
                    new OrderItemProjectionExpression(
                        new FieldUuid("t.words", "id"),
                        SortDirection.Desc
                    ),
                ]
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
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.words"),
                select:
                [
                    new SelectItemProjection(
                        "word",
                        new FieldStringNullable("t.words", "word")
                    ),
                ],
                orderBy:
                [
                    new OrderItemProjectionExpression(
                        new FieldBooleanNullable("t.words", "flag"),
                        SortDirection.Desc
                    ),
                    new OrderItemProjectionExpression(
                        new FieldStringNullable("t.words", "word")
                    ),
                ]
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
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.words"),
                select:
                [
                    new SelectItemProjection(
                        "flag",
                        new FieldBooleanNullable("t.words", "flag")
                    ),
                ],
                distinctBy: ["flag"],
                orderBy:
                [
                    new OrderItemProjectionExpression(
                        new FieldBooleanNullable("t.words", "flag")
                    ),
                ]
            )
        );

        Assert.Equal(["", "False", "True"], ResultColumn.Texts(projection, "flag"));
    }

    [Fact]
    public void DistinctByKeepsTheFirstRowInOrderBeforeThePageIsTaken()
    {
        PureQLProjection projection = new(
            [Words],
            new MainPlainQueryWithPagination(
                from: new FromEntity("t.words"),
                select:
                [
                    new SelectItemProjection(
                        "word",
                        new FieldStringNullable("t.words", "word")
                    ),
                    new SelectItemProjection(
                        "flag",
                        new FieldBooleanNullable("t.words", "flag")
                    ),
                ],
                distinctBy: ["flag"],
                orderBy:
                [
                    new OrderItemProjectionExpression(
                        new FieldStringNullable("t.words", "word")
                    ),
                ],
                pagination: new Pagination(skip: 1, take: 2)
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
            new MainTotalQuery(
                from: new FromEntity("t.words"),
                aggregate:
                [
                    new SelectItemTotal(
                        "last",
                        new MaxUuidNullableTotal(new FieldUuid("t.words", "id"))
                    ),
                    new SelectItemTotal(
                        "least",
                        new MinBooleanNullableTotal(
                            new FieldBooleanNullable("t.words", "flag")
                        )
                    ),
                    new SelectItemTotal(
                        "above",
                        new CountTotal(
                            new GreaterThanUuidRow(
                                new FieldUuid("t.words", "id"),
                                new LiteralUuid(
                                    Guid.Parse("00000000-0000-0000-0000-000000000003")
                                )
                            )
                        )
                    ),
                    new SelectItemTotal(
                        "truths",
                        new CountTotal(
                            new GreaterThanBooleanRow(
                                new FieldBooleanNullable("t.words", "flag"),
                                new LiteralBoolean(false)
                            )
                        )
                    ),
                ]
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
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.words"),
                select:
                [
                    new SelectItemProjection(
                        "below",
                        new LessThanStringProjection(
                            new FieldStringNullable("t.words", "word"),
                            new LiteralString("b")
                        )
                    ),
                    new SelectItemProjection(
                        "missing",
                        new EqualBooleanProjection(
                            new FieldBooleanNullable("t.words", "flag"),
                            new LiteralBooleanNullable()
                        )
                    ),
                ]
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
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.words"),
                where: new AndRow([
                    new GreaterThanOrEqualStringRow(
                        new FieldStringNullable("t.words", "word"),
                        new LiteralString("a")
                    ),
                    new LessThanOrEqualStringRow(
                        new FieldStringNullable("t.words", "word"),
                        new LiteralString("ab")
                    ),
                ]),
                select: [new SelectItemProjection("id", new FieldUuid("t.words", "id"))]
            )
        );

        Assert.Equal(
            [
                "00000000-0000-0000-0000-000000000001",
                "00000000-0000-0000-0000-00000000000a",
            ],
            ResultColumn.Texts(projection, "id")
        );
    }
}
