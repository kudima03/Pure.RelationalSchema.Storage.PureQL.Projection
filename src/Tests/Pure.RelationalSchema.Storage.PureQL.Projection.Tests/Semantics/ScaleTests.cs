using System.Diagnostics;
using System.Globalization;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.GroupExpressions;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;

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

    private const int Amounts = 100_000;

    private static readonly TimeSpan Bound = TimeSpan.FromSeconds(3);

    [Fact]
    public void ChainOfJoinsOverThousandsOfRowsIsFast()
    {
        IStoredSchemaDataSet lines = Table("order_lines", Lines, i =>
            (i % Orders).ToString(CultureInfo.InvariantCulture)
        );
        IStoredSchemaDataSet orders = Table("orders", Orders, i =>
            (i % Customers).ToString(CultureInfo.InvariantCulture)
        );
        IStoredSchemaDataSet customers = Table("customers", Customers, i =>
            (i % 7).ToString(CultureInfo.InvariantCulture)
        );
        IStoredSchemaDataSet tickets = Table("tickets", Tickets, i =>
            (i * 3 % Customers).ToString(CultureInfo.InvariantCulture)
        );

        PureQLProjection projection = new(
            [lines, orders, customers, tickets],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("s.order_lines", "l"),
                joins:
                [
                    Inner("s.orders", "o", "l", "parent", "o", "id"),
                    Inner("s.customers", "c", "o", "parent", "c", "id"),
                    new JoinEntity(
                        JoinKind.Left,
                        "s.tickets",
                        Equal("t", "parent", true, "c", "id", false),
                        "t"
                    ),
                ],
                groupBy: [new GroupKey("customer", new FieldInteger("c", "id"))],
                select:
                [
                    new SelectItemGroup("customer", new KeyInteger("customer")),
                    new SelectItemGroup("lines", new CountGroup()),
                ]
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
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "amounts",
                        new PlainQueryWithoutPaginationWithoutOrderBy(
                            from: new FromEntity("s.orders", "o"),
                            select:
                            [
                                new SelectItemProjection(
                                    "amount",
                                    new FieldDecimal("o", "amount")
                                ),
                            ]
                        )
                    ),
                ],
                from: new FromEntity("s.order_lines", "l"),
                where: new InDecimalRow(
                    new FieldDecimal("l", "amount"),
                    new ListSubqueryColumnDecimal("amounts", "amount", false)
                ),
                select: [new SelectItemProjection("id", new FieldInteger("l", "id"))]
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
        ).With([
            .. Enumerable
                .Range(0, Amounts)
                .Select(i =>
                    new[]
                    {
                        i.ToString(CultureInfo.InvariantCulture),
                        amount(i).ToString(CultureInfo.InvariantCulture),
                    }
                ),
        ]);
    }

    // A table of (id, parent): id counts up, parent is the key into the next
    // table.
    private static IStoredSchemaDataSet Table(
        string name,
        int rows,
        Func<int, string> parent
    )
    {
        return new InlineTable(
            "s",
            name,
            ("id", new LongColumnType()),
            ("parent", new LongColumnType())
        ).With([
            .. Enumerable
                .Range(0, rows)
                .Select(i =>
                    new[] { i.ToString(CultureInfo.InvariantCulture), parent(i) }
                ),
        ]);
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
        return new JoinEntity(
            JoinKind.Inner,
            entity,
            Equal(leftSource, leftField, false, rightSource, rightField, false),
            alias
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
        return new EqualDecimalRow(
            Integer(leftSource, leftField, leftNullable),
            Integer(rightSource, rightField, rightNullable)
        );
    }

    private static DecimalNullableRow Integer(string source, string field, bool nullable)
    {
        return nullable
            ? new FieldIntegerNullable(source, field)
            : new FieldInteger(source, field);
    }
}
