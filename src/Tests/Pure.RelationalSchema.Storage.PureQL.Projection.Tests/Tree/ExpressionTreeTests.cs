using System.Linq.Expressions;
using Pure.Primitives.Number;
using Pure.RelationalSchema.Abstractions.Table;
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

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Tree;

// The expression tree a projection exposes for a LINQ provider to translate:
// one constant root per stored table, read for only the fields the query
// reads; conditions at the sources they read; joins hashed on the equalities
// of their conditions; folded constants; no redundant Where, Select, OrderBy
// or DistinctBy; and pagination last.
public sealed class ExpressionTreeTests
{
    private static readonly IStoredSchemaDataSet Items = new InlineTable(
        "t",
        "items",
        ("id", new LongColumnType()),
        ("name", new StringColumnType()),
        ("price", new DoubleColumnType()),
        ("active", new BoolColumnType())
    ).With(
        ["1", "pen", "1.5", "True"],
        ["2", "ink", "", "False"],
        ["3", "pad", "4", "True"]
    );

    private static readonly IStoredSchemaDataSet Tags = new InlineTable(
        "s",
        "tags",
        ("item_id", new LongColumnType()),
        ("tag", new StringColumnType())
    ).With(["1", "pen"], ["3", "office"], ["4", "spare"]);

    [Fact]
    public void StoredTableIsItsOwnRootReadForTheFieldsTheQueryReads()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                select: [new SelectItemProjection("name", new FieldString("i", "name"))]
            )
        );

        Assert.Equal(
            "[items].Select(i => (i[name]!))"
                + ".Select(row => new ResultRow([name], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Same(Items.Values.Single(), Roots(projection.Expression).Single());
    }

    [Fact]
    public void StoredCellDeclaredNullableIsReadAsItsParsedTextWithEmptyTextAsNull()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                select:
                [
                    new SelectItemProjection("id", new FieldIntegerNullable("i", "id")),
                ]
            )
        );

        Assert.Equal(
            "[items].Select(i => ((String.IsNullOrEmpty(i.Cells[id].Value.TextValue) ? "
                + "null : (long?)Int64.Parse(i.Cells[id].Value.TextValue, "
                + "NumberStyles.Integer, CultureInfo.InvariantCulture))))"
                + ".Select(row => new ResultRow([id], new[] { (object)row.Item1 }))",
            TreeText.Full(projection.Expression)
        );
        Assert.Equal(
            "[items].Select(i => (i[id]))"
                + ".Select(row => new ResultRow([id], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
    }

    // A provider sees a value that is never null, so SQL compares, joins and
    // filters on it with =, which a hash join or an index serves (issue #243).
    [Fact]
    public void StoredCellDeclaredNonNullIsReadAsItsParsedTextWithoutANullCheck()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("i", "id")),
                    new SelectItemProjection("name", new FieldString("i", "name")),
                ]
            )
        );

        Assert.Equal(
            "[items].Select(i => ((long?)Int64.Parse(i.Cells[id].Value.TextValue, "
                + "NumberStyles.Integer, CultureInfo.InvariantCulture), "
                + "i.Cells[name].Value.TextValue))"
                + ".Select(row => new ResultRow([id, name], "
                + "new[] { (object)row.Item1, (object)row.Item2 }))",
            TreeText.Full(projection.Expression)
        );
        Assert.Equal(
            "[items].Select(i => (i[id]!, i[name]!))"
                + ".Select(row => new ResultRow([id, name], "
                + "new[] { (object)row.Item1, (object)row.Item2 }))",
            TreeText.Of(projection.Expression)
        );
    }

    // Storage does not say whether a column holds NULL, so a query may declare
    // a field both ways; a nullable declaration where the source has a row of
    // its own wins, so the read never fails on a cell the query allows.
    [Fact]
    public void StoredCellDeclaredNullableAnywhereItsSourceHasARowIsReadNullable()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                where: new GreaterThanDecimalRow(
                    new FieldIntegerNullable("i", "id"),
                    new LiteralInteger(1)
                ),
                select: [new SelectItemProjection("id", new FieldInteger("i", "id"))]
            )
        );

        Assert.Equal(
            "[items].Select(i => (i[id])).Where(i => (i.Item1 > 1L))"
                + ".Select(row => new ResultRow([id], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Equal(["2", "3"], ResultColumn.Texts(projection, "id"));
    }

    // A field of the optional side of an outer join is declared nullable
    // after the join whatever it holds, so only its declarations before the
    // join, in on, say whether its cells are read without a null check.
    [Fact]
    public void StoredCellOfAnOptionalSourceIsReadAsItsJoinConditionDeclaresIt()
    {
        PureQLProjection projection = new(
            [Items, Tags],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Left,
                        "s.tags",
                        new EqualDecimalRow(
                            new FieldInteger("g", "item_id"),
                            new FieldInteger("i", "id")
                        ),
                        "g"
                    ),
                ],
                select:
                [
                    new SelectItemProjection("tag", new FieldStringNullable("g", "tag")),
                    new SelectItemProjection(
                        "item",
                        new FieldIntegerNullable("g", "item_id")
                    ),
                ]
            )
        );

        Assert.Equal(
            "[items].Select(i => (i[id]!))"
                + ".GroupJoin([tags].Select(g => (g[item_id]!, g[tag])), "
                + "left => (left.Item1), g => (g.Item1), "
                + "(left, matches) => (left, matches))"
                + ".SelectMany(match => match.Item2.DefaultIfEmpty(), "
                + "(match, g) => (match.Item1, g))"
                + ".Select(row => (row.Item2.Item2, row.Item2.Item1))"
                + ".Select(row => new ResultRow([tag, item], "
                + "new[] { (object)row.Item1, (object)row.Item2 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Equal(["pen", "", "office"], ResultColumn.Texts(projection, "tag"));
    }

    [Fact]
    public void JoinedTablesOfTwoDatasetsAreTwoRoots()
    {
        PureQLProjection projection = new(
            [Items, Tags],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Inner,
                        "s.tags",
                        new EqualDecimalRow(
                            new FieldInteger("g", "item_id"),
                            new FieldInteger("i", "id")
                        ),
                        "g"
                    ),
                ],
                select: [new SelectItemProjection("tag", new FieldString("g", "tag"))]
            )
        );

        Assert.Equal(
            "[items].Select(i => (i[id]!))"
                + ".Join([tags].Select(g => (g[item_id]!, g[tag]!)), "
                + "left => (left.Item1), g => (g.Item1), (left, g) => (left, g))"
                + ".Select(row => (row.Item2.Item2))"
                + ".Select(row => new ResultRow([tag], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Equal(
            [Items.Values.Single(), Tags.Values.Single()],
            Roots(projection.Expression)
        );
    }

    [Fact]
    public void ConstantsFoldAndAlwaysTrueConditionsAreDropped()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                where: new AndRow([
                    new LiteralBoolean(true),
                    new GreaterThanDecimalRow(
                        new FieldInteger("i", "id"),
                        new AddIntegerRow([new LiteralInteger(1), new LiteralInteger(1)])
                    ),
                ]),
                select:
                [
                    new SelectItemProjection(
                        "label",
                        new ConcatStringProjection([
                            new LiteralString("a"),
                            new LiteralString("b"),
                        ])
                    ),
                ]
            )
        );

        Assert.Equal(
            "[items].Select(i => (i[id]!)).Where(i => (i.Item1 > 2L))"
                + ".Select(row => (\"ab\"))"
                + ".Select(row => new ResultRow([label], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Equal(["ab"], ResultColumn.Texts(projection, "label"));
    }

    [Fact]
    public void ConstantComputationThatFailsIsLeftToFailTheQuery()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                select:
                [
                    new SelectItemProjection(
                        "ratio",
                        new DivideDecimalProjection([
                            new LiteralDecimal(1),
                            new LiteralDecimal(0),
                        ])
                    ),
                ]
            )
        );

        Assert.Equal(
            "[items].Select(i => ())"
                + ".Select(row => ((decimal?)(1m / 0m)))"
                + ".Select(row => new ResultRow([ratio], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
        _ = Assert.Throws<DivideByZeroException>(() => projection.ToList());
    }

    [Fact]
    public void ConditionsMoveToTheSourcesTheyReadOrBecomeJoinKeys()
    {
        PureQLProjection projection = new(
            [Items, Tags],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Inner,
                        "s.tags",
                        new EqualDecimalRow(
                            new FieldInteger("g", "item_id"),
                            new FieldInteger("i", "id")
                        ),
                        "g"
                    ),
                ],
                where: new AndRow([
                    new FieldBoolean("i", "active"),
                    new EqualStringRow(
                        new FieldString("g", "tag"),
                        new FieldString("i", "name")
                    ),
                    new NotEqualStringRow(
                        new FieldString("g", "tag"),
                        new LiteralString("spare")
                    ),
                ]),
                select: [new SelectItemProjection("name", new FieldString("i", "name"))]
            )
        );

        Assert.Equal(
            "[items].Select(i => (i[id]!, i[name]!, i[active]!))"
                + ".Where(i => (i.Item3 == true))"
                + ".Join([tags].Select(g => (g[item_id]!, g[tag]!))"
                + ".Where(g => (g.Item2 != \"spare\")), "
                + "left => (left.Item1, left.Item2), g => (g.Item1, g.Item2), "
                + "(left, g) => (left, g))"
                + ".Select(row => (row.Item1.Item2))"
                + ".Select(row => new ResultRow([name], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Equal(["pen"], ResultColumn.Texts(projection, "name"));
    }

    [Fact]
    public void ConditionOnTheOptionalSideOfALeftJoinStaysAfterTheJoin()
    {
        PureQLProjection projection = new(
            [Items, Tags],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Left,
                        "s.tags",
                        new EqualDecimalRow(
                            new FieldIntegerNullable("g", "item_id"),
                            new FieldInteger("i", "id")
                        ),
                        "g"
                    ),
                ],
                where: new AndRow([
                    new FieldBoolean("i", "active"),
                    new EqualStringRow(
                        new FieldStringNullable("g", "tag"),
                        new LiteralStringNullable()
                    ),
                ]),
                select: [new SelectItemProjection("name", new FieldString("i", "name"))]
            )
        );

        Assert.Equal(
            "[items].Select(i => (i[id]!, i[name]!, i[active]!))"
                + ".Where(i => (i.Item3 == true))"
                + ".GroupJoin([tags].Select(g => (g[item_id], g[tag])), "
                + "left => (left.Item1), g => (g.Item1), "
                + "(left, matches) => (left, matches))"
                + ".SelectMany(match => match.Item2.DefaultIfEmpty(), "
                + "(match, g) => (match.Item1, g))"
                + ".Where(row => (row.Item2.Item2 == null))"
                + ".Select(row => (row.Item1.Item2))"
                + ".Select(row => new ResultRow([name], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Empty(ResultColumn.Texts(projection, "name"));
    }

    [Fact]
    public void RightJoinIsDrivenByTheJoinedSource()
    {
        PureQLProjection projection = new(
            [Items, Tags],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Right,
                        "s.tags",
                        new EqualDecimalRow(
                            new FieldInteger("g", "item_id"),
                            new FieldIntegerNullable("i", "id")
                        ),
                        "g"
                    ),
                ],
                select:
                [
                    new SelectItemProjection("tag", new FieldString("g", "tag")),
                    new SelectItemProjection(
                        "name",
                        new FieldStringNullable("i", "name")
                    ),
                ]
            )
        );

        Assert.Equal(
            "[tags].Select(g => (g[item_id]!, g[tag]!))"
                + ".GroupJoin([items].Select(i => (i[id], i[name])), "
                + "g => (g.Item1), left => (left.Item1), (g, lefts) => (g, lefts))"
                + ".SelectMany(match => match.Item2.DefaultIfEmpty(), "
                + "(match, left) => (left, match.Item1))"
                + ".Select(row => (row.Item2.Item2, row.Item1.Item2))"
                + ".Select(row => new ResultRow([tag, name], new[] { (object)row.Item1, "
                + "(object)row.Item2 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Equal(["pen", "office", "spare"], ResultColumn.Texts(projection, "tag"));
        Assert.Equal(["pen", "pad", ""], ResultColumn.Texts(projection, "name"));
    }

    [Fact]
    public void FullJoinIsTheLeftJoinAndTheUnmatchedJoinedRows()
    {
        PureQLProjection projection = new(
            [Items, Tags],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Full,
                        "s.tags",
                        new EqualDecimalRow(
                            new FieldIntegerNullable("g", "item_id"),
                            new FieldIntegerNullable("i", "id")
                        ),
                        "g"
                    ),
                ],
                select:
                [
                    new SelectItemProjection(
                        "name",
                        new FieldStringNullable("i", "name")
                    ),
                    new SelectItemProjection("tag", new FieldStringNullable("g", "tag")),
                ]
            )
        );

        Assert.Equal(
            "[items].Select(i => (i[id], i[name]))"
                + ".GroupJoin([tags].Select(g => (g[item_id], g[tag])), "
                + "left => (left.Item1), g => (g.Item1), "
                + "(left, matches) => (left, matches))"
                + ".SelectMany(match => match.Item2.DefaultIfEmpty(), "
                + "(match, g) => (match.Item1, g))"
                + ".Concat([tags].Select(g => (g[item_id], g[tag]))"
                + ".GroupJoin([items].Select(i => (i[id], i[name])), "
                + "g => (g.Item1), left => (left.Item1), (g, lefts) => (g, lefts))"
                + ".Where(match => !match.Item2.Any())"
                + ".Select(match => (default(ValueTuple<long?, string>), match.Item1)))"
                + ".Select(row => (row.Item1.Item2, row.Item2.Item2))"
                + ".Select(row => new ResultRow([name, tag], new[] { (object)row.Item1, "
                + "(object)row.Item2 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Equal(["pen", "ink", "pad", ""], ResultColumn.Texts(projection, "name"));
        Assert.Equal(
            ["pen", "", "office", "spare"],
            ResultColumn.Texts(projection, "tag")
        );
    }

    [Fact]
    public void JoinWithoutAnEqualityIsACorrelatedSelectMany()
    {
        PureQLProjection projection = new(
            [Items, Tags],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Left,
                        "s.tags",
                        new GreaterThanDecimalRow(
                            new FieldIntegerNullable("g", "item_id"),
                            new FieldInteger("i", "id")
                        ),
                        "g"
                    ),
                ],
                select:
                [
                    new SelectItemProjection("tag", new FieldStringNullable("g", "tag")),
                ]
            )
        );

        Assert.Equal(
            "[items].Select(i => (i[id]!))"
                + ".SelectMany(left => [tags].Select(g => (g[item_id], g[tag]))"
                + ".Where(g => (g.Item1 > left.Item1)).DefaultIfEmpty(), "
                + "(left, g) => (left, g))"
                + ".Select(row => (row.Item2.Item2))"
                + ".Select(row => new ResultRow([tag], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Equal(
            ["office", "spare", "office", "spare", "spare"],
            ResultColumn.Texts(projection, "tag")
        );
    }

    [Fact]
    public void JoinConditionsBesideTheKeysFilterThePairsOfAHashJoin()
    {
        Join join(JoinKind kind)
        {
            return new JoinEntity(
                kind,
                "s.tags",
                new AndRow([
                    new EqualDecimalRow(
                        new FieldIntegerNullable("g", "item_id"),
                        new FieldInteger("i", "id")
                    ),
                    new NotEqualStringRow(
                        new FieldStringNullable("g", "tag"),
                        new FieldString("i", "name")
                    ),
                ]),
                "g"
            );
        }

        PureQLQuery query(JoinKind kind)
        {
            return new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                joins: [join(kind)],
                select:
                [
                    new SelectItemProjection("tag", new FieldStringNullable("g", "tag")),
                ]
            );
        }

        PureQLProjection inner = new([Items, Tags], query(JoinKind.Inner));
        PureQLProjection left = new([Items, Tags], query(JoinKind.Left));

        Assert.Equal(
            "[items].Select(i => (i[id]!, i[name]!))"
                + ".Join([tags].Select(g => (g[item_id], g[tag])), "
                + "left => (left.Item1), g => (g.Item1), (left, g) => (left, g))"
                + ".Where(row => (row.Item2.Item2 != row.Item1.Item2))"
                + ".Select(row => (row.Item2.Item2))"
                + ".Select(row => new ResultRow([tag], new[] { (object)row.Item1 }))",
            TreeText.Of(inner.Expression)
        );
        Assert.Equal(
            "[items].Select(i => (i[id]!, i[name]!))"
                + ".GroupJoin([tags].Select(g => (g[item_id], g[tag])), "
                + "left => (left.Item1), g => (g.Item1), "
                + "(left, matches) => (left, matches))"
                + ".SelectMany(match => match.Item2"
                + ".Where(g => (g.Item2 != match.Item1.Item2)).DefaultIfEmpty(), "
                + "(match, g) => (match.Item1, g))"
                + ".Select(row => (row.Item2.Item2))"
                + ".Select(row => new ResultRow([tag], new[] { (object)row.Item1 }))",
            TreeText.Of(left.Expression)
        );
        Assert.Equal(["office"], ResultColumn.Texts(inner, "tag"));
        Assert.Equal(["", "", "office"], ResultColumn.Texts(left, "tag"));
    }

    [Fact]
    public void JoinKeyOfAnIntegerAndADecimalCompareInDecimal()
    {
        PureQLProjection projection = new(
            [Items, Tags],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Inner,
                        "s.tags",
                        new EqualDecimalRow(
                            new FieldDecimalNullable("i", "price"),
                            new FieldInteger("g", "item_id")
                        ),
                        "g"
                    ),
                ],
                select: [new SelectItemProjection("tag", new FieldString("g", "tag"))]
            )
        );

        Assert.Equal(
            "[items].Select(i => (i[price]))"
                + ".Join([tags].Select(g => (g[item_id]!, g[tag]!)), "
                + "left => (left.Item1), g => ((decimal?)g.Item1), "
                + "(left, g) => (left, g))"
                + ".Select(row => (row.Item2.Item2))"
                + ".Select(row => new ResultRow([tag], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Equal(["spare"], ResultColumn.Texts(projection, "tag"));
    }

    [Fact]
    public void ConstantStringsCompareByCodePointWhenFolded()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithPagination(
                from: new FromEntity("t.items", "i"),
                select:
                [
                    new SelectItemProjection(
                        "above",
                        new GreaterThanStringProjection(
                            new LiteralString("\U0001F600"),
                            new LiteralString("\uE000")
                        )
                    ),
                ],
                pagination: new Pagination(take: 1)
            )
        );

        Assert.Equal(
            "[items].Select(i => ())"
                + ".Select(row => (true)).Take(1)"
                + ".Select(row => new ResultRow([above], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Equal(["True"], ResultColumn.Texts(projection, "above"));
    }

    [Fact]
    public void SubquerySourceIsReadThroughAndTakesTheOuterCondition()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "active_items",
                        new PlainQueryWithoutPaginationWithoutOrderBy(
                            from: new FromEntity("t.items", "i"),
                            where: new FieldBoolean("i", "active"),
                            select:
                            [
                                new SelectItemProjection(
                                    "id",
                                    new FieldInteger("i", "id")
                                ),
                                new SelectItemProjection(
                                    "name",
                                    new FieldString("i", "name")
                                ),
                            ]
                        )
                    ),
                ],
                from: new FromSubquery("active_items", "a"),
                where: new GreaterThanDecimalRow(
                    new FieldInteger("a", "id"),
                    new LiteralInteger(1)
                ),
                select: [new SelectItemProjection("name", new FieldString("a", "name"))]
            )
        );

        Assert.Equal(
            "[items].Select(i => (i[id]!, i[name]!, i[active]!))"
                + ".Where(i => ((i.Item3 == true) && (i.Item1 > 1L)))"
                + ".Select(row => (row.Item2))"
                + ".Select(row => new ResultRow([name], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Equal(["pad"], ResultColumn.Texts(projection, "name"));
    }

    [Fact]
    public void ConditionOnAGroupedSubqueryBecomesItsHaving()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "states",
                        new GroupedQueryWithoutPaginationWithoutOrderBy(
                            from: new FromEntity("t.items", "i"),
                            groupBy:
                            [
                                new GroupKey("active", new FieldBoolean("i", "active")),
                            ],
                            select:
                            [
                                new SelectItemGroup("active", new KeyBoolean("active")),
                                new SelectItemGroup("items", new CountGroup()),
                            ]
                        )
                    ),
                ],
                from: new FromSubquery("states", "s"),
                where: new GreaterThanDecimalRow(
                    new FieldInteger("s", "items"),
                    new LiteralInteger(1)
                ),
                select:
                [
                    new SelectItemProjection("active", new FieldBoolean("s", "active")),
                ]
            )
        );

        Assert.Equal(
            "[items].Select(i => (i[active]!)).GroupBy(row => (row.Item1))"
                + ".Where(group => (group.LongCount() > 1L))"
                + ".Select(group => (group.Key.Item1))"
                + ".Select(row => new ResultRow([active], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Equal(["True"], ResultColumn.Texts(projection, "active"));
    }

    [Fact]
    public void RepeatedAndConstantOrderKeysAreDroppedAndStringsOrderOrdinally()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                select: [new SelectItemProjection("id", new FieldInteger("i", "id"))],
                orderBy:
                [
                    new OrderItemProjectionExpression(new FieldString("i", "name")),
                    new OrderItemProjectionExpression(new LiteralInteger(7)),
                    new OrderItemProjectionExpression(new CountProjection()),
                    new OrderItemProjectionExpression(
                        new FieldString("i", "name"),
                        SortDirection.Desc
                    ),
                    new OrderItemProjectionExpression(
                        new FieldInteger("i", "id"),
                        SortDirection.Desc
                    ),
                ]
            )
        );

        Assert.Equal(
            "[items].Select(i => (i[id]!, i[name]!))"
                + ".OrderBy(row => row.Item2, Ordinal).ThenByDescending(row => row.Item1)"
                + ".Select(row => (row.Item1))"
                + ".Select(row => new ResultRow([id], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Equal(["2", "3", "1"], ResultColumn.Texts(projection, "id"));
    }

    [Fact]
    public void SubqueryReadForMembershipIsNeitherOrderedNorDistinct()
    {
        PureQLProjection projection = new(
            [Items, Tags],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "names",
                        new PlainQueryWithoutPaginationWithOrderBy(
                            from: new FromEntity("t.items", "i"),
                            select:
                            [
                                new SelectItemProjection(
                                    "name",
                                    new FieldString("i", "name")
                                ),
                            ],
                            distinctBy: ["name"],
                            orderBy:
                            [
                                new OrderItemProjectionExpression(
                                    new FieldDecimal("i", "price")
                                ),
                            ]
                        )
                    ),
                ],
                from: new FromEntity("s.tags", "g"),
                where: new InStringRow(
                    new FieldString("g", "tag"),
                    new ListSubqueryColumnString("names", "name", false)
                ),
                select: [new SelectItemProjection("tag", new FieldString("g", "tag"))]
            )
        );

        Assert.Equal(
            "[tags].Select(g => (g[tag]!))"
                + ".Where(g => [items].Select(i => (i[name]!))"
                + ".Select(names => names.Item1)"
                + ".Contains(g.Item1))"
                + ".Select(row => new ResultRow([tag], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Equal(["pen"], ResultColumn.Texts(projection, "tag"));
    }

    [Fact]
    public void PagedSubqueryReadForMembershipKeepsItsOrderAndPage()
    {
        PureQLProjection projection = new(
            [Items, Tags],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "first_name",
                        new PlainQueryWithPagination(
                            from: new FromEntity("t.items", "i"),
                            select:
                            [
                                new SelectItemProjection(
                                    "name",
                                    new FieldString("i", "name")
                                ),
                            ],
                            orderBy:
                            [
                                new OrderItemProjectionExpression(
                                    new FieldInteger("i", "id")
                                ),
                            ],
                            pagination: new Pagination(take: 1)
                        )
                    ),
                ],
                from: new FromEntity("s.tags", "g"),
                where: new InStringRow(
                    new FieldString("g", "tag"),
                    new ListSubqueryColumnString("first_name", "name", false)
                ),
                select: [new SelectItemProjection("tag", new FieldString("g", "tag"))]
            )
        );

        Assert.Equal(
            "[tags].Select(g => (g[tag]!))"
                + ".Where(g => [items].Select(i => (i[id]!, i[name]!))"
                + ".OrderBy(row => row.Item1).Select(row => row.Item2)"
                + ".Take(1).Contains(g.Item1))"
                + ".Select(row => new ResultRow([tag], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Equal(["pen"], ResultColumn.Texts(projection, "tag"));
    }

    [Fact]
    public void DistinctByTheGroupKeysIsDroppedAndPaginationEndsTheTree()
    {
        PureQLProjection projection = new(
            [Items],
            new MainGroupedQueryWithPagination(
                from: new FromEntity("t.items", "i"),
                groupBy: [new GroupKey("active", new FieldBoolean("i", "active"))],
                select: [new SelectItemGroup("active", new KeyBoolean("active"))],
                distinctBy: ["active", "active"],
                orderBy: [new OrderItemGroupExpression(new KeyBoolean("active"))],
                pagination: new Pagination(take: 5)
            )
        );

        Assert.Equal(
            "[items].Select(i => (i[active]!)).GroupBy(row => (row.Item1))"
                + ".OrderBy(group => group.Key.Item1)"
                + ".Select(group => (group.Key.Item1)).Take(5)"
                + ".Select(row => new ResultRow([active], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Equal(["False", "True"], ResultColumn.Texts(projection, "active"));
    }

    [Fact]
    public void DistinctByKeepsTheTypedRowsAndTheResultRowsArePaged()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithPagination(
                from: new FromEntity("t.items", "i"),
                select:
                [
                    new SelectItemProjection("name", new FieldString("i", "name")),
                    new SelectItemProjection("active", new FieldBoolean("i", "active")),
                ],
                distinctBy: ["active"],
                pagination: new Pagination(skip: 1, take: 5)
            )
        );

        Assert.Equal(
            "[items].Select(i => (i[name]!, i[active]!)).DistinctBy(row => (row.Item2))"
                + ".Skip(1).Take(5)"
                + ".Select(row => new ResultRow([name, active], "
                + "new[] { (object)row.Item1, (object)row.Item2 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Equal(["ink"], ResultColumn.Texts(projection, "name"));
    }

    [Fact]
    public void ParameterIsACapturedVariable()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                where: new GreaterThanDecimalRow(
                    new FieldInteger("i", "id"),
                    new ParamInteger("min_id")
                ),
                select: [new SelectItemProjection("id", new FieldInteger("i", "id"))]
            ),
            new Dictionary<string, IParameter>
            {
                ["min_id"] = new IntegerParameter(new Long(0)),
            }
        );

        Assert.Equal(
            "[items].Select(i => (i[id]!)).Where(i => (i.Item1 > @min_id))"
                + ".Select(row => new ResultRow([id], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
        MemberExpression parameter = Assert.Single(
            Nodes(projection.Expression).OfType<MemberExpression>(),
            member => member.Expression is ConstantExpression { Value: not null }
        );
        Assert.Equal(typeof(long?), parameter.Type);
    }

    [Fact]
    public void AggregatesOverAllRowsReadOneSharedSubtreeOfTheRows()
    {
        PureQLProjection projection = new(
            [Items],
            new MainTotalQuery(
                from: new FromEntity("t.items", "i"),
                where: new FieldBoolean("i", "active"),
                aggregate:
                [
                    new SelectItemTotal("items", new CountTotal()),
                    new SelectItemTotal(
                        "total",
                        new SumDecimalTotal(new FieldDecimalNullable("i", "price"))
                    ),
                ]
            )
        );

        Assert.Equal(
            "[one row]"
                + ".Select(one => ((long?)[items].Select(i => (i[price], i[active]!))"
                + ".Where(i => (i.Item2 == true)).LongCount(), "
                + "[items].Select(i => (i[price], i[active]!))"
                + ".Where(i => (i.Item2 == true)).Sum(row => row.Item1)))"
                + ".Select(row => new ResultRow([items, total], "
                + "new[] { (object)row.Item1, (object)row.Item2 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Same(
            Aggregated(projection.Expression, nameof(Queryable.LongCount)),
            Aggregated(projection.Expression, nameof(Queryable.Sum))
        );
        Assert.Equal(["2"], ResultColumn.Texts(projection, "items"));
        Assert.Equal(["5.5"], ResultColumn.Texts(projection, "total"));
    }

    [Fact]
    public void ValueThatCannotBeNullIsComparedWithoutLifting()
    {
        PureQLProjection projection = new(
            [Items],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                groupBy: [new GroupKey("active", new FieldBoolean("i", "active"))],
                having: new GreaterThanOrEqualDecimalGroup(
                    new CountGroup(),
                    new LiteralInteger(2)
                ),
                select:
                [
                    new SelectItemGroup(
                        "next",
                        new AddIntegerGroup([new CountGroup(), new LiteralInteger(1)])
                    ),
                ]
            )
        );

        Assert.Equal(
            "[items].Select(i => (i[active]!)).GroupBy(row => (row.Item1))"
                + ".Where(group => (group.LongCount() >= 2L))"
                + ".Select(group => ((long?)(group.LongCount() + 1L)))"
                + ".Select(row => new ResultRow([next], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Equal(["3"], ResultColumn.Texts(projection, "next"));
    }

    [Fact]
    public void LiftedOperationOverANullLiteralIsNull()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                select:
                [
                    new SelectItemProjection(
                        "nothing",
                        new SubtractDecimalNullableProjection([
                            new FieldDecimalNullable("i", "price"),
                            new LiteralDecimalNullable(),
                        ])
                    ),
                ]
            )
        );

        Assert.Equal(
            "[items].Select(i => (i[price]))"
                + ".Select(row => (null))"
                + ".Select(row => new ResultRow([nothing], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
    }

    [Fact]
    public void InMemoryRunReadsEachStoredTableOnce()
    {
        CountedTable items = new(Items.Values.Single());
        CountedTable tags = new(Tags.Values.Single());

        PureQLProjection projection = new(
            [
                new StoredSchemaDataset(
                    Items.Schema,
                    new Dictionary<ITable, IStoredTableDataSet>(
                        ReferenceEqualityComparer.Instance
                    )
                    {
                        [items.TableSchema] = items,
                    }
                ),
                new StoredSchemaDataset(
                    Tags.Schema,
                    new Dictionary<ITable, IStoredTableDataSet>(
                        ReferenceEqualityComparer.Instance
                    )
                    {
                        [tags.TableSchema] = tags,
                    }
                ),
            ],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Inner,
                        "s.tags",
                        new EqualDecimalRow(
                            new FieldInteger("g", "item_id"),
                            new FieldInteger("i", "id")
                        ),
                        "g"
                    ),
                ],
                select:
                [
                    new SelectItemProjection("tag", new FieldString("g", "tag")),
                    new SelectItemProjection("pairs", new CountProjection()),
                ]
            )
        );

        Assert.Equal(["pen", "office"], ResultColumn.Texts(projection, "tag"));
        Assert.Equal(1, items.Reads);
        Assert.Equal(1, tags.Reads);
    }

    private static List<IStoredTableDataSet> Roots(Expression expression)
    {
        return
        [
            .. Nodes(expression)
                .OfType<ConstantExpression>()
                .Select(constant => constant.Value)
                .OfType<IStoredTableDataSet>(),
        ];
    }

    private static Expression Aggregated(Expression expression, string aggregate)
    {
        return Nodes(expression)
            .OfType<MethodCallExpression>()
            .Single(call => call.Method.Name == aggregate)
            .Arguments[0];
    }

    private static List<Expression> Nodes(Expression expression)
    {
        Collector collector = new();
        _ = collector.Visit(expression);
        return collector.Nodes;
    }

    private sealed class Collector : ExpressionVisitor
    {
        public List<Expression> Nodes { get; } = [];

        public override Expression? Visit(Expression? node)
        {
            if (node is not null)
            {
                Nodes.Add(node);
            }

            return base.Visit(node);
        }
    }
}
