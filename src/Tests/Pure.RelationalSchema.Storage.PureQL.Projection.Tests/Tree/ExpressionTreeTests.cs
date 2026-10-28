using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.GroupExpressions;
using PureQL.CSharp.Model.GroupKeys;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.SelectItems;
using PureQL.CSharp.Model.TotalExpressions;

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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionString(
                                    "name",
                                    new StringProjection(new FieldString("i", "name"))
                                )
                            )
                        ),
                    ]
                )
            )
        );

        Assert.Equal(
            "[items].Select(i => (i[name]!))"
                + ".Select(row => new ResultRow([name], new[] { (object)row.Item1 }))",
            TreeText.Of(projection.Expression)
        );
        Assert.Same(
            Items.Values.Single(),
            Roots(projection.Expression).Single()
        );
    }

    [Fact]
    public void StoredCellDeclaredNullableIsReadAsItsParsedTextWithEmptyTextAsNull()
    {
        PureQLProjection projection = new(
            [Items],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionIntegerNullable(
                                    "id",
                                    new ExactIntegerNullableProjection(
                                        new FieldIntegerNullable("i", "id")
                                    )
                                )
                            )
                        ),
                    ]
                )
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "id",
                                    new IntegerProjection(new FieldInteger("i", "id"))
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionString(
                                    "name",
                                    new StringProjection(new FieldString("i", "name"))
                                )
                            )
                        ),
                    ]
                )
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "id",
                                    new IntegerProjection(new FieldInteger("i", "id"))
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins: null,
                    where: new BooleanRow(
                        new ComparisonRow(
                            new GreaterThanRow(
                                new GreaterThanDecimalRow(
                                    new DecimalNullableRow(
                                        new ExactIntegerNullableRow(
                                            new FieldIntegerNullable("i", "id")
                                        )
                                    ),
                                    new DecimalNullableRow(
                                        new IntegerRow(new LiteralInteger(1))
                                    )
                                )
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionStringNullable(
                                    "tag",
                                    new ExactStringNullableProjection(
                                        new FieldStringNullable("g", "tag")
                                    )
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionIntegerNullable(
                                    "item",
                                    new ExactIntegerNullableProjection(
                                        new FieldIntegerNullable("g", "item_id")
                                    )
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins:
                    [
                        new Join(
                            new JoinEntity(
                                JoinKind.Left,
                                "s.tags",
                                new BooleanRow(
                                    new ComparisonRow(
                                        new EqualRow(
                                            new EqualDecimalRow(
                                                new DecimalNullableRow(
                                                    new IntegerRow(
                                                        new FieldInteger("g", "item_id")
                                                    )
                                                ),
                                                new DecimalNullableRow(
                                                    new IntegerRow(
                                                        new FieldInteger("i", "id")
                                                    )
                                                )
                                            )
                                        )
                                    )
                                ),
                                "g"
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionString(
                                    "tag",
                                    new StringProjection(new FieldString("g", "tag"))
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins:
                    [
                        new Join(
                            new JoinEntity(
                                JoinKind.Inner,
                                "s.tags",
                                new BooleanRow(
                                    new ComparisonRow(
                                        new EqualRow(
                                            new EqualDecimalRow(
                                                new DecimalNullableRow(
                                                    new IntegerRow(
                                                        new FieldInteger("g", "item_id")
                                                    )
                                                ),
                                                new DecimalNullableRow(
                                                    new IntegerRow(
                                                        new FieldInteger("i", "id")
                                                    )
                                                )
                                            )
                                        )
                                    )
                                ),
                                "g"
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionString(
                                    "label",
                                    new StringProjection(
                                        new ConcatStringProjection(
                                            [
                                                new StringProjection(new LiteralString("a")),
                                                new StringProjection(new LiteralString("b")),
                                            ]
                                        )
                                    )
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
                                    new BooleanRow(new LiteralBoolean(true)),
                                    new BooleanRow(
                                        new ComparisonRow(
                                            new GreaterThanRow(
                                                new GreaterThanDecimalRow(
                                                    new DecimalNullableRow(
                                                        new IntegerRow(
                                                            new FieldInteger("i", "id")
                                                        )
                                                    ),
                                                    new DecimalNullableRow(
                                                        new IntegerRow(
                                                            new ArithmeticIntegerRow(
                                                                new AddIntegerRow(
                                                                    [
                                                                        new IntegerRow(
                                                                            new LiteralInteger(1)
                                                                        ),
                                                                        new IntegerRow(
                                                                            new LiteralInteger(1)
                                                                        ),
                                                                    ]
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
                    ),
                    orderBy: null,
                    pagination: null,
                    distinctBy: null
                )
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionDecimal(
                                    "ratio",
                                    new ExactDecimalProjection(
                                        new ArithmeticDecimalProjection(
                                            new DivideDecimalProjection(
                                                [
                                                    new DecimalProjection(
                                                        new ExactDecimalProjection(
                                                            new LiteralDecimal(1)
                                                        )
                                                    ),
                                                    new DecimalProjection(
                                                        new ExactDecimalProjection(
                                                            new LiteralDecimal(0)
                                                        )
                                                    ),
                                                ]
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionString(
                                    "name",
                                    new StringProjection(new FieldString("i", "name"))
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins:
                    [
                        new Join(
                            new JoinEntity(
                                JoinKind.Inner,
                                "s.tags",
                                new BooleanRow(
                                    new ComparisonRow(
                                        new EqualRow(
                                            new EqualDecimalRow(
                                                new DecimalNullableRow(
                                                    new IntegerRow(
                                                        new FieldInteger("g", "item_id")
                                                    )
                                                ),
                                                new DecimalNullableRow(
                                                    new IntegerRow(
                                                        new FieldInteger("i", "id")
                                                    )
                                                )
                                            )
                                        )
                                    )
                                ),
                                "g"
                            )
                        ),
                    ],
                    where: new BooleanRow(
                        new LogicalRow(
                            new AndRow(
                                [
                                    new BooleanRow(new FieldBoolean("i", "active")),
                                    new BooleanRow(
                                        new ComparisonRow(
                                            new EqualRow(
                                                new EqualStringRow(
                                                    new StringNullableRow(
                                                        new StringRow(
                                                            new FieldString("g", "tag")
                                                        )
                                                    ),
                                                    new StringNullableRow(
                                                        new StringRow(
                                                            new FieldString("i", "name")
                                                        )
                                                    )
                                                )
                                            )
                                        )
                                    ),
                                    new BooleanRow(
                                        new ComparisonRow(
                                            new NotEqualRow(
                                                new NotEqualStringRow(
                                                    new StringNullableRow(
                                                        new StringRow(
                                                            new FieldString("g", "tag")
                                                        )
                                                    ),
                                                    new StringNullableRow(
                                                        new StringRow(
                                                            new LiteralString("spare")
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionString(
                                    "name",
                                    new StringProjection(new FieldString("i", "name"))
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins:
                    [
                        new Join(
                            new JoinEntity(
                                JoinKind.Left,
                                "s.tags",
                                new BooleanRow(
                                    new ComparisonRow(
                                        new EqualRow(
                                            new EqualDecimalRow(
                                                new DecimalNullableRow(
                                                    new ExactIntegerNullableRow(
                                                        new FieldIntegerNullable(
                                                            "g",
                                                            "item_id"
                                                        )
                                                    )
                                                ),
                                                new DecimalNullableRow(
                                                    new IntegerRow(
                                                        new FieldInteger("i", "id")
                                                    )
                                                )
                                            )
                                        )
                                    )
                                ),
                                "g"
                            )
                        ),
                    ],
                    where: new BooleanRow(
                        new LogicalRow(
                            new AndRow(
                                [
                                    new BooleanRow(new FieldBoolean("i", "active")),
                                    new BooleanRow(
                                        new ComparisonRow(
                                            new EqualRow(
                                                new EqualStringRow(
                                                    new StringNullableRow(
                                                        new ExactStringNullableRow(
                                                            new FieldStringNullable(
                                                                "g",
                                                                "tag"
                                                            )
                                                        )
                                                    ),
                                                    new StringNullableRow(
                                                        new ExactStringNullableRow(
                                                            new LiteralStringNullable()
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionString(
                                    "tag",
                                    new StringProjection(new FieldString("g", "tag"))
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionStringNullable(
                                    "name",
                                    new ExactStringNullableProjection(
                                        new FieldStringNullable("i", "name")
                                    )
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins:
                    [
                        new Join(
                            new JoinEntity(
                                JoinKind.Right,
                                "s.tags",
                                new BooleanRow(
                                    new ComparisonRow(
                                        new EqualRow(
                                            new EqualDecimalRow(
                                                new DecimalNullableRow(
                                                    new IntegerRow(
                                                        new FieldInteger("g", "item_id")
                                                    )
                                                ),
                                                new DecimalNullableRow(
                                                    new ExactIntegerNullableRow(
                                                        new FieldIntegerNullable("i", "id")
                                                    )
                                                )
                                            )
                                        )
                                    )
                                ),
                                "g"
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionStringNullable(
                                    "name",
                                    new ExactStringNullableProjection(
                                        new FieldStringNullable("i", "name")
                                    )
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionStringNullable(
                                    "tag",
                                    new ExactStringNullableProjection(
                                        new FieldStringNullable("g", "tag")
                                    )
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins:
                    [
                        new Join(
                            new JoinEntity(
                                JoinKind.Full,
                                "s.tags",
                                new BooleanRow(
                                    new ComparisonRow(
                                        new EqualRow(
                                            new EqualDecimalRow(
                                                new DecimalNullableRow(
                                                    new ExactIntegerNullableRow(
                                                        new FieldIntegerNullable(
                                                            "g",
                                                            "item_id"
                                                        )
                                                    )
                                                ),
                                                new DecimalNullableRow(
                                                    new ExactIntegerNullableRow(
                                                        new FieldIntegerNullable("i", "id")
                                                    )
                                                )
                                            )
                                        )
                                    )
                                ),
                                "g"
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionStringNullable(
                                    "tag",
                                    new ExactStringNullableProjection(
                                        new FieldStringNullable("g", "tag")
                                    )
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins:
                    [
                        new Join(
                            new JoinEntity(
                                JoinKind.Left,
                                "s.tags",
                                new BooleanRow(
                                    new ComparisonRow(
                                        new GreaterThanRow(
                                            new GreaterThanDecimalRow(
                                                new DecimalNullableRow(
                                                    new ExactIntegerNullableRow(
                                                        new FieldIntegerNullable(
                                                            "g",
                                                            "item_id"
                                                        )
                                                    )
                                                ),
                                                new DecimalNullableRow(
                                                    new IntegerRow(
                                                        new FieldInteger("i", "id")
                                                    )
                                                )
                                            )
                                        )
                                    )
                                ),
                                "g"
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
            return new Join(
                new JoinEntity(
                    kind,
                    "s.tags",
                    new BooleanRow(
                        new LogicalRow(
                            new AndRow(
                                [
                                    new BooleanRow(
                                        new ComparisonRow(
                                            new EqualRow(
                                                new EqualDecimalRow(
                                                    new DecimalNullableRow(
                                                        new ExactIntegerNullableRow(
                                                            new FieldIntegerNullable(
                                                                "g",
                                                                "item_id"
                                                            )
                                                        )
                                                    ),
                                                    new DecimalNullableRow(
                                                        new IntegerRow(
                                                            new FieldInteger("i", "id")
                                                        )
                                                    )
                                                )
                                            )
                                        )
                                    ),
                                    new BooleanRow(
                                        new ComparisonRow(
                                            new NotEqualRow(
                                                new NotEqualStringRow(
                                                    new StringNullableRow(
                                                        new ExactStringNullableRow(
                                                            new FieldStringNullable(
                                                                "g",
                                                                "tag"
                                                            )
                                                        )
                                                    ),
                                                    new StringNullableRow(
                                                        new StringRow(
                                                            new FieldString("i", "name")
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
                    "g"
                )
            );
        }

        PureQLQuery query(JoinKind kind)
        {
            return new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionStringNullable(
                                    "tag",
                                    new ExactStringNullableProjection(
                                        new FieldStringNullable("g", "tag")
                                    )
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins: [join(kind)],
                    where: null,
                    orderBy: null,
                    pagination: null,
                    distinctBy: null
                )
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionString(
                                    "tag",
                                    new StringProjection(new FieldString("g", "tag"))
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins:
                    [
                        new Join(
                            new JoinEntity(
                                JoinKind.Inner,
                                "s.tags",
                                new BooleanRow(
                                    new ComparisonRow(
                                        new EqualRow(
                                            new EqualDecimalRow(
                                                new DecimalNullableRow(
                                                    new ExactDecimalNullableRow(
                                                        new FieldDecimalNullable(
                                                            "i",
                                                            "price"
                                                        )
                                                    )
                                                ),
                                                new DecimalNullableRow(
                                                    new IntegerRow(
                                                        new FieldInteger("g", "item_id")
                                                    )
                                                )
                                            )
                                        )
                                    )
                                ),
                                "g"
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionBoolean(
                                    "above",
                                    new BooleanProjection(
                                        new ComparisonProjection(
                                            new GreaterThanProjection(
                                                new GreaterThanStringProjection(
                                                    new StringNullableProjection(
                                                        new StringProjection(
                                                            new LiteralString("\U0001F600")
                                                        )
                                                    ),
                                                    new StringNullableProjection(
                                                        new StringProjection(
                                                            new LiteralString("\uE000")
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
                    orderBy: null,
                    pagination: new Pagination(0, 1),
                    distinctBy: null
                )
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromSubquery("active_items", "a")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionString(
                                    "name",
                                    new StringProjection(new FieldString("a", "name"))
                                )
                            )
                        ),
                    ],
                    subqueries:
                    [
                        new Subquery(
                            "active_items",
                            new Query(
                                new PlainQuery(
                                    new From(new FromEntity("t.items", "i")),
                                    [
                                        new SelectItemProjection(
                                            new SelectItemProjectionNonNullable(
                                                new SelectItemProjectionInteger(
                                                    "id",
                                                    new IntegerProjection(
                                                        new FieldInteger("i", "id")
                                                    )
                                                )
                                            )
                                        ),
                                        new SelectItemProjection(
                                            new SelectItemProjectionNonNullable(
                                                new SelectItemProjectionString(
                                                    "name",
                                                    new StringProjection(
                                                        new FieldString("i", "name")
                                                    )
                                                )
                                            )
                                        ),
                                    ],
                                    joins: null,
                                    where: new BooleanRow(
                                        new FieldBoolean("i", "active")
                                    ),
                                    orderBy: null,
                                    pagination: null,
                                    distinctBy: null
                                )
                            )
                        ),
                    ],
                    joins: null,
                    where: new BooleanRow(
                        new ComparisonRow(
                            new GreaterThanRow(
                                new GreaterThanDecimalRow(
                                    new DecimalNullableRow(
                                        new IntegerRow(new FieldInteger("a", "id"))
                                    ),
                                    new DecimalNullableRow(
                                        new IntegerRow(new LiteralInteger(1))
                                    )
                                )
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromSubquery("states", "s")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionBoolean(
                                    "active",
                                    new BooleanProjection(new FieldBoolean("s", "active"))
                                )
                            )
                        ),
                    ],
                    subqueries:
                    [
                        new Subquery(
                            "states",
                            new Query(
                                new GroupedQuery(
                                    new From(new FromEntity("t.items", "i")),
                                    [
                                        new GroupKey(
                                            new GroupKeyNonNullable(
                                                new GroupKeyBoolean(
                                                    "active",
                                                    new BooleanRow(
                                                        new FieldBoolean("i", "active")
                                                    )
                                                )
                                            )
                                        ),
                                    ],
                                    [
                                        new SelectItemGroup(
                                            new SelectItemGroupNonNullable(
                                                new SelectItemGroupBoolean(
                                                    "active",
                                                    new BooleanGroup(
                                                        new KeyBoolean("active")
                                                    )
                                                )
                                            )
                                        ),
                                        new SelectItemGroup(
                                            new SelectItemGroupNonNullable(
                                                new SelectItemGroupInteger(
                                                    "items",
                                                    new IntegerGroup(
                                                        new AggregateIntegerGroup(
                                                            new CountGroup()
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
                    joins: null,
                    where: new BooleanRow(
                        new ComparisonRow(
                            new GreaterThanRow(
                                new GreaterThanDecimalRow(
                                    new DecimalNullableRow(
                                        new IntegerRow(new FieldInteger("s", "items"))
                                    ),
                                    new DecimalNullableRow(
                                        new IntegerRow(new LiteralInteger(1))
                                    )
                                )
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "id",
                                    new IntegerProjection(new FieldInteger("i", "id"))
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
                                    new StringProjection(new FieldString("i", "name"))
                                )
                            )
                        ),
                        new OrderItemProjection(
                            new ExprProjection(
                                new DecimalNullableProjection(
                                    new IntegerProjection(new LiteralInteger(7))
                                )
                            )
                        ),
                        new OrderItemProjection(
                            new ExprProjection(
                                new DecimalNullableProjection(
                                    new IntegerProjection(
                                        new AggregateIntegerProjection(new CountProjection())
                                    )
                                )
                            )
                        ),
                        new OrderItemProjection(
                            new ExprProjection(
                                new StringNullableProjection(
                                    new StringProjection(new FieldString("i", "name"))
                                )
                            ),
                            SortDirection.Desc
                        ),
                        new OrderItemProjection(
                            new ExprProjection(
                                new DecimalNullableProjection(
                                    new IntegerProjection(new FieldInteger("i", "id"))
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("s.tags", "g")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionString(
                                    "tag",
                                    new StringProjection(new FieldString("g", "tag"))
                                )
                            )
                        ),
                    ],
                    subqueries:
                    [
                        new Subquery(
                            "names",
                            new Query(
                                new PlainQuery(
                                    new From(new FromEntity("t.items", "i")),
                                    [
                                        new SelectItemProjection(
                                            new SelectItemProjectionNonNullable(
                                                new SelectItemProjectionString(
                                                    "name",
                                                    new StringProjection(
                                                        new FieldString("i", "name")
                                                    )
                                                )
                                            )
                                        ),
                                    ],
                                    joins: null,
                                    where: null,
                                    orderBy:
                                    [
                                        new OrderItemProjection(
                                            new ExprProjection(
                                                new DecimalNullableProjection(
                                                    new ExactDecimalProjection(
                                                        new FieldDecimal("i", "price")
                                                    )
                                                )
                                            )
                                        ),
                                    ],
                                    pagination: null,
                                    distinctBy: ["name"]
                                )
                            )
                        ),
                    ],
                    joins: null,
                    where: new BooleanRow(
                        new ComparisonRow(
                            new InRow(
                                new InStringRow(
                                    new StringNullableRow(
                                        new StringRow(new FieldString("g", "tag"))
                                    ),
                                    new ListString(
                                        new ListSubqueryColumnString("names", "name", false)
                                    )
                                )
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("s.tags", "g")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionString(
                                    "tag",
                                    new StringProjection(new FieldString("g", "tag"))
                                )
                            )
                        ),
                    ],
                    subqueries:
                    [
                        new Subquery(
                            "first_name",
                            new Query(
                                new PlainQuery(
                                    new From(new FromEntity("t.items", "i")),
                                    [
                                        new SelectItemProjection(
                                            new SelectItemProjectionNonNullable(
                                                new SelectItemProjectionString(
                                                    "name",
                                                    new StringProjection(
                                                        new FieldString("i", "name")
                                                    )
                                                )
                                            )
                                        ),
                                    ],
                                    joins: null,
                                    where: null,
                                    orderBy:
                                    [
                                        new OrderItemProjection(
                                            new ExprProjection(
                                                new DecimalNullableProjection(
                                                    new IntegerProjection(
                                                        new FieldInteger("i", "id")
                                                    )
                                                )
                                            )
                                        ),
                                    ],
                                    pagination: new Pagination(0, 1),
                                    distinctBy: null
                                )
                            )
                        ),
                    ],
                    joins: null,
                    where: new BooleanRow(
                        new ComparisonRow(
                            new InRow(
                                new InStringRow(
                                    new StringNullableRow(
                                        new StringRow(new FieldString("g", "tag"))
                                    ),
                                    new ListString(
                                        new ListSubqueryColumnString(
                                            "first_name",
                                            "name",
                                            false
                                        )
                                    )
                                )
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
            new PureQLQuery(
                new MainGroupedQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new GroupKey(
                            new GroupKeyNonNullable(
                                new GroupKeyBoolean(
                                    "active",
                                    new BooleanRow(new FieldBoolean("i", "active"))
                                )
                            )
                        ),
                    ],
                    [
                        new SelectItemGroup(
                            new SelectItemGroupNonNullable(
                                new SelectItemGroupBoolean(
                                    "active",
                                    new BooleanGroup(new KeyBoolean("active"))
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
                                new BooleanNullableGroup(
                                    new BooleanGroup(new KeyBoolean("active"))
                                )
                            )
                        ),
                    ],
                    pagination: new Pagination(0, 5),
                    distinctBy: ["active", "active"]
                )
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionString(
                                    "name",
                                    new StringProjection(new FieldString("i", "name"))
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionBoolean(
                                    "active",
                                    new BooleanProjection(new FieldBoolean("i", "active"))
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins: null,
                    where: null,
                    orderBy: null,
                    pagination: new Pagination(1, 5),
                    distinctBy: ["active"]
                )
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "id",
                                    new IntegerProjection(new FieldInteger("i", "id"))
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins: null,
                    where: new BooleanRow(
                        new ComparisonRow(
                            new GreaterThanRow(
                                new GreaterThanDecimalRow(
                                    new DecimalNullableRow(
                                        new IntegerRow(new FieldInteger("i", "id"))
                                    ),
                                    new DecimalNullableRow(
                                        new IntegerRow(new ParamInteger("min_id"))
                                    )
                                )
                            )
                        )
                    ),
                    orderBy: null,
                    pagination: null,
                    distinctBy: null
                )
            ),
            new Dictionary<string, ILiteral> { ["min_id"] = new LiteralInteger(0) }
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
            new PureQLQuery(
                new MainTotalQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemTotal(
                            new SelectItemTotalNonNullable(
                                new SelectItemTotalInteger(
                                    "items",
                                    new IntegerTotal(
                                        new AggregateIntegerTotal(new CountTotal())
                                    )
                                )
                            )
                        ),
                        new SelectItemTotal(
                            new SelectItemTotalNonNullable(
                                new SelectItemTotalDecimal(
                                    "total",
                                    new ExactDecimalTotal(
                                        new SumDecimalTotal(
                                            new DecimalNullableRow(
                                                new ExactDecimalNullableRow(
                                                    new FieldDecimalNullable("i", "price")
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
                    where: new BooleanRow(new FieldBoolean("i", "active"))
                )
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
            new PureQLQuery(
                new MainGroupedQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new GroupKey(
                            new GroupKeyNonNullable(
                                new GroupKeyBoolean(
                                    "active",
                                    new BooleanRow(new FieldBoolean("i", "active"))
                                )
                            )
                        ),
                    ],
                    [
                        new SelectItemGroup(
                            new SelectItemGroupNonNullable(
                                new SelectItemGroupInteger(
                                    "next",
                                    new IntegerGroup(
                                        new ArithmeticIntegerGroup(
                                            new AddIntegerGroup(
                                                [
                                                    new IntegerGroup(
                                                        new AggregateIntegerGroup(
                                                            new CountGroup()
                                                        )
                                                    ),
                                                    new IntegerGroup(new LiteralInteger(1)),
                                                ]
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
                    having: new BooleanGroup(
                        new ComparisonGroup(
                            new GreaterThanOrEqualGroup(
                                new GreaterThanOrEqualDecimalGroup(
                                    new DecimalNullableGroup(
                                        new IntegerGroup(
                                            new AggregateIntegerGroup(new CountGroup())
                                        )
                                    ),
                                    new DecimalNullableGroup(
                                        new IntegerGroup(new LiteralInteger(2))
                                    )
                                )
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNullable(
                                new SelectItemProjectionDecimalNullable(
                                    "nothing",
                                    new ExactDecimalNullableProjection(
                                        new ArithmeticDecimalNullableProjection(
                                            new SubtractDecimalNullableProjection(
                                                [
                                                    new DecimalNullableProjection(
                                                        new ExactDecimalNullableProjection(
                                                            new FieldDecimalNullable(
                                                                "i",
                                                                "price"
                                                            )
                                                        )
                                                    ),
                                                    new DecimalNullableProjection(
                                                        new ExactDecimalNullableProjection(
                                                            new LiteralDecimalNullable()
                                                        )
                                                    ),
                                                ]
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
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("t.items", "i")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionString(
                                    "tag",
                                    new StringProjection(new FieldString("g", "tag"))
                                )
                            )
                        ),
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "pairs",
                                    new IntegerProjection(
                                        new AggregateIntegerProjection(new CountProjection())
                                    )
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins:
                    [
                        new Join(
                            new JoinEntity(
                                JoinKind.Inner,
                                "s.tags",
                                new BooleanRow(
                                    new ComparisonRow(
                                        new EqualRow(
                                            new EqualDecimalRow(
                                                new DecimalNullableRow(
                                                    new IntegerRow(
                                                        new FieldInteger("g", "item_id")
                                                    )
                                                ),
                                                new DecimalNullableRow(
                                                    new IntegerRow(
                                                        new FieldInteger("i", "id")
                                                    )
                                                )
                                            )
                                        )
                                    )
                                ),
                                "g"
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
