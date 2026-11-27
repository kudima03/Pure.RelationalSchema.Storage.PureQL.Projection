using Pure.Primitives.Number;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Parameter;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.GroupExpressions;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.TotalExpressions;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// matchesRegex, extractRegex and replaceRegex in queries, as the
// specification defines them under Regular expressions and Null semantics:
// matching is by code point (a character outside the Basic Multilingual
// Plane is one code point for ., a class, a range and a count), always
// case-sensitive, with ASCII sets and anchors at the very ends; the
// replacement is literal text; and "pattern and group are never computed",
// so "a pattern that is not one of the dialect ... or a group the pattern
// does not have is an error whatever the data", checked "before it reads a
// row", for a literal and for a bound parameter alike (Validation; Execution
// errors).
public sealed class RegexEdgeTests
{
    private const string Grin = "\U0001F600";

    private const string Beam = "\U0001F601";

    private const string Smiley = "\U0001F603";

    private const string Rocket = "\U0001F680";

    private static readonly InlineTable Texts = new(
        "t",
        "texts",
        ("id", new LongColumnType()),
        ("text", new StringColumnType())
    );

    private static readonly FieldStringNullable Text = new("t.texts", "text");

    private static readonly FieldInteger Id = new("t.texts", "id");

    [Theory]
    [InlineData(Grin, "True")]
    [InlineData("ab", "False")]
    [InlineData("e\x0301", "False")]
    [InlineData("\x00E9", "True")]
    [InlineData("\n", "False")]
    [InlineData("\r", "True")]
    [InlineData("\U0010FFFF", "True")]
    public void TheDotIsOneCodePoint(string text, string expected)
    {
        Assert.Equal(
            expected,
            Value(text, new MatchesRegexProjection(Text, Pattern("^.$")))
        );
    }

    [Theory]
    [InlineData("^[\U0001F600-\U0001F64F]+$", Smiley + Grin, "True")]
    [InlineData("^[\U0001F600-\U0001F64F]+$", Rocket, "False")]
    [InlineData("^[\U0001F600-\U0001F64F]+$", Grin + "a", "False")]
    [InlineData("[\xFFFF-\U00010001]", "\U00010000", "True")]
    [InlineData("[\xFFFF-\U00010001]", "\xFFFF", "True")]
    [InlineData("[\xFFFF-\U00010001]", "\xFFFE", "False")]
    [InlineData("[\xFFFF-\U00010001]", "\U00010002", "False")]
    [InlineData("[\xE000-\U0001F600]", "\xD7FF", "False")]
    [InlineData("[\xE000-\U0001F600]", "\xFFFD", "True")]
    [InlineData("[\xE000-\U0001F600]", "\U00010000", "True")]
    [InlineData("[\xE000-\U0001F600]", Beam, "False")]
    [InlineData("^[^" + Grin + "]$", Beam, "True")]
    [InlineData("^[^" + Grin + "]$", Grin, "False")]
    [InlineData("^[a" + Grin + "]{2}$", "a" + Grin, "True")]
    [InlineData("^[" + Grin + "-" + Grin + "]$", Grin, "True")]
    public void AClassAndARangeHoldCodePointsInCodePointOrder(
        string pattern,
        string text,
        string expected
    )
    {
        Assert.Equal(
            expected,
            Value(text, new MatchesRegexProjection(Text, Pattern(pattern)))
        );
    }

    [Theory]
    [InlineData(Grin + Beam, "[^" + Grin + "]", Beam)]
    [InlineData(Grin + Grin + Grin + "a", "^.{2}", Grin + Grin)]
    [InlineData(Grin + Grin + Grin, Grin + "+?", Grin)]
    [InlineData(Grin + Grin + Grin, Grin + "{2,}", Grin + Grin + Grin)]
    [InlineData("a" + Grin + " b", "\\S+", "a" + Grin)]
    [InlineData("x" + Grin + Beam, "..$", Grin + Beam)]
    [InlineData("b", "a|", "")]
    public void ExtractRegexCountsCodePoints(string text, string pattern, string expected)
    {
        Assert.Equal(
            Bracketed(expected),
            Value(text, Bracketed(new ExtractRegexProjection(Text, Pattern(pattern))))
        );
    }

    [Fact]
    public void ExtractRegexOfAGroupHoldsWholeCodePoints()
    {
        Assert.Equal(
            Grin,
            Value(
                "x" + Grin + Beam,
                new ExtractRegexProjection(
                    Text,
                    Pattern("(.)(.)$"),
                    new LiteralInteger(1)
                )
            )
        );
        Assert.Equal(
            Beam,
            Value(
                Grin + Beam + Grin + Beam,
                new ExtractRegexProjection(
                    Text,
                    Pattern("(?:" + Grin + "(.))+"),
                    new LiteralInteger(1)
                )
            )
        );
    }

    // replaceRegex: "After an empty match the search moves on by one code
    // point".
    [Theory]
    [InlineData(Grin + Beam, "", "-" + Grin + "-" + Beam + "-")]
    [InlineData(Grin + "b" + Grin, "b*", "-" + Grin + "--" + Grin + "-")]
    [InlineData(Grin + "a", "\\B", "-" + Grin + "a")]
    [InlineData(Grin + "a", "(?:" + Grin + "|a)+?", "--")]
    [InlineData("I \U0001F1EB\U0001F1F7 it", "[\U0001F1E6-\U0001F1FF]{2}", "I - it")]
    [InlineData("e\x0301", ".", "--")]
    public void ReplaceRegexMovesOnByCodePoints(
        string text,
        string pattern,
        string expected
    )
    {
        Assert.Equal(
            Bracketed(expected),
            Value(
                text,
                Bracketed(
                    new ReplaceRegexStringNullableProjection(
                        Text,
                        new LiteralString("-"),
                        Pattern(pattern)
                    )
                )
            )
        );
    }

    // replaceRegex: "replacement is literal text, in which $1 and \1 stand
    // for themselves".
    [Theory]
    [InlineData("$1")]
    [InlineData("\\1")]
    [InlineData("$0")]
    [InlineData("${1}")]
    [InlineData("$&")]
    [InlineData("$$")]
    public void AReplacementIsLiteralTextInAQuery(string replacement)
    {
        Assert.Equal(
            "a" + replacement + "c",
            Value(
                "abc",
                new ReplaceRegexStringNullableProjection(
                    Text,
                    new LiteralString(replacement),
                    Pattern("(b)")
                )
            )
        );
    }

    // Matching is always case-sensitive; "to ignore case, match a pattern in
    // folded form against foldCase of the operand".
    [Theory]
    [InlineData("STRASSE", "False", "True")]
    [InlineData("Stra\x00DFe", "False", "True")]
    [InlineData("strasse", "True", "True")]
    [InlineData("STRA\x1E9EE", "False", "True")]
    public void MatchingIsCaseSensitiveUnlessTheOperandIsFolded(
        string text,
        string plain,
        string folded
    )
    {
        Assert.Equal(
            plain,
            Value(text, new MatchesRegexProjection(Text, Pattern("^strasse$")))
        );
        Assert.Equal(
            folded,
            Value(
                text,
                new MatchesRegexProjection(
                    new FoldCaseStringNullableProjection(Text),
                    Pattern("^strasse$")
                )
            )
        );
    }

    [Fact]
    public void TheSetsAreAsciiInAQuery()
    {
        Assert.Equal(
            "\x00EF\x00E9",
            Value(
                "na\x00EFve_1\x00E9",
                new ReplaceRegexStringNullableProjection(
                    Text,
                    new LiteralString(""),
                    Pattern("\\w")
                )
            )
        );
        Assert.Equal(
            "False",
            Value("\x0663", new MatchesRegexProjection(Text, Pattern("\\d")))
        );
        Assert.Equal(
            "False",
            Value("\x00A0", new MatchesRegexProjection(Text, Pattern("\\s")))
        );
    }

    [Theory]
    [InlineData("abc\n", "abc$", "False")]
    [InlineData("abc\n", "abc\\n$", "True")]
    [InlineData("x\nabc", "^abc", "False")]
    [InlineData("a/b.c", "^a\\/b\\.c$", "True")]
    [InlineData("a/bxc", "^a\\/b\\.c$", "False")]
    public void AnchorsAndEscapesInAQuery(string text, string pattern, string expected)
    {
        Assert.Equal(
            expected,
            Value(text, new MatchesRegexProjection(Text, Pattern(pattern)))
        );
    }

    // Null semantics: matchesRegex is false for null, extractRegex is null
    // for null and when nothing matches (an empty match is not null), and
    // replaceRegex is lifted.
    [Fact]
    public void NullAndEmptyMatchesAreToldApart()
    {
        PureQLProjection projection = new(
            [Texts.With(["1", "abc"], ["2", ""])],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.texts"),
                select:
                [
                    new SelectItemProjection(
                        "matches",
                        new MatchesRegexProjection(Text, Pattern("x*"))
                    ),
                    new SelectItemProjection(
                        "empty",
                        OrNull(new ExtractRegexProjection(Text, Pattern("x*")))
                    ),
                    new SelectItemProjection(
                        "none",
                        OrNull(new ExtractRegexProjection(Text, Pattern("x")))
                    ),
                    new SelectItemProjection(
                        "group",
                        OrNull(
                            new ExtractRegexProjection(
                                Text,
                                Pattern("(x)|a"),
                                new LiteralInteger(1)
                            )
                        )
                    ),
                    new SelectItemProjection(
                        "replaced",
                        OrNull(
                            new ReplaceRegexStringNullableProjection(
                                Text,
                                new LiteralString("-"),
                                Pattern("x*")
                            )
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        Assert.Equal(["True", "False"], ResultColumn.Texts(projection, "matches"));
        Assert.Equal(["[]", "NULL"], ResultColumn.Texts(projection, "empty"));
        Assert.Equal(["NULL", "NULL"], ResultColumn.Texts(projection, "none"));
        Assert.Equal(["NULL", "NULL"], ResultColumn.Texts(projection, "group"));
        Assert.Equal(["[-a-b-c-]", "NULL"], ResultColumn.Texts(projection, "replaced"));
    }

    [Fact]
    public void APatternAndAGroupAreReadFromParameters()
    {
        PureQLProjection projection = new(
            [Texts.With(["1", "AB-123"], ["2", "abc"], ["3", "CD-9"])],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.texts"),
                where: new MatchesRegexRow(Text, new ParamString("pattern")),
                select:
                [
                    new SelectItemProjection(
                        "part",
                        new ExtractRegexProjection(
                            Text,
                            new ParamString("pattern"),
                            new ParamInteger("group")
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            ),
            new Dictionary<string, IParameter>
            {
                ["pattern"] = new StringParameter(new String("^([A-Z]+)-([0-9]+)$")),
                ["group"] = new IntegerParameter(new Long(2)),
            }
        );

        Assert.Equal(["123", "9"], ResultColumn.Texts(projection, "part"));
    }

    [Fact]
    public void ARegexKeyGroupsTheRows()
    {
        PureQLProjection projection = new(
            [Texts.With(["1", "ab-1"], ["2", "ab-2"], ["3", "c-3"], ["4", "99"])],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.texts"),
                groupBy:
                [
                    new GroupKey("prefix", new ExtractRegexRow(Text, Pattern("^[a-z]+"))),
                ],
                select:
                [
                    new SelectItemGroup(
                        "prefix",
                        new CoalesceStringGroup([
                            new KeyStringNullable("prefix"),
                            new LiteralString("NULL"),
                        ])
                    ),
                    new SelectItemGroup("count", new CountGroup()),
                    new SelectItemGroup(
                        "digits",
                        new CountGroup(new MatchesRegexRow(Text, Pattern("[0-9]$")))
                    ),
                ],
                having: new NotEqualStringGroup(
                    new KeyStringNullable("prefix"),
                    new LiteralString("zz")
                ),
                orderBy: [new OrderItemGroupColumn("prefix")]
            )
        );

        Assert.Equal(["NULL", "ab", "c"], ResultColumn.Texts(projection, "prefix"));
        Assert.Equal(["1", "2", "1"], ResultColumn.Texts(projection, "count"));
        Assert.Equal(["1", "2", "1"], ResultColumn.Texts(projection, "digits"));
    }

    // The queries the next tests build fail only for their pattern or group:
    // with a pattern of the dialect and a group it has, each runs.
    [Fact]
    public void TheQueriesOfTheFailingPatternsRunWithAValidOne()
    {
        Assert.Empty(
            ResultColumn.Texts(
                Selecting([], new MatchesRegexProjection(Text, Pattern("(a)"))),
                "value"
            )
        );
        Assert.Equal(
            "False",
            Value("b", new MatchesRegexProjection(new LiteralString("abc"), Pattern("x")))
        );
        Assert.Equal(
            "b",
            Value(
                "ab",
                new ExtractRegexProjection(Text, Pattern("(a)(b)"), new LiteralInteger(2))
            )
        );
        Assert.Equal(
            "b",
            Value(
                "ab",
                new ExtractRegexProjection(
                    new LiteralString("ab"),
                    Pattern("(a)(b)"),
                    new ParamInteger("group")
                ),
                new Dictionary<string, IParameter>
                {
                    ["group"] = new IntegerParameter(new Long(2)),
                }
            )
        );
        Assert.Equal(
            "-",
            Value(
                "ab",
                new ReplaceRegexStringNullableProjection(
                    Text,
                    new LiteralString("-"),
                    new ParamString("pattern")
                ),
                new Dictionary<string, IParameter>
                {
                    ["pattern"] = new StringParameter(new String("^a(b)$")),
                }
            )
        );
    }

    // A literal pattern that is not one of the dialect fails when the
    // projection is built, whatever the data, also over no rows and also
    // where the operand is a literal the simplifier could fold.
    [Theory]
    [InlineData("(a")]
    [InlineData("a)")]
    [InlineData("[z-a]")]
    [InlineData("a{3,2}")]
    [InlineData("(?i)a")]
    [InlineData("\\p{L}")]
    [InlineData("(a)\\1")]
    [InlineData("a(?=b)")]
    [InlineData("a**")]
    [InlineData("*a")]
    [InlineData("[a")]
    [InlineData("\\x41")]
    [InlineData("[\U0001F601-\U0001F600]")]
    public void ALiteralPatternOutsideTheDialectFailsWhenBuilt(string pattern)
    {
        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Selecting([], new MatchesRegexProjection(Text, Pattern(pattern)))
        );
        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Selecting(
                [],
                new MatchesRegexProjection(new LiteralString("abc"), Pattern(pattern))
            )
        );
        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Selecting(
                [["1", "abc"]],
                new ExtractRegexProjection(new LiteralString("abc"), Pattern(pattern))
            )
        );
        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Selecting(
                [["1", "abc"]],
                new ReplaceRegexStringProjection(
                    new LiteralString("abc"),
                    new LiteralString("-"),
                    Pattern(pattern)
                )
            )
        );
    }

    [Theory]
    [InlineData("(a)(b)", 3)]
    [InlineData("ab", 1)]
    [InlineData("(?:a)b", 1)]
    [InlineData("\\(a\\)", 1)]
    [InlineData("[(]a[)]", 1)]
    [InlineData("(a)", 0)]
    [InlineData("(a)", -1)]
    public void ALiteralGroupThePatternDoesNotHaveFailsWhenBuilt(
        string pattern,
        long group
    )
    {
        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Selecting(
                [],
                new ExtractRegexProjection(
                    Text,
                    Pattern(pattern),
                    new LiteralInteger(group)
                )
            )
        );
        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Selecting(
                [["1", "ab"]],
                new ExtractRegexProjection(
                    new LiteralString("ab"),
                    Pattern(pattern),
                    new LiteralInteger(group)
                )
            )
        );
    }

    [Theory]
    [InlineData("(a")]
    [InlineData("(?=a)")]
    [InlineData("[[:alpha:]]")]
    [InlineData("a{2,1}")]
    [InlineData("(?<n>a)")]
    [InlineData("a]")]
    public void APatternParameterOutsideTheDialectFailsWhenBuilt(string pattern)
    {
        Dictionary<string, IParameter> parameters = new()
        {
            ["pattern"] = new StringParameter(new String(pattern)),
        };

        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Selecting(
                [],
                new MatchesRegexProjection(Text, new ParamString("pattern")),
                parameters
            )
        );
        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Selecting(
                [["1", "abc"]],
                new ReplaceRegexStringNullableProjection(
                    Text,
                    new LiteralString("-"),
                    new ParamString("pattern")
                ),
                parameters
            )
        );
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(3)]
    [InlineData(long.MaxValue)]
    public void AGroupParameterThePatternDoesNotHaveFailsWhenBuilt(long group)
    {
        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Selecting(
                [["1", "ab"]],
                new ExtractRegexProjection(
                    Text,
                    Pattern("(a)(b)"),
                    new ParamInteger("group")
                ),
                new Dictionary<string, IParameter>
                {
                    ["group"] = new IntegerParameter(new Long(group)),
                }
            )
        );
    }

    // A group parameter is checked against the pattern it goes with, which
    // may itself be a parameter.
    [Fact]
    public void AGroupParameterIsCheckedAgainstAPatternParameter()
    {
        Dictionary<string, IParameter> parameters = new()
        {
            ["pattern"] = new StringParameter(new String("(a)")),
            ["group"] = new IntegerParameter(new Long(2)),
        };

        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Selecting(
                [["1", "ab"]],
                new ExtractRegexProjection(
                    Text,
                    new ParamString("pattern"),
                    new ParamInteger("group")
                ),
                parameters
            )
        );
    }

    // Execution errors: "is run with a missing parameter ... null for a
    // non-null parameter".
    [Fact]
    public void APatternParameterThatIsMissingOrNullFailsWhenBuilt()
    {
        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Selecting(
                [["1", "ab"]],
                new MatchesRegexProjection(Text, new ParamString("p"))
            )
        );
        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Selecting(
                [["1", "ab"]],
                new MatchesRegexProjection(Text, new ParamString("p")),
                new Dictionary<string, IParameter>
                {
                    ["p"] = new NullableStringParameter(),
                }
            )
        );
    }

    // The checks are made wherever the pattern stands, and "whatever the
    // data": with a pattern the dialect has and the group it has, each query
    // builds and runs; with a pattern outside the dialect, or a group it does
    // not have, it fails when it is built.
    [Theory]
    [InlineData("where")]
    [InlineData("join")]
    [InlineData("having")]
    [InlineData("total")]
    [InlineData("subquery")]
    [InlineData("orderBy")]
    [InlineData("predicate")]
    [InlineData("partition")]
    [InlineData("union")]
    [InlineData("untaken")]
    public void APatternIsCheckedWhereverItStands(string place)
    {
        _ = Placed(place, "(a)", 1).ToList();
        _ = Assert.ThrowsAny<ArgumentException>(() => Placed(place, "(a", 1));
        _ = Assert.ThrowsAny<ArgumentException>(() => Placed(place, "a{2,1}", 1));
        _ = Assert.ThrowsAny<ArgumentException>(() => Placed(place, "(a)", 2));
    }

    // A query that reads a regular expression and its group at one place.
    private static PureQLProjection Placed(string place, string pattern, long group)
    {
        IStoredSchemaDataSet texts = Texts.With(["1", "ab"]);
        FieldInteger id = new("t.texts", "id");
        ExtractRegexRow extracted = new(
            Text,
            Pattern(pattern),
            new LiteralInteger(group)
        );
        BooleanRow condition = new EqualStringRow(extracted, new LiteralString("a"));

        return place switch
        {
            "where" => new PureQLProjection(
                [texts],
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.texts"),
                    where: condition,
                    select: [new SelectItemProjection("id", id)]
                )
            ),
            "join" => new PureQLProjection(
                [texts],
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.texts", "a"),
                    joins:
                    [
                        new JoinEntity(
                            JoinKind.Left,
                            "t.texts",
                            new EqualStringRow(
                                new ExtractRegexRow(
                                    new FieldStringNullable("b", "text"),
                                    Pattern(pattern),
                                    new LiteralInteger(group)
                                ),
                                new LiteralString("a")
                            ),
                            "b"
                        ),
                    ],
                    select: [new SelectItemProjection("id", new FieldInteger("a", "id"))]
                )
            ),
            "having" => new PureQLProjection(
                [texts],
                new MainGroupedQueryWithoutPagination(
                    from: new FromEntity("t.texts"),
                    groupBy: [new GroupKey("text", Text)],
                    select: [new SelectItemGroup("text", new KeyStringNullable("text"))],
                    having: new EqualStringGroup(
                        new ExtractRegexGroup(
                            new KeyStringNullable("text"),
                            Pattern(pattern),
                            new LiteralInteger(group)
                        ),
                        new LiteralString("a")
                    )
                )
            ),
            "total" => new PureQLProjection(
                [texts],
                new MainTotalQuery(
                    from: new FromEntity("t.texts"),
                    aggregate: [new SelectItemTotal("n", new CountTotal(condition))]
                )
            ),
            "subquery" => new PureQLProjection(
                [texts],
                new MainPlainQueryWithoutPagination(
                    subqueries:
                    [
                        new SubqueryQuery(
                            "matching",
                            new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.texts"),
                                where: condition,
                                select: [new SelectItemProjection("text", Text)]
                            )
                        ),
                    ],
                    from: new FromSubquery("matching"),
                    select:
                    [
                        new SelectItemProjection(
                            "text",
                            new FieldStringNullable("matching", "text")
                        ),
                    ]
                )
            ),
            "orderBy" => new PureQLProjection(
                [texts],
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.texts"),
                    select: [new SelectItemProjection("id", id)],
                    orderBy:
                    [
                        new OrderItemProjectionExpression(
                            new ExtractRegexProjection(
                                Text,
                                Pattern(pattern),
                                new LiteralInteger(group)
                            )
                        ),
                    ]
                )
            ),
            "predicate" => new PureQLProjection(
                [texts],
                new MainGroupedQueryWithoutPagination(
                    from: new FromEntity("t.texts"),
                    groupBy: [new GroupKey("id", id)],
                    select: [new SelectItemGroup("n", new CountGroup(condition))]
                )
            ),
            "partition" => new PureQLProjection(
                [texts],
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.texts"),
                    select:
                    [
                        new SelectItemProjection(
                            "n",
                            new CountProjection(
                                over: new WindowWithoutFrameWithoutRange([extracted])
                            )
                        ),
                    ]
                )
            ),
            "union" => new PureQLProjection(
                [texts],
                new MainCombinedQueryUnion([
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromEntity("t.texts"),
                        select: [new SelectItemProjection("text", Text)]
                    ),
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromEntity("t.texts"),
                        where: condition,
                        select: [new SelectItemProjection("text", Text)]
                    ),
                ])
            ),
            _ => new PureQLProjection(
                [texts],
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.texts"),
                    select:
                    [
                        new SelectItemProjection(
                            "value",
                            new IfStringNullableProjection(
                                new ExtractRegexProjection(
                                    Text,
                                    Pattern(pattern),
                                    new LiteralInteger(group)
                                ),
                                new LiteralString("x"),
                                new LiteralBoolean(false)
                            )
                        ),
                    ]
                )
            ),
        };
    }

    // A valid pattern written in a literal is matched the same when the
    // operand is a literal folded at build time and when it is read.
    [Theory]
    [InlineData("^[\U0001F600-\U0001F64F]$", Smiley)]
    [InlineData("a.c", "a" + Grin + "c")]
    [InlineData("\\b" + "a", Grin + "a")]
    public void AFoldedMatchAgreesWithAStoredOne(string pattern, string text)
    {
        Assert.Equal(
            Value(text, new MatchesRegexProjection(Text, Pattern(pattern))),
            Value(
                text,
                new MatchesRegexProjection(new LiteralString(text), Pattern(pattern))
            )
        );
        Assert.Equal(
            "True",
            Value(
                text,
                new MatchesRegexProjection(new LiteralString(text), Pattern(pattern))
            )
        );
    }

    private static LiteralString Pattern(string pattern)
    {
        return new LiteralString(pattern);
    }

    private static string Bracketed(string text)
    {
        return $"[{text}]";
    }

    private static CoalesceStringProjection Bracketed(StringNullableProjection expression)
    {
        return new CoalesceStringProjection([
            new ConcatStringNullableProjection([
                new LiteralString("["),
                expression,
                new LiteralString("]"),
            ]),
            new LiteralString("NULL"),
        ]);
    }

    // A string that may be null, between brackets, or NULL.
    private static CoalesceStringProjection OrNull(StringNullableProjection expression)
    {
        return Bracketed(expression);
    }

    private static PureQLProjection Selecting(
        string[][] rows,
        ExprProjection expression,
        Dictionary<string, IParameter>? parameters = null
    )
    {
        PureQLProjection projection = new(
            [Texts.With(rows)],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.texts"),
                select: [new SelectItemProjection("value", expression)]
            ),
            parameters ?? []
        );

        return projection;
    }

    private static string Value(
        string text,
        ExprProjection expression,
        Dictionary<string, IParameter>? parameters = null
    )
    {
        return Assert.Single(
            ResultColumn.Texts(Selecting([["1", text]], expression, parameters), "value")
        );
    }
}
