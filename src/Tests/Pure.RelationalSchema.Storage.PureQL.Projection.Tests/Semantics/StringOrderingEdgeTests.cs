using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Parameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.GroupExpressions;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.TotalExpressions;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Strings are ordered "lexicographic by code point" (Ordering and
// equality), and "the same order serves comparisons, min / max, greatest /
// least and orderBy". UTF-16 ordinal order differs from it exactly between
// the code points U+E000-U+FFFF and those outside the Basic Multilingual
// Plane, whose surrogates (U+D800-U+DFFF) sort below U+E000 as code units:
// so every string here is one of U+E000, U+FFFD, U+FFFF, U+10000 and
// U+1F600, in that order, and each place a string is ordered is checked:
// orderBy, pagination, min / max over a group, over all rows and over a
// window, greatest / least, comparisons with literals and parameters,
// first / last, concatAll, ranking, mode and percentileDiscrete (both "the
// least by the order of the type" and "in order"), distinctBy, having and a
// subquery's pagination read by in. Equality, by contrast, compares code
// points without normalizing: groupBy, countDistinct and set operations.
public sealed class StringOrderingEdgeTests
{
    private const string Private = "\xE000";

    private const string Replacement = "\xFFFD";

    private const string Last = "\xFFFF";

    private const string Linear = "\U00010000";

    private const string Grin = "\U0001F600";

    // Row 6 has no word; rows 1 and 7 share one.
    private static readonly IStoredSchemaDataSet Words = new InlineTable(
        "t",
        "words",
        ("id", new LongColumnType()),
        ("grp", new StringColumnType()),
        ("word", new StringColumnType())
    ).With(
        ["1", "x", Last],
        ["2", "x", Linear],
        ["3", "y", Private],
        ["4", "y", Grin],
        ["5", "y", Replacement],
        ["6", "z", ""],
        ["7", "z", Last]
    );

    // The same letter in different encodings and cases.
    private static readonly IStoredSchemaDataSet Forms = new InlineTable(
        "t",
        "forms",
        ("id", new LongColumnType()),
        ("form", new StringColumnType())
    ).With(
        ["1", "\x00E9"],
        ["2", "e\x0301"],
        ["3", "E\x0301"],
        ["4", "\x00C9"],
        ["5", "\x00E9"]
    );

    private static readonly FieldInteger Id = new("t.words", "id");

    private static readonly FieldString Grp = new("t.words", "grp");

    private static readonly FieldStringNullable Word = new("t.words", "word");

    [Fact]
    public void OrderByAscendingIsByCodePointWithNullFirst()
    {
        Assert.Equal(
            ["6", "3", "5", "1", "7", "2", "4"],
            Ids(
                orderBy:
                [
                    new OrderItemProjectionExpression(Word),
                    new OrderItemProjectionExpression(Id),
                ]
            )
        );
    }

    [Fact]
    public void OrderByDescendingIsByCodePointWithNullLast()
    {
        Assert.Equal(
            ["4", "2", "1", "7", "5", "3", "6"],
            Ids(
                orderBy:
                [
                    new OrderItemProjectionExpression(Word, SortDirection.Desc),
                    new OrderItemProjectionExpression(Id),
                ]
            )
        );
    }

    [Fact]
    public void OrderByAComputedStringIsByCodePoint()
    {
        Assert.Equal(
            ["6", "3", "5", "1", "7", "2", "4"],
            Ids(
                orderBy:
                [
                    new OrderItemProjectionExpression(
                        new ConcatStringNullableProjection([
                            new LiteralString("a"),
                            Word,
                        ])
                    ),
                    new OrderItemProjectionExpression(Id),
                ]
            )
        );
    }

    [Fact]
    public void OrderByAColumnAliasIsByCodePoint()
    {
        PureQLProjection projection = new(
            [Words],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.words"),
                where: new NotEqualStringRow(Word, new LiteralStringNullable()),
                select: [new SelectItemProjection("w", Word)],
                orderBy: [new OrderItemProjectionColumn("w", SortDirection.Desc)]
            )
        );

        Assert.Equal(
            [Grin, Linear, Last, Last, Replacement, Private],
            ResultColumn.Texts(projection, "w")
        );
    }

    [Fact]
    public void APageIsTakenInCodePointOrder()
    {
        PureQLProjection projection = new(
            [Words],
            new MainPlainQueryWithPagination(
                from: new FromEntity("t.words"),
                pagination: new Pagination(skip: 1, take: 2),
                select: [new SelectItemProjection("id", Id)],
                orderBy:
                [
                    new OrderItemProjectionExpression(Word),
                    new OrderItemProjectionExpression(Id),
                ]
            )
        );

        Assert.Equal(["3", "5"], ResultColumn.Texts(projection, "id"));
    }

    [Fact]
    public void MinAndMaxOfATotalQueryAreByCodePoint()
    {
        PureQLProjection projection = new(
            [Words],
            new MainTotalQuery(
                from: new FromEntity("t.words"),
                aggregate:
                [
                    new SelectItemTotal("min", new MinStringNullableTotal(Word)),
                    new SelectItemTotal("max", new MaxStringNullableTotal(Word)),
                    new SelectItemTotal(
                        "minX",
                        new MinStringNullableTotal(
                            Word,
                            new EqualStringRow(Grp, new LiteralString("x"))
                        )
                    ),
                    new SelectItemTotal(
                        "maxX",
                        new MaxStringNullableTotal(
                            Word,
                            new EqualStringRow(Grp, new LiteralString("x"))
                        )
                    ),
                ]
            )
        );

        Assert.Equal([Private], ResultColumn.Texts(projection, "min"));
        Assert.Equal([Grin], ResultColumn.Texts(projection, "max"));
        Assert.Equal([Last], ResultColumn.Texts(projection, "minX"));
        Assert.Equal([Linear], ResultColumn.Texts(projection, "maxX"));
    }

    [Fact]
    public void MinAndMaxOfEachGroupAndOfAllRowsAreByCodePoint()
    {
        PureQLProjection projection = Grouped(
            new SelectItemGroup("min", new MinStringNullableGroup(Word)),
            new SelectItemGroup("max", new MaxStringNullableGroup(Word)),
            new SelectItemGroup(
                "maxAll",
                new MaxStringNullableGroup(Word, over: AggregateOver.All)
            ),
            new SelectItemGroup(
                "minAll",
                new MinStringNullableGroup(Word, over: AggregateOver.All)
            )
        );

        Assert.Equal(["x", "y", "z"], ResultColumn.Texts(projection, "grp"));
        Assert.Equal([Last, Private, Last], ResultColumn.Texts(projection, "min"));
        Assert.Equal([Linear, Grin, Last], ResultColumn.Texts(projection, "max"));
        Assert.Equal([Grin, Grin, Grin], ResultColumn.Texts(projection, "maxAll"));
        Assert.Equal(
            [Private, Private, Private],
            ResultColumn.Texts(projection, "minAll")
        );
    }

    [Fact]
    public void MinAndMaxOverAWindowAreByCodePoint()
    {
        PureQLProjection projection = new(
            [Words],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.words"),
                select:
                [
                    new SelectItemProjection(
                        "max",
                        new MaxStringNullableProjection(
                            Word,
                            over: new WindowWithoutFrameWithoutRange([Grp])
                        )
                    ),
                    new SelectItemProjection(
                        "min",
                        new MinStringNullableProjection(
                            Word,
                            over: new WindowWithoutFrameWithoutRange([Grp])
                        )
                    ),
                    new SelectItemProjection(
                        "maxAll",
                        new MaxStringNullableProjection(Word)
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        Assert.Equal(
            [Linear, Linear, Grin, Grin, Grin, Last, Last],
            ResultColumn.Texts(projection, "max")
        );
        Assert.Equal(
            [Last, Last, Private, Private, Private, Last, Last],
            ResultColumn.Texts(projection, "min")
        );
        Assert.Equal(
            [.. Enumerable.Repeat(Grin, 7)],
            ResultColumn.Texts(projection, "maxAll")
        );
    }

    [Fact]
    public void GreatestAndLeastOfARowAreByCodePoint()
    {
        PureQLProjection projection = new(
            [Words],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.words"),
                select:
                [
                    new SelectItemProjection(
                        "greatest",
                        new CoalesceStringProjection([
                            new GreatestStringNullableProjection([
                                Word,
                                new LiteralString(Last),
                            ]),
                            new LiteralString("NULL"),
                        ])
                    ),
                    new SelectItemProjection(
                        "least",
                        new CoalesceStringProjection([
                            new LeastStringNullableProjection([
                                Word,
                                new LiteralString(Linear),
                            ]),
                            new LiteralString("NULL"),
                        ])
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        Assert.Equal(
            [Last, Linear, Last, Grin, Last, "NULL", Last],
            ResultColumn.Texts(projection, "greatest")
        );
        Assert.Equal(
            [Last, Linear, Private, Linear, Replacement, "NULL", Last],
            ResultColumn.Texts(projection, "least")
        );
    }

    // Over literals only, the simplifier may compute the value when the
    // projection is built: it is still the one of the code point order.
    [Fact]
    public void GreatestAndLeastOfLiteralsAreByCodePoint()
    {
        Assert.Equal(
            [Linear, Private, Last, Replacement],
            Constants(
                new GreatestStringProjection([
                    new LiteralString(Last),
                    new LiteralString(Linear),
                    new LiteralString(Private),
                ]),
                new LeastStringProjection([
                    new LiteralString(Last),
                    new LiteralString(Linear),
                    new LiteralString(Private),
                ]),
                new GreatestStringProjection([
                    new LiteralString(Last),
                    new LiteralString(Private),
                ]),
                new LeastStringProjection([
                    new LiteralString(Grin),
                    new LiteralString(Replacement),
                ])
            )
        );
    }

    [Fact]
    public void ComparisonsOfLiteralsAreByCodePoint()
    {
        Assert.Equal(
            ["True", "False", "True", "False", "True", "True", "False"],
            Constants(
                new LessThanStringProjection(
                    new LiteralString(Last),
                    new LiteralString(Linear)
                ),
                new GreaterThanStringProjection(
                    new LiteralString(Last),
                    new LiteralString(Linear)
                ),
                new GreaterThanStringProjection(
                    new LiteralString(Grin),
                    new LiteralString(Private)
                ),
                new LessThanStringProjection(
                    new LiteralString("a" + Grin),
                    new LiteralString("a" + Last)
                ),
                new LessThanStringProjection(
                    new LiteralString(Last),
                    new LiteralString(Last + Private)
                ),
                new GreaterThanOrEqualStringProjection(
                    new LiteralString(Linear),
                    new LiteralString(Replacement + Grin)
                ),
                new LessThanOrEqualStringProjection(
                    new LiteralString(Linear),
                    new LiteralString(Last + Grin)
                )
            )
        );
    }

    [Theory]
    [InlineData("greaterThan", "\xFFFF", "2,4")]
    [InlineData("greaterThanOrEqual", "\xFFFF", "1,2,4,7")]
    [InlineData("lessThan", "\U00010000", "1,3,5,7")]
    [InlineData("lessThanOrEqual", "\xFFFD", "3,5")]
    [InlineData("lessThan", "\U0001F600", "1,2,3,5,7")]
    [InlineData("greaterThan", "\xE000", "1,2,4,5,7")]
    public void AComparisonInWhereIsByCodePoint(
        string comparison,
        string bound,
        string expected
    )
    {
        Assert.Equal(
            expected,
            string.Join(",", Where(Compared(comparison, Word, new LiteralString(bound))))
        );
    }

    [Fact]
    public void AComparisonWithAParameterIsByCodePoint()
    {
        PureQLProjection projection = new(
            [Words],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.words"),
                where: new GreaterThanStringRow(Word, new ParamString("bound")),
                select: [new SelectItemProjection("id", Id)],
                orderBy: [new OrderItemProjectionExpression(Id)]
            ),
            new Dictionary<string, IParameter>
            {
                ["bound"] = new StringParameter(new String(Replacement)),
            }
        );

        Assert.Equal(["1", "2", "4", "7"], ResultColumn.Texts(projection, "id"));
    }

    [Fact]
    public void AComparisonInAJoinIsByCodePoint()
    {
        IStoredSchemaDataSet bounds = new InlineTable(
            "u",
            "bounds",
            ("bound", new StringColumnType())
        ).With([Last]);

        PureQLProjection projection = new(
            [Words, bounds],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.words"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Inner,
                        "u.bounds",
                        new LessThanStringRow(
                            new FieldString("u.bounds", "bound"),
                            Word
                        )
                    ),
                ],
                select: [new SelectItemProjection("id", Id)],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        Assert.Equal(["2", "4"], ResultColumn.Texts(projection, "id"));
    }

    [Fact]
    public void FirstAndLastSortTheirRowsByCodePoint()
    {
        PureQLProjection projection = new(
            [Words],
            new MainTotalQuery(
                from: new FromEntity("t.words"),
                aggregate:
                [
                    new SelectItemTotal(
                        "first",
                        new FirstIntegerNullableTotal(
                            Id,
                            [new OrderItemRow(Word), new OrderItemRow(Id)]
                        )
                    ),
                    new SelectItemTotal(
                        "last",
                        new LastIntegerNullableTotal(
                            Id,
                            [new OrderItemRow(Word), new OrderItemRow(Id)]
                        )
                    ),
                    new SelectItemTotal(
                        "firstWord",
                        new FirstIntegerNullableTotal(
                            Id,
                            [new OrderItemRow(Word), new OrderItemRow(Id)],
                            new NotEqualStringRow(Word, new LiteralStringNullable())
                        )
                    ),
                    new SelectItemTotal(
                        "lastDown",
                        new LastIntegerNullableTotal(
                            Id,
                            [
                                new OrderItemRow(Word, SortDirection.Desc),
                                new OrderItemRow(Id),
                            ]
                        )
                    ),
                ]
            )
        );

        Assert.Equal(["6"], ResultColumn.Texts(projection, "first"));
        Assert.Equal(["4"], ResultColumn.Texts(projection, "last"));
        Assert.Equal(["3"], ResultColumn.Texts(projection, "firstWord"));
        Assert.Equal(["6"], ResultColumn.Texts(projection, "lastDown"));
    }

    [Fact]
    public void ConcatAllJoinsInCodePointOrder()
    {
        PureQLProjection projection = new(
            [Words],
            new MainTotalQuery(
                from: new FromEntity("t.words"),
                aggregate:
                [
                    new SelectItemTotal(
                        "all",
                        new ConcatAllTotal(
                            Word,
                            new LiteralString(","),
                            [new OrderItemRow(Word)]
                        )
                    ),
                    new SelectItemTotal(
                        "distinct",
                        new ConcatAllTotal(
                            Word,
                            new LiteralString(","),
                            [new OrderItemRow(Word, SortDirection.Desc)],
                            distinct: true
                        )
                    ),
                ]
            )
        );

        Assert.Equal(
            [string.Join(",", Private, Replacement, Last, Last, Linear, Grin)],
            ResultColumn.Texts(projection, "all")
        );
        Assert.Equal(
            [string.Join(",", Grin, Linear, Last, Replacement, Private)],
            ResultColumn.Texts(projection, "distinct")
        );
    }

    [Fact]
    public void RankingFunctionsOrderTheirWindowByCodePoint()
    {
        PureQLProjection projection = new(
            [Words],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.words"),
                select:
                [
                    new SelectItemProjection(
                        "rowNumber",
                        new RowNumberProjection(
                            new OrderedWindow([
                                new OrderItemRow(Word),
                                new OrderItemRow(Id),
                            ])
                        )
                    ),
                    new SelectItemProjection(
                        "rank",
                        new RankProjection(new OrderedWindow([new OrderItemRow(Word)]))
                    ),
                    new SelectItemProjection(
                        "denseRank",
                        new DenseRankProjection(
                            new OrderedWindow([
                                new OrderItemRow(Word, SortDirection.Desc),
                            ])
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        Assert.Equal(
            ["4", "6", "2", "7", "3", "1", "5"],
            ResultColumn.Texts(projection, "rowNumber")
        );
        Assert.Equal(
            ["4", "6", "2", "7", "3", "1", "4"],
            ResultColumn.Texts(projection, "rank")
        );
        Assert.Equal(
            ["3", "2", "5", "1", "4", "6", "3"],
            ResultColumn.Texts(projection, "denseRank")
        );
    }

    // mode: "among several, the least by the order of the type";
    // percentileDiscrete: "the value number ceil(fraction x n) ... among the
    // n non-null values in order".
    [Fact]
    public void ModeAndPercentileDiscreteOrderTheValuesByCodePoint()
    {
        PureQLProjection projection = Grouped(
            new SelectItemGroup("mode", new ModeStringNullableGroup(Word)),
            new SelectItemGroup(
                "median",
                new PercentileDiscreteStringNullableGroup(Word, new LiteralDecimal(0.5m))
            ),
            new SelectItemGroup(
                "first",
                new PercentileDiscreteStringNullableGroup(Word, new LiteralDecimal(0m))
            ),
            new SelectItemGroup(
                "top",
                new PercentileDiscreteStringNullableGroup(Word, new LiteralDecimal(1m))
            )
        );

        Assert.Equal([Last, Private, Last], ResultColumn.Texts(projection, "mode"));
        Assert.Equal([Last, Replacement, Last], ResultColumn.Texts(projection, "median"));
        Assert.Equal([Last, Private, Last], ResultColumn.Texts(projection, "first"));
        Assert.Equal([Linear, Grin, Last], ResultColumn.Texts(projection, "top"));
    }

    [Fact]
    public void DistinctByKeepsTheFirstRowInCodePointOrder()
    {
        PureQLProjection projection = new(
            [Words],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.words"),
                select:
                [
                    new SelectItemProjection("id", Id),
                    new SelectItemProjection("grp", Grp),
                ],
                distinctBy: ["grp"],
                orderBy:
                [
                    new OrderItemProjectionExpression(Word),
                    new OrderItemProjectionExpression(Id),
                ]
            )
        );

        Assert.Equal(["6", "3", "1"], ResultColumn.Texts(projection, "id"));
    }

    [Fact]
    public void HavingComparesAnAggregateByCodePoint()
    {
        PureQLProjection projection = new(
            [Words],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.words"),
                groupBy: [new GroupKey("grp", Grp)],
                select: [new SelectItemGroup("grp", new KeyString("grp"))],
                having: new GreaterThanStringGroup(
                    new MaxStringNullableGroup(Word),
                    new LiteralString(Last)
                ),
                orderBy: [new OrderItemGroupColumn("grp")]
            )
        );

        Assert.Equal(["x", "y"], ResultColumn.Texts(projection, "grp"));
    }

    [Theory]
    [InlineData(SortDirection.Asc, "3")]
    [InlineData(SortDirection.Desc, "4")]
    public void ASubqueryPageReadByInIsTakenInCodePointOrder(
        SortDirection direction,
        string expected
    )
    {
        PureQLProjection projection = new(
            [Words],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "extreme",
                        new PlainQueryWithPagination(
                            from: new FromEntity("t.words"),
                            pagination: new Pagination(take: 1),
                            where: new NotEqualStringRow(
                                Word,
                                new LiteralStringNullable()
                            ),
                            select: [new SelectItemProjection("word", Word)],
                            orderBy: [new OrderItemProjectionExpression(Word, direction)]
                        )
                    ),
                ],
                from: new FromEntity("t.words"),
                where: new InStringRow(
                    Word,
                    new ListSubqueryColumnStringNullable("extreme", "word")
                ),
                select: [new SelectItemProjection("id", Id)]
            )
        );

        Assert.Equal([expected], ResultColumn.Texts(projection, "id"));
    }

    [Fact]
    public void GroupByAWordMakesOneGroupPerCodePointSequenceInOrder()
    {
        PureQLProjection projection = new(
            [Words],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.words"),
                groupBy: [new GroupKey("word", Word)],
                select:
                [
                    new SelectItemGroup(
                        "word",
                        new CoalesceStringGroup([
                            new KeyStringNullable("word"),
                            new LiteralString("NULL"),
                        ])
                    ),
                    new SelectItemGroup("count", new CountGroup()),
                ],
                orderBy:
                [
                    new OrderItemGroupExpression(new KeyStringNullable("word")),
                ]
            )
        );

        Assert.Equal(
            ["NULL", Private, Replacement, Last, Linear, Grin],
            ResultColumn.Texts(projection, "word")
        );
        Assert.Equal(
            ["1", "1", "1", "2", "1", "1"],
            ResultColumn.Texts(projection, "count")
        );
    }

    // Strings: "equal compares code point by code point: it applies no case
    // folding, Unicode normalization or trimming".
    [Fact]
    public void CountDistinctAndGroupByNeitherNormalizeNorFold()
    {
        PureQLProjection projection = new(
            [Forms],
            new MainTotalQuery(
                from: new FromEntity("t.forms"),
                aggregate:
                [
                    new SelectItemTotal(
                        "distinct",
                        new CountDistinctTotal(new FieldString("t.forms", "form"))
                    ),
                    new SelectItemTotal(
                        "normalized",
                        new CountDistinctTotal(
                            new NormalizeStringRow(
                                new FieldString("t.forms", "form"),
                                NormalizationForm.NFC
                            )
                        )
                    ),
                    new SelectItemTotal(
                        "folded",
                        new CountDistinctTotal(
                            new NormalizeStringRow(
                                new FoldCaseStringRow(new FieldString("t.forms", "form")),
                                NormalizationForm.NFC
                            )
                        )
                    ),
                ]
            )
        );

        Assert.Equal(["4"], ResultColumn.Texts(projection, "distinct"));
        Assert.Equal(["2"], ResultColumn.Texts(projection, "normalized"));
        Assert.Equal(["1"], ResultColumn.Texts(projection, "folded"));
    }

    [Theory]
    [InlineData("union", "2")]
    [InlineData("intersect", "0")]
    [InlineData("except", "1")]
    public void ASetOperationComparesCodePointsWithoutNormalizing(
        string operation,
        string expected
    )
    {
        Query[] inputs =
        [
            new PlainQueryWithoutPaginationWithoutOrderBy(
                from: new FromEntity("t.forms"),
                where: new EqualDecimalRow(
                    new FieldInteger("t.forms", "id"),
                    new LiteralInteger(1)
                ),
                select:
                [
                    new SelectItemProjection("form", new FieldString("t.forms", "form")),
                ]
            ),
            new PlainQueryWithoutPaginationWithoutOrderBy(
                from: new FromEntity("t.forms"),
                where: new EqualDecimalRow(
                    new FieldInteger("t.forms", "id"),
                    new LiteralInteger(2)
                ),
                select:
                [
                    new SelectItemProjection("form", new FieldString("t.forms", "form")),
                ]
            ),
        ];

        PureQLProjection projection = new(
            [Forms],
            operation switch
            {
                "union" => new MainCombinedQueryUnion(inputs),
                "intersect" => new MainCombinedQueryIntersect(inputs),
                _ => new MainCombinedQueryExcept(inputs),
            }
        );

        Assert.Equal(
            expected,
            ResultColumn.Texts(projection, "form").Count.ToString(
                System.Globalization.CultureInfo.InvariantCulture
            )
        );
    }

    [Fact]
    public void NextFollowsAWindowOrderedByCodePointEitherWay()
    {
        OrderedWindow byWord = new([new OrderItemRow(Word), new OrderItemRow(Id)]);
        PureQLProjection projection = new(
            [Words],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.words"),
                select:
                [
                    new SelectItemProjection(
                        "following",
                        new NextIntegerProjection(Id, new LiteralInteger(0), byWord)
                    ),
                    new SelectItemProjection(
                        "next",
                        new NextIntegerNullableProjection(
                            Id,
                            new OrderedWindow([
                                new OrderItemRow(Word, SortDirection.Desc),
                                new OrderItemRow(Id, SortDirection.Desc),
                            ])
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        Assert.Equal(
            ["7", "4", "5", "0", "1", "3", "2"],
            ResultColumn.Texts(projection, "following")
        );
        Assert.Equal(
            ["5", "7", "6", "2", "3", "", "1"],
            ResultColumn.Texts(projection, "next")
        );
    }

    [Fact]
    public void GroupsSortByAnAggregateInCodePointOrder()
    {
        PureQLProjection projection = new(
            [Words],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.words"),
                groupBy: [new GroupKey("grp", Grp)],
                select: [new SelectItemGroup("grp", new KeyString("grp"))],
                orderBy:
                [
                    new OrderItemGroupExpression(
                        new MaxStringNullableGroup(Word),
                        SortDirection.Desc
                    ),
                ]
            )
        );

        Assert.Equal(["y", "x", "z"], ResultColumn.Texts(projection, "grp"));
    }

    // greatest and least are lifted: "the result is null when any operand is
    // null; unlike max and min they do not skip it".
    [Fact]
    public void GreatestAndLeastOfANullAreNull()
    {
        Assert.Equal(
            ["NULL", "NULL"],
            Constants(
                new CoalesceStringProjection([
                    new GreatestStringNullableProjection([
                        new LiteralString(Grin),
                        new LiteralStringNullable(),
                    ]),
                    new LiteralString("NULL"),
                ]),
                new CoalesceStringProjection([
                    new LeastStringNullableProjection([
                        new LiteralStringNullable(),
                        new LiteralString(Private),
                    ]),
                    new LiteralString("NULL"),
                ])
            )
        );
    }

    [Fact]
    public void HavingComparesAGroupKeyByCodePoint()
    {
        PureQLProjection projection = new(
            [Words],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.words"),
                groupBy: [new GroupKey("word", Word)],
                select: [new SelectItemGroup("word", new KeyStringNullable("word"))],
                having: new GreaterThanStringGroup(
                    new KeyStringNullable("word"),
                    new LiteralString(Last)
                ),
                orderBy: [new OrderItemGroupColumn("word", SortDirection.Desc)]
            )
        );

        Assert.Equal([Grin, Linear], ResultColumn.Texts(projection, "word"));
    }

    // concatAll with distinct: "a value equal to one before it in that order
    // is dropped", equal as equal compares, by code points.
    [Fact]
    public void ConcatAllDistinctKeepsEveryEncodingApart()
    {
        PureQLProjection projection = new(
            [Forms],
            new MainTotalQuery(
                from: new FromEntity("t.forms"),
                aggregate:
                [
                    new SelectItemTotal(
                        "forms",
                        new ConcatAllTotal(
                            new FieldString("t.forms", "form"),
                            new LiteralString("|"),
                            [new OrderItemRow(new FieldInteger("t.forms", "id"))],
                            distinct: true
                        )
                    ),
                ]
            )
        );

        Assert.Equal(
            ["\x00E9|e\x0301|E\x0301|\x00C9"],
            ResultColumn.Texts(projection, "forms")
        );
    }

    private static BooleanRow Compared(
        string comparison,
        StringNullableRow left,
        StringNullableRow right
    )
    {
        return comparison switch
        {
            "greaterThan" => new GreaterThanStringRow(left, right),
            "greaterThanOrEqual" => new GreaterThanOrEqualStringRow(left, right),
            "lessThan" => new LessThanStringRow(left, right),
            _ => new LessThanOrEqualStringRow(left, right),
        };
    }

    // The ids of the words, in the order of the keys.
    private static IReadOnlyList<string> Ids(IEnumerable<OrderItemProjection> orderBy)
    {
        PureQLProjection projection = new(
            [Words],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.words"),
                select: [new SelectItemProjection("id", Id)],
                orderBy: orderBy
            )
        );

        return ResultColumn.Texts(projection, "id");
    }

    // The ids of the words a condition holds for, in order.
    private static IReadOnlyList<string> Where(BooleanRow condition)
    {
        PureQLProjection projection = new(
            [Words],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.words"),
                where: condition,
                select: [new SelectItemProjection("id", Id)],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        return ResultColumn.Texts(projection, "id");
    }

    // The words grouped by grp, in the order of grp.
    private static PureQLProjection Grouped(params SelectItemGroup[] columns)
    {
        return new PureQLProjection(
            [Words],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.words"),
                groupBy: [new GroupKey("grp", Grp)],
                select: [new SelectItemGroup("grp", new KeyString("grp")), .. columns],
                orderBy: [new OrderItemGroupColumn("grp")]
            )
        );
    }

    // The values of expressions over constants, computed over the first row.
    private static IReadOnlyList<string> Constants(params ExprProjection[] expressions)
    {
        PureQLProjection projection = new(
            [Words],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.words"),
                where: new EqualDecimalRow(Id, new LiteralInteger(1)),
                select:
                [
                    .. expressions.Select(
                        (expression, index) =>
                            new SelectItemProjection($"c{index}", expression)
                    ),
                ]
            )
        );

        return
        [
            .. Enumerable
                .Range(0, expressions.Length)
                .Select(index =>
                    Assert.Single(ResultColumn.Texts(projection, $"c{index}"))
                ),
        ];
    }
}
