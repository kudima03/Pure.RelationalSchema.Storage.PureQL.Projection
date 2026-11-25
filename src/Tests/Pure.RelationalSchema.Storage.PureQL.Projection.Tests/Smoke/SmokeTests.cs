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

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Smoke;

public sealed class SmokeTests(Xunit.Abstractions.ITestOutputHelper output)
{
    [Fact]
    public void Runs()
    {
        IStoredSchemaDataSet numbers = new InlineTable(
            "t",
            "numbers",
            ("id", new LongColumnType()),
            ("g", new StringColumnType()),
            ("n", new LongColumnType()),
            ("d", new DoubleColumnType()),
            ("parent", new LongColumnType())
        ).With(
            ["1", "a", "7", "2.5", ""],
            ["2", "a", "-7", "-2.5", "1"],
            ["3", "b", "5", "1.5", "2"],
            ["4", "b", "5", "", "3"]
        );

        void Run(string name, PureQLQuery query)
        {
            try
            {
                PureQLProjection projection = new([numbers], query);
                output.WriteLine(
                    $"{name}: "
                        + string.Join(
                            " | ",
                            projection
                                .AsEnumerable()
                                .Select(row =>
                                    string.Join(
                                        ",",
                                        projection.TableSchema.Columns.Select(c =>
                                            row.Cells[c].Value.TextValue
                                        )
                                    )
                                )
                        )
                );
            }
            catch (Exception e)
            {
                output.WriteLine(
                    $"{name}: FAILED {e.GetType().Name}: {e.Message}\n{e.StackTrace?.Split('\n').FirstOrDefault()}"
                );
            }
        }

        FieldInteger n = new("t.numbers", "n");
        FieldInteger id = new("t.numbers", "id");
        FieldString g = new("t.numbers", "g");
        FieldDecimalNullable d = new("t.numbers", "d");

        Run(
            "plain",
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.numbers"),
                where: new GreaterThanDecimalRow(n, new LiteralInteger(0)),
                select:
                [
                    new SelectItemProjection("n", n),
                    new SelectItemProjection(
                        "sum",
                        new AddDecimalNullableProjection([n, d, new LiteralDecimal(0.1m)])
                    ),
                    new SelectItemProjection(
                        "q",
                        new DivideDecimalProjection([
                            new LiteralInteger(2),
                            new LiteralInteger(3),
                            new LiteralInteger(2),
                        ])
                    ),
                ],
                orderBy:
                [
                    new OrderItemProjectionColumn("n", SortDirection.Desc),
                    new OrderItemProjectionExpression(id),
                ]
            )
        );

        Run(
            "grouped",
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.numbers"),
                groupBy: [new GroupKey("g", g)],
                select:
                [
                    new SelectItemGroup("g", new KeyString("g")),
                    new SelectItemGroup("c", new CountGroup()),
                    new SelectItemGroup("s", new SumDecimalGroup(d)),
                    new SelectItemGroup("cd", new CountDistinctGroup(n)),
                    new SelectItemGroup(
                        "first",
                        new FirstIntegerGroup(
                            n,
                            [new OrderItemRow(id, SortDirection.Desc)]
                        )
                    ),
                    new SelectItemGroup(
                        "all",
                        new ConcatAllGroup(
                            new AsNullableStringNullableRow(g),
                            new LiteralString("+"),
                            [new OrderItemRow(id)]
                        )
                    ),
                ],
                orderBy: [new OrderItemGroupColumn("g")]
            )
        );

        Run(
            "total",
            new MainTotalQuery(
                from: new FromEntity("t.numbers"),
                aggregate:
                [
                    new SelectItemTotal("c", new CountTotal()),
                    new SelectItemTotal("max", new MaxIntegerNullableTotal(n)),
                ]
            )
        );

        OrderedWindow byId = new([new OrderItemRow(id)], [g]);
        Run(
            "window",
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.numbers"),
                select:
                [
                    new SelectItemProjection("id", id),
                    new SelectItemProjection("rn", new RowNumberProjection(byId)),
                    new SelectItemProjection(
                        "running",
                        new SumIntegerProjection(
                            n,
                            over: new WindowWithFrame(
                                [new OrderItemRow(id)],
                                new Frame(preceding: new All())
                            )
                        )
                    ),
                    new SelectItemProjection(
                        "prev",
                        new PreviousIntegerNullableProjection(n, byId)
                    ),
                    new SelectItemProjection(
                        "part",
                        new CountProjection(over: new WindowWithoutFrameWithoutRange([g]))
                    ),
                    new SelectItemProjection(
                        "range",
                        new SumIntegerProjection(
                            n,
                            over: new WindowWithoutFrameWithRangeInteger(
                                [new OrderItemIntegerRow(n)],
                                new RangeInteger(preceding: 2L)
                            )
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(id)]
            )
        );

        Run(
            "union",
            new MainCombinedQueryUnion(
                union:
                [
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromEntity("t.numbers"),
                        select: [new SelectItemProjection("x", n)]
                    ),
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromEntity("t.numbers"),
                        select: [new SelectItemProjection("x", d)]
                    ),
                ]
            )
        );

        Run(
            "rows",
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRows(
                        "s",
                        [
                            [
                                new RowItem("k", new LiteralString("a")),
                                new RowItem("v", new LiteralInteger(1)),
                            ],
                            [
                                new RowItem("v", new LiteralDecimal(2.5m)),
                                new RowItem("k", new LiteralStringNullable()),
                            ],
                        ]
                    ),
                ],
                from: new FromSubquery("s"),
                select:
                [
                    new SelectItemProjection("k", new FieldStringNullable("s", "k")),
                    new SelectItemProjection("v", new FieldDecimal("s", "v")),
                ]
            )
        );

        Run(
            "recursive",
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryRecursive(
                        "tree",
                        new Recursion(
                            seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.numbers"),
                                where: new EqualDecimalRow(id, new LiteralInteger(1)),
                                select:
                                [
                                    new SelectItemProjection("id", id),
                                    new SelectItemProjection(
                                        "depth",
                                        new LiteralInteger(0)
                                    ),
                                ]
                            ),
                            next: new PlainNextQuery(
                                from: new FromEntity("t.numbers"),
                                joins:
                                [
                                    new JoinSubquery(
                                        JoinKind.Inner,
                                        "tree",
                                        new EqualDecimalRow(
                                            new FieldIntegerNullable(
                                                "t.numbers",
                                                "parent"
                                            ),
                                            new FieldInteger("tree", "id")
                                        )
                                    ),
                                ],
                                select:
                                [
                                    new SelectItemRow("id", id),
                                    new SelectItemRow(
                                        "depth",
                                        new AddIntegerRow([
                                            new FieldInteger("tree", "depth"),
                                            new LiteralInteger(1),
                                        ])
                                    ),
                                ]
                            ),
                            maxDepth: 10L
                        )
                    ),
                ],
                from: new FromSubquery("tree"),
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("tree", "id")),
                    new SelectItemProjection("depth", new FieldInteger("tree", "depth")),
                ],
                orderBy: [new OrderItemProjectionColumn("depth")]
            )
        );
    }
}
