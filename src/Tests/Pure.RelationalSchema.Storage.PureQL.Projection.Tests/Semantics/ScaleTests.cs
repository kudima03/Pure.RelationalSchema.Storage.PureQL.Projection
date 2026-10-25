using System.Diagnostics;
using System.Globalization;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.GroupExpressions;
using PureQL.CSharp.Model.GroupKeys;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.SelectItems;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Queries over tables of thousands of rows finish in well under the time a
// nested loop over the product of the tables takes: a join and a membership
// test cost the rows of both sides, not their product. The bounds are
// generous, so that a slow machine passes while a quadratic join (tens of
// seconds for the chain below) cannot.
public sealed class ScaleTests
{
    private const int Lines = 10_000;

    private const int Orders = 3_000;

    private const int Customers = 1_000;

    private const int Tickets = 2_000;

    private const int Amounts = 30_000;

    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(3);

    [Fact]
    public void ChainOfJoinsOverThousandsOfRowsIsFast()
    {
        IStoredSchemaDataSet lines = Table(
            "order_lines",
            Lines,
            i => (i % Orders).ToString(CultureInfo.InvariantCulture)
        );
        IStoredSchemaDataSet orders = Table(
            "orders",
            Orders,
            i => (i % Customers).ToString(CultureInfo.InvariantCulture)
        );
        IStoredSchemaDataSet customers = Table(
            "customers",
            Customers,
            i => (i % 7).ToString(CultureInfo.InvariantCulture)
        );
        IStoredSchemaDataSet tickets = Table(
            "tickets",
            Tickets,
            i => (i * 3 % Customers).ToString(CultureInfo.InvariantCulture)
        );

        PureQLProjection projection = new(
            [lines, orders, customers, tickets],
            new PureQLQuery(
                new MainGroupedQuery(
                    new From(new FromEntity("s.order_lines", "l")),
                    [
                        new GroupKey(
                            new GroupKeyNonNullable(
                                new GroupKeyInteger(
                                    "customer",
                                    new IntegerRow(new FieldInteger("c", "id"))
                                )
                            )
                        ),
                    ],
                    [
                        new SelectItemGroup(
                            new SelectItemGroupNonNullable(
                                new SelectItemGroupInteger(
                                    "customer",
                                    new IntegerGroup(new KeyInteger("customer"))
                                )
                            )
                        ),
                        new SelectItemGroup(
                            new SelectItemGroupNonNullable(
                                new SelectItemGroupInteger(
                                    "lines",
                                    new IntegerGroup(
                                        new AggregateIntegerGroup(new CountGroup())
                                    )
                                )
                            )
                        ),
                    ],
                    subqueries: null,
                    joins:
                    [
                        Inner("s.orders", "o", "l", "parent", "o", "id"),
                        Inner("s.customers", "c", "o", "parent", "c", "id"),
                        new Join(
                            new JoinEntity(
                                JoinKind.Left,
                                "s.tickets",
                                Equal("t", "parent", true, "c", "id", false),
                                "t"
                            )
                        ),
                    ],
                    where: null,
                    having: null,
                    orderBy: null,
                    pagination: null,
                    distinctBy: null
                )
            )
        );

        Stopwatch watch = Stopwatch.StartNew();
        int groups = projection.Count();
        watch.Stop();

        Assert.Equal(Customers, groups);
        Assert.True(
            watch.Elapsed < Bound,
            $"The joins took {watch.Elapsed.TotalSeconds:F1} s."
        );
    }

    // Decimal keys: comparing two decimals costs enough that scanning the
    // subquery's rows for every row takes several seconds.
    [Fact]
    public void MembershipInASubqueryOfThousandsOfRowsIsFast()
    {
        IStoredSchemaDataSet lines = Priced("order_lines", i => i + 0.25m);
        IStoredSchemaDataSet orders = Priced("orders", i => (i * 2) + 0.25m);

        PureQLProjection projection = new(
            [lines, orders],
            new PureQLQuery(
                new MainPlainQuery(
                    new From(new FromEntity("s.order_lines", "l")),
                    [
                        new SelectItemProjection(
                            new SelectItemProjectionNonNullable(
                                new SelectItemProjectionInteger(
                                    "id",
                                    new IntegerProjection(new FieldInteger("l", "id"))
                                )
                            )
                        ),
                    ],
                    subqueries:
                    [
                        new Subquery(
                            "amounts",
                            new Query(
                                new PlainQuery(
                                    new From(new FromEntity("s.orders", "o")),
                                    [
                                        new SelectItemProjection(
                                            new SelectItemProjectionNonNullable(
                                                new SelectItemProjectionDecimal(
                                                    "amount",
                                                    new ExactDecimalProjection(
                                                        new FieldDecimal("o", "amount")
                                                    )
                                                )
                                            )
                                        ),
                                    ],
                                    joins: null,
                                    where: null,
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
                            new InRow(
                                new InDecimalRow(
                                    new DecimalNullableRow(
                                        new ExactDecimalRow(new FieldDecimal("l", "amount"))
                                    ),
                                    new ListDecimal(
                                        new ListSubqueryColumnDecimal(
                                            "amounts",
                                            "amount",
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

        Stopwatch watch = Stopwatch.StartNew();
        int rows = projection.Count();
        watch.Stop();

        Assert.Equal(Amounts / 2, rows);
        Assert.True(
            watch.Elapsed < Bound,
            $"The membership test took {watch.Elapsed.TotalSeconds:F1} s."
        );
    }

    // A table of (id, amount): id counts up, amount is a decimal of the id.
    private static IStoredSchemaDataSet Priced(string name, Func<int, decimal> amount)
    {
        return new InlineTable(
            "s",
            name,
            ("id", new LongColumnType()),
            ("amount", new DoubleColumnType())
        ).With(
            [
                .. Enumerable
                    .Range(0, Amounts)
                    .Select(i => new[]
                    {
                        i.ToString(CultureInfo.InvariantCulture),
                        amount(i).ToString(CultureInfo.InvariantCulture),
                    }),
            ]
        );
    }

    // A table of (id, parent): id counts up, parent is the key into the next
    // table.
    private static IStoredSchemaDataSet Table(string name, int rows, Func<int, string> parent)
    {
        return new InlineTable(
            "s",
            name,
            ("id", new LongColumnType()),
            ("parent", new LongColumnType())
        ).With(
            [
                .. Enumerable
                    .Range(0, rows)
                    .Select(i => new[] { i.ToString(CultureInfo.InvariantCulture), parent(i) }),
            ]
        );
    }

    private static Join Inner(
        string entity,
        string alias,
        string leftSource,
        string leftField,
        string rightSource,
        string rightField
    )
    {
        return new Join(
            new JoinEntity(
                JoinKind.Inner,
                entity,
                Equal(leftSource, leftField, false, rightSource, rightField, false),
                alias
            )
        );
    }

    private static BooleanRow Equal(
        string leftSource,
        string leftField,
        bool leftNullable,
        string rightSource,
        string rightField,
        bool rightNullable
    )
    {
        return new BooleanRow(
            new ComparisonRow(
                new EqualRow(
                    new EqualDecimalRow(
                        Integer(leftSource, leftField, leftNullable),
                        Integer(rightSource, rightField, rightNullable)
                    )
                )
            )
        );
    }

    private static DecimalNullableRow Integer(string source, string field, bool nullable)
    {
        return nullable
            ? new DecimalNullableRow(
                new ExactIntegerNullableRow(new FieldIntegerNullable(source, field))
            )
            : new DecimalNullableRow(new IntegerRow(new FieldInteger(source, field)));
    }
}
