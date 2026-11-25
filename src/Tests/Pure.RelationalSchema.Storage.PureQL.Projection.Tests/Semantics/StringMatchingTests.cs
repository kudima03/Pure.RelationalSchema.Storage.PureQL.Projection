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

// contains, startsWith and endsWith test whether the code points of the
// pattern occur in the operand as one run: anywhere, at its start, at its
// end. They are conditions as they are, wherever a condition stands: false
// when either operand is null, also when both are; the pattern is literal
// text, so % and _ stand for themselves, and an empty one matches every
// non-null operand.
public sealed class StringMatchingTests
{
    private static readonly IStoredSchemaDataSet Products = new InlineTable(
        "t",
        "products",
        ("id", new LongColumnType()),
        ("kind", new StringColumnType()),
        ("name", new StringColumnType()),
        ("tag", new StringColumnType())
    ).With(
        ["1", "drink", "Green Tea", "Tea"],
        ["2", "ware", "tea pot", "tea"],
        ["3", "ware", "", "tea"],
        ["4", "drink", "Coffee", ""],
        ["5", "misc", "", ""],
        ["6", "misc", "100% cotton", "%"],
        ["7", "misc", "under_score", "_"]
    );

    private static readonly IStoredSchemaDataSet Words = new InlineTable(
        "u",
        "words",
        ("id", new LongColumnType()),
        ("word", new StringColumnType())
    ).With(["1", "Gre"], ["2", "tea"], ["3", ""], ["4", "co"], ["5", "pot"]);

    private static readonly InlineTable Texts = new(
        "t",
        "texts",
        ("text", new StringColumnType())
    );

    private static readonly FieldInteger Id = new("t.products", "id");

    private static readonly FieldString Kind = new("t.products", "kind");

    private static readonly FieldStringNullable Name = new("t.products", "name");

    private static readonly FieldStringNullable Tag = new("t.products", "tag");

    [Theory]
    [InlineData("contains", "ea", "1,2")]
    [InlineData("contains", "Tea", "1")]
    [InlineData("contains", "tea", "2")]
    [InlineData("contains", "o", "2,4,6,7")]
    [InlineData("contains", "green", "")]
    [InlineData("startsWith", "tea", "2")]
    [InlineData("startsWith", "Tea", "")]
    [InlineData("startsWith", "Green", "1")]
    [InlineData("startsWith", "reen", "")]
    [InlineData("endsWith", "Tea", "1")]
    [InlineData("endsWith", "pot", "2")]
    [InlineData("endsWith", "tea", "")]
    [InlineData("endsWith", "po", "")]
    public void AMatchInWhereKeepsTheRowsItHoldsFor(
        string match,
        string pattern,
        string expected
    )
    {
        Assert.Equal(expected, Where(Match(match, Name, new LiteralString(pattern))));
    }

    [Theory]
    [InlineData("contains", "%", "6")]
    [InlineData("contains", "100%", "6")]
    [InlineData("contains", "_", "7")]
    [InlineData("contains", "_e", "")]
    [InlineData("contains", "%e%", "")]
    [InlineData("contains", "T_a", "")]
    [InlineData("contains", "*", "")]
    [InlineData("contains", "\\", "")]
    [InlineData("startsWith", "%", "")]
    [InlineData("startsWith", "under_", "7")]
    [InlineData("startsWith", "Gr%", "")]
    [InlineData("startsWith", "_reen", "")]
    [InlineData("endsWith", "%", "")]
    [InlineData("endsWith", "_score", "7")]
    [InlineData("endsWith", "% cotton", "6")]
    [InlineData("endsWith", "%a", "")]
    public void PercentAndUnderscoreStandForThemselves(
        string match,
        string pattern,
        string expected
    )
    {
        Assert.Equal(expected, Where(Match(match, Name, new LiteralString(pattern))));
    }

    [Theory]
    [InlineData("contains")]
    [InlineData("startsWith")]
    [InlineData("endsWith")]
    public void AnEmptyPatternMatchesEveryNonNullOperand(string match)
    {
        Assert.Equal("1,2,4,6,7", Where(Match(match, Name, new LiteralString(""))));
    }

    [Theory]
    [InlineData("contains", "True,True,False,False,False,True,True")]
    [InlineData("startsWith", "False,True,False,False,False,False,False")]
    [InlineData("endsWith", "True,False,False,False,False,False,False")]
    public void AMatchAsAColumnIsFalseWhenEitherOperandIsNull(
        string match,
        string expected
    )
    {
        PureQLProjection projection = new(
            [Products],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.products"),
                select:
                [
                    new SelectItemProjection(
                        "matches",
                        match switch
                        {
                            "contains" => new ContainsProjection(Name, Tag),
                            "startsWith" => new StartsWithProjection(Name, Tag),
                            _ => new EndsWithProjection(Name, Tag),
                        }
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        Assert.Equal(expected, string.Join(",", ResultColumn.Texts(projection, "matches")));
    }

    [Theory]
    [InlineData("contains")]
    [InlineData("startsWith")]
    [InlineData("endsWith")]
    public void AMatchWithANullLiteralIsFalse(string match)
    {
        LiteralStringNullable missing = new();
        LiteralString text = new("tea");

        PureQLProjection projection = new(
            [Products],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.products"),
                where: new EqualDecimalRow(Id, new LiteralInteger(1)),
                select:
                [
                    new SelectItemProjection("operand", Column(match, missing, text)),
                    new SelectItemProjection("pattern", Column(match, text, missing)),
                    new SelectItemProjection("both", Column(match, missing, missing)),
                    new SelectItemProjection("field", Column(match, Name, missing)),
                    new SelectItemProjection("neither", Column(match, text, text)),
                ]
            )
        );

        Assert.Equal(["False"], ResultColumn.Texts(projection, "operand"));
        Assert.Equal(["False"], ResultColumn.Texts(projection, "pattern"));
        Assert.Equal(["False"], ResultColumn.Texts(projection, "both"));
        Assert.Equal(["False"], ResultColumn.Texts(projection, "field"));
        Assert.Equal(["True"], ResultColumn.Texts(projection, "neither"));
    }

    [Theory]
    [InlineData("abc", "b", true, false, false)]
    [InlineData("abc", "ab", true, true, false)]
    [InlineData("abc", "bc", true, false, true)]
    [InlineData("abc", "abc", true, true, true)]
    [InlineData("abc", "abcd", false, false, false)]
    [InlineData("abc", "ac", false, false, false)]
    [InlineData("abc", "B", false, false, false)]
    [InlineData("abc", "", true, true, true)]
    [InlineData("", "", true, true, true)]
    [InlineData("", "a", false, false, false)]
    [InlineData("été", "e", false, false, false)]
    [InlineData("été", "e", true, true, false)]
    [InlineData("é", "́", true, false, true)]
    [InlineData("abc", "­", false, false, false)]
    [InlineData("abc", "‍", false, false, false)]
    [InlineData("a\U0001F600", "\U0001F600", true, false, true)]
    [InlineData("\U0001F601a", "\U0001F600", false, false, false)]
    [InlineData("50%", "%", true, false, true)]
    [InlineData("a\\b", "\\", true, false, false)]
    [InlineData("a*b", "*", true, false, false)]
    [InlineData("abc", "a*", false, false, false)]
    [InlineData("abc", "a_c", false, false, false)]
    [InlineData("abc", "%", false, false, false)]
    [InlineData("abc", "a%", false, false, false)]
    [InlineData("straße", "ss", false, false, false)]
    public void AMatchComparesCodePointByCodePoint(
        string operand,
        string pattern,
        bool contains,
        bool startsWith,
        bool endsWith
    )
    {
        LiteralString text = new(pattern);
        string[] expected = [$"{contains}", $"{startsWith}", $"{endsWith}"];

        Assert.Equal(expected, Matches(operand, new FieldString("t.texts", "text"), text));
        Assert.Equal(expected, Matches(operand, new LiteralString(operand), text));
    }

    [Theory]
    [InlineData("contains", "1:1,2:2,2:5,6:4,7:4")]
    [InlineData("startsWith", "1:1,2:2")]
    [InlineData("endsWith", "2:5")]
    public void AMatchInAJoinPairsTheRowsItHoldsFor(string match, string expected)
    {
        PureQLProjection projection = new(
            [Products, Words],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.products"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Inner,
                        "u.words",
                        Match(match, Name, new FieldStringNullable("u.words", "word"))
                    ),
                ],
                select:
                [
                    new SelectItemProjection("product", Id),
                    new SelectItemProjection("word", new FieldInteger("u.words", "id")),
                ],
                orderBy:
                [
                    new OrderItemProjectionColumn("product"),
                    new OrderItemProjectionColumn("word"),
                ]
            )
        );

        Assert.Equal(
            expected,
            string.Join(
                ",",
                ResultColumn
                    .Texts(projection, "product")
                    .Zip(
                        ResultColumn.Texts(projection, "word"),
                        (product, word) => $"{product}:{word}"
                    )
            )
        );
    }

    [Fact]
    public void ANullPatternMatchesNothingInALeftJoin()
    {
        PureQLProjection projection = new(
            [Products, Words],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("u.words"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Left,
                        "t.products",
                        new StartsWithRow(
                            Name,
                            new FieldStringNullable("u.words", "word")
                        )
                    ),
                ],
                select:
                [
                    new SelectItemProjection("word", new FieldInteger("u.words", "id")),
                    new SelectItemProjection(
                        "product",
                        new FieldIntegerNullable("t.products", "id")
                    ),
                ],
                orderBy: [new OrderItemProjectionColumn("word")]
            )
        );

        Assert.Equal(["1", "2", "3", "4", "5"], ResultColumn.Texts(projection, "word"));
        Assert.Equal(["1", "2", "", "", ""], ResultColumn.Texts(projection, "product"));
    }

    [Fact]
    public void AMatchAsAPredicateCountsTheRowsItHoldsForInATotalQuery()
    {
        PureQLProjection projection = new(
            [Products],
            new MainTotalQuery(
                from: new FromEntity("t.products"),
                aggregate:
                [
                    new SelectItemTotal(
                        "contains",
                        new CountTotal(new ContainsRow(Name, Tag))
                    ),
                    new SelectItemTotal(
                        "startsWith",
                        new CountTotal(new StartsWithRow(Name, Tag))
                    ),
                    new SelectItemTotal(
                        "endsWith",
                        new CountTotal(new EndsWithRow(Name, Tag))
                    ),
                    new SelectItemTotal(
                        "empty",
                        new CountTotal(new ContainsRow(Name, new LiteralString("")))
                    ),
                    new SelectItemTotal(
                        "least",
                        new ContainsTotal(
                            new MinStringNullableTotal(Name),
                            new LiteralString("% c")
                        )
                    ),
                    new SelectItemTotal(
                        "none",
                        new ContainsTotal(
                            new MinStringNullableTotal(
                                Name,
                                new EqualStringRow(Kind, new LiteralString("none"))
                            ),
                            new LiteralString("")
                        )
                    ),
                ]
            )
        );

        Assert.Equal(["4"], ResultColumn.Texts(projection, "contains"));
        Assert.Equal(["1"], ResultColumn.Texts(projection, "startsWith"));
        Assert.Equal(["1"], ResultColumn.Texts(projection, "endsWith"));
        Assert.Equal(["5"], ResultColumn.Texts(projection, "empty"));
        Assert.Equal(["True"], ResultColumn.Texts(projection, "least"));
        Assert.Equal(["False"], ResultColumn.Texts(projection, "none"));
    }

    [Fact]
    public void AMatchAsAPredicateCountsTheRowsItHoldsForInEachGroup()
    {
        PureQLProjection projection = new(
            [Products],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.products"),
                groupBy: [new GroupKey("kind", Kind)],
                select:
                [
                    new SelectItemGroup("kind", new KeyString("kind")),
                    new SelectItemGroup(
                        "contains",
                        new CountGroup(new ContainsRow(Name, Tag))
                    ),
                    new SelectItemGroup(
                        "startsWith",
                        new CountGroup(new StartsWithRow(Name, Tag))
                    ),
                    new SelectItemGroup(
                        "endsWith",
                        new CountGroup(new EndsWithRow(Name, Tag))
                    ),
                ],
                orderBy: [new OrderItemGroupColumn("kind")]
            )
        );

        Assert.Equal(["drink", "misc", "ware"], ResultColumn.Texts(projection, "kind"));
        Assert.Equal(["1", "2", "1"], ResultColumn.Texts(projection, "contains"));
        Assert.Equal(["0", "0", "1"], ResultColumn.Texts(projection, "startsWith"));
        Assert.Equal(["1", "0", "0"], ResultColumn.Texts(projection, "endsWith"));
    }

    [Fact]
    public void HavingKeepsTheGroupsWithEnoughMatchingRows()
    {
        Assert.Equal(
            ["misc"],
            Having(
                new GreaterThanDecimalGroup(
                    new CountGroup(new ContainsRow(Name, Tag)),
                    new LiteralInteger(1)
                )
            )
        );
    }

    [Fact]
    public void HavingKeepsTheGroupsWithAnyMatchingRow()
    {
        Assert.Equal(
            ["ware"],
            Having(new AnyGroup(new StartsWithRow(Name, new LiteralString("tea"))))
        );
    }

    [Fact]
    public void HavingKeepsTheGroupsWhoseEveryRowMatchesAndANullNeverDoes()
    {
        Assert.Equal(
            ["drink"],
            Having(new AllGroup(new ContainsRow(Name, new LiteralString("e"))))
        );
    }

    [Fact]
    public void HavingMatchesAGroupKeyAndAnAggregate()
    {
        Assert.Equal(
            ["drink", "misc"],
            Having(new ContainsGroup(new KeyString("kind"), new LiteralString("i")))
        );
        Assert.Equal(
            ["misc"],
            Having(
                new EndsWithGroup(
                    new MaxStringNullableGroup(Name),
                    new LiteralString("_score")
                )
            )
        );
    }

    private static BooleanRow Match(
        string match,
        StringNullableRow operand,
        StringNullableRow pattern
    )
    {
        return match switch
        {
            "contains" => new ContainsRow(operand, pattern),
            "startsWith" => new StartsWithRow(operand, pattern),
            _ => new EndsWithRow(operand, pattern),
        };
    }

    private static ExprProjection Column(
        string match,
        StringNullableProjection operand,
        StringNullableProjection pattern
    )
    {
        return match switch
        {
            "contains" => new ContainsProjection(operand, pattern),
            "startsWith" => new StartsWithProjection(operand, pattern),
            _ => new EndsWithProjection(operand, pattern),
        };
    }

    // The ids of the products a condition holds for, in order.
    private static string Where(BooleanRow condition)
    {
        PureQLProjection projection = new(
            [Products],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.products"),
                where: condition,
                select: [new SelectItemProjection("id", Id)],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        return string.Join(",", ResultColumn.Texts(projection, "id"));
    }

    // The kinds of product a condition on the group holds for, in order.
    private static IReadOnlyList<string> Having(BooleanGroup condition)
    {
        PureQLProjection projection = new(
            [Products],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.products"),
                groupBy: [new GroupKey("kind", Kind)],
                select: [new SelectItemGroup("kind", new KeyString("kind"))],
                having: condition,
                orderBy: [new OrderItemGroupColumn("kind")]
            )
        );

        return ResultColumn.Texts(projection, "kind");
    }

    // contains, startsWith and endsWith of an operand and a pattern, over
    // one stored row that holds the text.
    private static string[] Matches(
        string text,
        StringNullableProjection operand,
        StringNullableProjection pattern
    )
    {
        PureQLProjection projection = new(
            [Texts.With([text])],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.texts"),
                select:
                [
                    new SelectItemProjection(
                        "contains",
                        new ContainsProjection(operand, pattern)
                    ),
                    new SelectItemProjection(
                        "startsWith",
                        new StartsWithProjection(operand, pattern)
                    ),
                    new SelectItemProjection(
                        "endsWith",
                        new EndsWithProjection(operand, pattern)
                    ),
                ]
            )
        );

        return
        [
            Assert.Single(ResultColumn.Texts(projection, "contains")),
            Assert.Single(ResultColumn.Texts(projection, "startsWith")),
            Assert.Single(ResultColumn.Texts(projection, "endsWith")),
        ];
    }
}
