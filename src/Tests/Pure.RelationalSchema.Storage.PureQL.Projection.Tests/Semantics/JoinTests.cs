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

// Every join kind over tables with null keys, repeated keys and rows no row
// of the other side matches, with a key equality alone, with a further
// condition, with a key read as integer on one side and decimal on the
// other, and with no equality at all. The expected rows are the
// specification's semantics computed by nested loops over the same rows:
// null equals null, an ordering comparison against null is false, and an
// unmatched outer side is all nulls. The result is in the loops' order: the
// rows so far in order, each with its matches in the joined source's order
// (the joined source first for right), then the unmatched joined rows.
public sealed class JoinTests
{
    private sealed record Order(long? Id, long? Customer, decimal? Amount);

    private sealed record Customer(long? Id, string? Name, string? Tier);

    private static readonly IReadOnlyList<Order> OrderRows =
    [
        new(10, 1, 5),
        new(11, 2, 7),
        new(12, null, 1),
        new(13, 9, 3),
        new(14, 2, 2),
    ];

    private static readonly IReadOnlyList<Customer> CustomerRows =
    [
        new(1, "ann", "gold"),
        new(2, "bob", null),
        new(null, "nil", "gold"),
        new(2, "bo2", "silver"),
        new(7, "zed", "gold"),
    ];

    private static readonly IStoredSchemaDataSet Orders = new InlineTable(
        "s",
        "orders",
        ("id", new LongColumnType()),
        ("customer_id", new LongColumnType()),
        ("amount", new DoubleColumnType())
    ).With([.. OrderRows.Select(order => Texts(order.Id, order.Customer, order.Amount))]);

    private static readonly IStoredSchemaDataSet Customers = new InlineTable(
        "s",
        "customers",
        ("id", new LongColumnType()),
        ("name", new StringColumnType()),
        ("tier", new StringColumnType())
    ).With(
        [
            .. CustomerRows.Select(customer =>
                Texts(customer.Id, customer.Name, customer.Tier)
            ),
        ]
    );

    public static TheoryData<JoinKind, string> Joins =>
        new()
        {
            { JoinKind.Inner, "key" },
            { JoinKind.Left, "key" },
            { JoinKind.Right, "key" },
            { JoinKind.Full, "key" },
            { JoinKind.Inner, "key reversed" },
            { JoinKind.Left, "key reversed" },
            { JoinKind.Right, "key reversed" },
            { JoinKind.Full, "key reversed" },
            { JoinKind.Inner, "key and condition" },
            { JoinKind.Left, "key and condition" },
            { JoinKind.Right, "key and condition" },
            { JoinKind.Full, "key and condition" },
            { JoinKind.Inner, "decimal key" },
            { JoinKind.Left, "decimal key" },
            { JoinKind.Right, "decimal key" },
            { JoinKind.Full, "decimal key" },
            { JoinKind.Inner, "no key" },
            { JoinKind.Left, "no key" },
            { JoinKind.Right, "no key" },
            { JoinKind.Full, "no key" },
        };

    [Theory]
    [MemberData(nameof(Joins))]
    public void JoinKeepsTheRowsOfNestedLoopsInTheirOrder(JoinKind kind, string on)
    {
        (BooleanRow condition, Func<Order, Customer, bool> holds) = on switch
        {
            "key" => Case(
                Equal(Integer("o", "customer_id"), Integer("c", "id")),
                (order, customer) => order.Customer == customer.Id
            ),
            "key reversed" => Case(
                Equal(Integer("c", "id"), Integer("o", "customer_id")),
                (order, customer) => order.Customer == customer.Id
            ),
            "key and condition" => Case(
                new BooleanRow(
                    new LogicalRow(
                        new AndRow(
                            [
                                Equal(Integer("o", "customer_id"), Integer("c", "id")),
                                new BooleanRow(
                                    new ComparisonRow(
                                        new EqualRow(
                                            new EqualStringRow(
                                                new StringNullableRow(
                                                    new ExactStringNullableRow(
                                                        new FieldStringNullable("c", "tier")
                                                    )
                                                ),
                                                new StringNullableRow(
                                                    new StringRow(new LiteralString("gold"))
                                                )
                                            )
                                        )
                                    )
                                ),
                            ]
                        )
                    )
                ),
                (order, customer) =>
                    order.Customer == customer.Id && customer.Tier == "gold"
            ),
            "decimal key" => Case(
                Equal(
                    new DecimalNullableRow(
                        new ExactDecimalNullableRow(new FieldDecimalNullable("o", "amount"))
                    ),
                    Integer("c", "id")
                ),
                (order, customer) => order.Amount == customer.Id
            ),
            "no key" => Case(
                new BooleanRow(
                    new ComparisonRow(
                        new GreaterThanOrEqualRow(
                            new GreaterThanOrEqualDecimalRow(
                                new DecimalNullableRow(
                                    new ExactDecimalNullableRow(
                                        new FieldDecimalNullable("o", "amount")
                                    )
                                ),
                                Integer("c", "id")
                            )
                        )
                    )
                ),
                (order, customer) => order.Amount >= customer.Id
            ),
            _ => throw new ArgumentException(on),
        };

        PureQLProjection projection = new(
            [Orders, Customers],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("s.orders", "o")),
                    [IntegerColumn("order", "o", "id"), StringColumn("name", "c", "name")],
                    subqueries: null,
                    joins: [new Join(new JoinEntity(kind, "s.customers", condition, "c"))],
                    where: null,
                    orderBy: null,
                    pagination: null,
                    distinctBy: null
                )
            )
        );

        Assert.Equal(
            [
                .. Reference(kind, holds)
                    .Select(pair => $"{pair.Order?.Id}|{pair.Customer?.Name}"),
            ],
            Rows(projection, "order", "name")
        );
    }

    [Fact]
    public void JoinKeyMayReadASourceBeforeThePreviousOne()
    {
        IStoredSchemaDataSet regions = new InlineTable(
            "s",
            "regions",
            ("id", new LongColumnType()),
            ("name", new StringColumnType())
        ).With(["1", "north"], ["2", "south"]);
        IStoredSchemaDataSet items = new InlineTable(
            "s",
            "items",
            ("id", new LongColumnType()),
            ("region_id", new LongColumnType()),
            ("owner_id", new LongColumnType())
        ).With(["1", "1", "1"], ["2", "2", "2"], ["3", "2", "9"], ["4", "3", "1"]);

        PureQLProjection projection = new(
            [regions, items, Customers],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("s.items", "i")),
                    [
                        IntegerColumn("item", "i", "id"),
                        StringColumn("owner", "c", "name"),
                        StringColumn("region", "r", "name"),
                    ],
                    subqueries: null,
                    joins:
                    [
                        new Join(
                            new JoinEntity(
                                JoinKind.Left,
                                "s.customers",
                                Equal(Integer("i", "owner_id"), Integer("c", "id")),
                                "c"
                            )
                        ),
                        new Join(
                            new JoinEntity(
                                JoinKind.Inner,
                                "s.regions",
                                Equal(Integer("r", "id"), Integer("i", "region_id")),
                                "r"
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
            ["1|ann|north", "2|bob|south", "2|bo2|south", "3||south"],
            Rows(projection, "item", "owner", "region")
        );
    }

    [Fact]
    public void JoinedSubqueryIsHashedOnItsColumns()
    {
        PureQLProjection projection = new(
            [Orders, Customers],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("s.customers", "c")),
                    [StringColumn("name", "c", "name"), IntegerColumn("order", "big", "id")],
                    subqueries:
                    [
                        new Subquery(
                            "big_orders",
                            new Query(
                                new PlainQuery(
                                    new From(new FromEntity("s.orders", "o")),
                                    [
                                        IntegerColumn("id", "o", "id"),
                                        IntegerColumn("customer", "o", "customer_id"),
                                    ],
                                    joins: null,
                                    where: new BooleanRow(
                                        new ComparisonRow(
                                            new GreaterThanRow(
                                                new GreaterThanDecimalRow(
                                                    new DecimalNullableRow(
                                                        new ExactDecimalNullableRow(
                                                            new FieldDecimalNullable(
                                                                "o",
                                                                "amount"
                                                            )
                                                        )
                                                    ),
                                                    new DecimalNullableRow(
                                                        new IntegerRow(new LiteralInteger(2))
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
                        ),
                    ],
                    joins:
                    [
                        new Join(
                            new JoinSubquery(
                                JoinKind.Left,
                                "big_orders",
                                Equal(Integer("big", "customer"), Integer("c", "id")),
                                "big"
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
            ["ann|10", "bob|11", "nil|", "bo2|11", "zed|"],
            Rows(projection, "name", "order")
        );
    }

    private static (BooleanRow Condition, Func<Order, Customer, bool> Holds) Case(
        BooleanRow condition,
        Func<Order, Customer, bool> holds
    )
    {
        return (condition, holds);
    }

    private static IEnumerable<(Order? Order, Customer? Customer)> Reference(
        JoinKind kind,
        Func<Order, Customer, bool> on
    )
    {
        List<(Order? Order, Customer? Customer)> rows = [];

        if (kind == JoinKind.Right)
        {
            foreach (Customer customer in CustomerRows)
            {
                List<Order> matches = [.. OrderRows.Where(order => on(order, customer))];
                rows.AddRange(matches.Select(order => ((Order?)order, (Customer?)customer)));

                if (matches.Count == 0)
                {
                    rows.Add((null, customer));
                }
            }

            return rows;
        }

        foreach (Order order in OrderRows)
        {
            List<Customer> matches =
            [
                .. CustomerRows.Where(customer => on(order, customer)),
            ];
            rows.AddRange(matches.Select(customer => ((Order?)order, (Customer?)customer)));

            if (matches.Count == 0 && kind != JoinKind.Inner)
            {
                rows.Add((order, null));
            }
        }

        if (kind == JoinKind.Full)
        {
            rows.AddRange(
                CustomerRows
                    .Where(customer => !OrderRows.Any(order => on(order, customer)))
                    .Select(customer => ((Order?)null, (Customer?)customer))
            );
        }

        return rows;
    }

    // The result rows as "|"-joined cell texts of the given columns.
    private static List<string> Rows(IEnumerable<IRow> rows, params string[] columns)
    {
        return
        [
            .. rows.Select(row =>
                string.Join(
                    "|",
                    columns.Select(column => ResultColumn.Texts([row], column)[0])
                )
            ),
        ];
    }

    private static string[] Texts(params object?[] values)
    {
        return
        [
            .. values.Select(value =>
                value is null
                    ? string.Empty
                    : string.Format(
                        System.Globalization.CultureInfo.InvariantCulture,
                        "{0}",
                        value
                    )
            ),
        ];
    }

    private static DecimalNullableRow Integer(string source, string field)
    {
        return new DecimalNullableRow(
            new ExactIntegerNullableRow(new FieldIntegerNullable(source, field))
        );
    }

    private static BooleanRow Equal(DecimalNullableRow left, DecimalNullableRow right)
    {
        return new BooleanRow(
            new ComparisonRow(new EqualRow(new EqualDecimalRow(left, right)))
        );
    }

    private static SelectItemProjection IntegerColumn(
        string alias,
        string source,
        string field
    )
    {
        return new SelectItemProjection(
            new SelectItemProjectionNullable(
                new SelectItemProjectionIntegerNullable(
                    alias,
                    new ExactIntegerNullableProjection(
                        new FieldIntegerNullable(source, field)
                    )
                )
            )
        );
    }

    private static SelectItemProjection StringColumn(
        string alias,
        string source,
        string field
    )
    {
        return new SelectItemProjection(
            new SelectItemProjectionNullable(
                new SelectItemProjectionStringNullable(
                    alias,
                    new ExactStringNullableProjection(
                        new FieldStringNullable(source, field)
                    )
                )
            )
        );
    }
}
