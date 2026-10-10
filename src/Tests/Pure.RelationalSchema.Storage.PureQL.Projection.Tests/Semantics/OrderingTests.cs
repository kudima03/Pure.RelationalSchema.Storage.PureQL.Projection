using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.SelectItems;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Ordering and equality as the specification defines them for every type:
// strings by code point, uuids by their hexadecimal digits, false before
// true, null first ascending and last descending, and null equal to null.
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
                                    new StringNullableProjection(
                                        new FieldAsStringNullable(
                                            new FieldStringNullable("t.words", "word")
                                        )
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
                            new ValueProjection(
                                new StringNullableProjection(
                                    new FieldAsStringNullable(
                                        new FieldStringNullable("t.words", "word")
                                    )
                                )
                            )
                        ),
                    ],
                    pagination: null,
                    distinct: false
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
                            new ValueProjection(
                                new UuidNullableProjection(
                                    new FieldAsUuidNullable(new FieldUuid("t.words", "id"))
                                )
                            ),
                            SortDirection.Desc
                        ),
                    ],
                    pagination: null,
                    distinct: false
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
                                    new StringNullableProjection(
                                        new FieldAsStringNullable(
                                            new FieldStringNullable("t.words", "word")
                                        )
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
                            new ValueProjection(
                                new BooleanNullableProjection(
                                    new FieldAsBooleanNullable(
                                        new FieldBooleanNullable("t.words", "flag")
                                    )
                                )
                            ),
                            SortDirection.Desc
                        ),
                        new OrderItemProjection(
                            new ValueProjection(
                                new StringNullableProjection(
                                    new FieldAsStringNullable(
                                        new FieldStringNullable("t.words", "word")
                                    )
                                )
                            )
                        ),
                    ],
                    pagination: null,
                    distinct: false
                )
            )
        );

        Assert.Equal(
            ["B", "\U0001F600", "", "￿", "a", "ab"],
            ResultColumn.Texts(projection, "word")
        );
    }

    [Fact]
    public void DistinctTreatsNullsAsEqual()
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
                                    new BooleanNullableProjection(
                                        new FieldAsBooleanNullable(
                                            new FieldBooleanNullable("t.words", "flag")
                                        )
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
                            new ValueProjection(
                                new BooleanNullableProjection(
                                    new FieldAsBooleanNullable(
                                        new FieldBooleanNullable("t.words", "flag")
                                    )
                                )
                            )
                        ),
                    ],
                    pagination: null,
                    distinct: true
                )
            )
        );

        Assert.Equal(["", "False", "True"], ResultColumn.Texts(projection, "flag"));
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
                                                        new FieldAsStringNullable(
                                                            new FieldStringNullable(
                                                                "t.words",
                                                                "word"
                                                            )
                                                        )
                                                    ),
                                                    new StringNullableProjection(
                                                        new LiteralAsStringNullable(
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
                                                        new FieldAsBooleanNullable(
                                                            new FieldBooleanNullable(
                                                                "t.words",
                                                                "flag"
                                                            )
                                                        )
                                                    ),
                                                    new BooleanNullableProjection(
                                                        new LiteralAsBooleanNullable(
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
                                                        new FieldAsStringNullable(
                                                            new FieldStringNullable(
                                                                "t.words",
                                                                "word"
                                                            )
                                                        )
                                                    ),
                                                    new StringNullableRow(
                                                        new LiteralAsStringNullable(
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
                                                        new FieldAsStringNullable(
                                                            new FieldStringNullable(
                                                                "t.words",
                                                                "word"
                                                            )
                                                        )
                                                    ),
                                                    new StringNullableRow(
                                                        new LiteralAsStringNullable(
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
                    distinct: false
                )
            )
        );

        Assert.Equal(
            ["00000000-0000-0000-0000-000000000001", "00000000-0000-0000-0000-00000000000a"],
            ResultColumn.Texts(projection, "id")
        );
    }
}
