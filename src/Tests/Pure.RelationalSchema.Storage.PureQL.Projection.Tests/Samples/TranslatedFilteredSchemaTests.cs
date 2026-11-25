using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Samples.Tables;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.RowExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Samples;

// A query over a filtered schema dataset whose tables are stored in
// PostgreSQL is still one SQL statement, run by the provider of the tables:
// the conditions are part of it, each at the scan of its table, and the
// result is the one the same query gives over the same tables, filtered the
// same way, held in memory. The fixture's tables are filtered as a host
// would filter them for a user: the users of one tenant, the orders that
// are not cancelled, the products in stock and the order items of more than
// one unit; the employees are not filtered. Needs Docker.
public sealed class TranslatedFilteredSchemaTests(PostgreSqlDatabase database)
    : IClassFixture<PostgreSqlDatabase>
{
    private const string Tenant = "00000385-0000-0000-0000-000000000000";

    // The scan of the users of the tenant, as a plan shows it: the condition
    // is the scan's filter, over the stored column as it is.
    private const string ScanOfOwnUsers =
        "Seq Scan on \"[0-9A-F]+\" t0 .*\n"
        + "Filter: \\(\"[0-9A-F]+\" = '"
        + Tenant
        + "'::uuid\\)";

    // The scan of the shipped orders below a join.
    private const string ScanOfShippedOrders =
        "Seq Scan on \"[0-9A-F]+\" t1 .*\n"
        + "Filter: \\(COALESCE\\(\"[0-9A-F]+\", ''::text\\) = 'shipped'::text\\)";

    private readonly PostgreSqlDatabase _database = database;

    public static TheoryData<string> SamplesWithResult =>
        SampleCatalogueTests.SamplesWithResult;

    public static TheoryData<string> SamplesWithParameters =>
        SampleCatalogueTests.SamplesWithParameters;

    // The users of one tenant: Ann, Bob and Cara.
    private static BooleanRow OwnUsers =>
        new(
            new EqualUuidRow(
                new FieldUuid("users", "user_tenant_id"),
                new LiteralUuid(Guid.Parse(Tenant))
            )
        );

    // The orders that are shipped or pending: all but one of Cara's.
    private static BooleanRow OpenOrders =>
        new(
            new InStringRow(
                new FieldStringNullable("orders", "order_status"),
                new ListLiteralString(["shipped", "pending"])
            )
        );

    // The shipped orders: one of Ann's, Bob's and one of Cara's.
    private static BooleanRow Shipped =>
        new(
            new EqualStringRow(
                new FieldString("orders", "order_status"),
                new LiteralString("shipped")
            )
        );

    private static BooleanRow InStock =>
        new(new FieldBoolean("products", "product_in_stock"));

    private static BooleanRow SeveralUnits =>
        new(
            new GreaterThanDecimalRow(
                new FieldDecimalNullable("order_items", "item_qty"),
                new LiteralInteger(1)
            )
        );

    [Theory]
    [MemberData(nameof(SamplesWithResult))]
    public void SampleOverFilteredTablesIsTranslatedAndReturnsTheFilteredResult(
        string name
    )
    {
        Sample sample = Sample.Named(name);
        PureQLProjection expected = new(Filtered(InMemory()), sample.Query);

        PureQLProjection actual = new(Filtered(_database.Datasets), sample.Query);

        AssertSame(sample, expected, actual);
        Assert.Null(actual.FallbackReason);
        Assert.Equal(QueryExecution.Translated, actual.Execution);
    }

    [Theory]
    [MemberData(nameof(SamplesWithParameters))]
    public void SampleWithParametersOverFilteredTablesIsTranslated(string name)
    {
        Sample sample = Sample.Named(name);
        PureQLProjection expected = new(
            Filtered(InMemory()),
            sample.Query,
            SampleCatalogueTests.Parameters
        );

        PureQLProjection actual = new(
            Filtered(_database.Datasets),
            sample.Query,
            SampleCatalogueTests.Parameters
        );

        AssertSame(sample, expected, actual);
        Assert.Null(actual.FallbackReason);
        Assert.Equal(QueryExecution.Translated, actual.Execution);
    }

    [Fact]
    public void ConditionOfATableInFromIsInTheStatementAtTheScanOfTheTable()
    {
        PureQLProjection projection = new(
            Filtered(_database.Datasets),
            Sample.Named("UserNameColumnQuery").Query
        );

        IReadOnlyList<string> names = ResultColumn.Texts(projection, "user_name");
        string plan = _database.LastPlan();

        Assert.Equal(["Ann", "Bob", "Cara"], names.Order());
        Assert.Equal(QueryExecution.Translated, projection.Execution);
        Assert.Null(projection.FallbackReason);
        Assert.Contains(" WHERE t0.", plan, StringComparison.Ordinal);
        Assert.Matches(ScanOfOwnUsers, plan);
    }

    [Fact]
    public void ConditionOfTheOptionalSideOfALeftJoinIsBelowTheJoin()
    {
        PureQLProjection projection = new(
            [
                new PureQLFilteredSchemaDataSet(
                    _database.Datasets[0],
                    [KeyValuePair.Create<ITable, BooleanRow>(new OrdersTable(), Shipped)]
                ),
            ],
            UsersWithOrders()
        );

        IReadOnlyList<string> rows = Staff.Lines(projection);
        string plan = _database.LastPlan();

        Assert.Equal(
            ["Ann|100.5", "Bob|200", "Cara|300", "Dan|", "Eve|", "Fay|"],
            rows.Order()
        );
        Assert.Equal(QueryExecution.Translated, projection.Execution);
        Assert.Null(projection.FallbackReason);
        Assert.Matches("Hash (Left|Right) Join", plan);
        Assert.Matches(ScanOfShippedOrders, plan);
        Assert.DoesNotContain("Join Filter", plan, StringComparison.Ordinal);
    }

    [Fact]
    public void FilteredTableEnumeratedOnItsOwnIsFilteredByTheStore()
    {
        IStoredTableDataSet users = Filtered(_database.Datasets)[0][new UsersTable()];

        IReadOnlyList<string> names = ResultColumn.Texts(users, "user_name");
        string plan = _database.LastPlan();

        Assert.Equal(["Ann", "Bob", "Cara"], names.Order());
        Assert.Matches(ScanOfOwnUsers, plan);
    }

    [Fact]
    public void LinqComposedOverAFilteredTableIsFilteredByTheStore()
    {
        IStoredTableDataSet users = Filtered(_database.Datasets)[0][new UsersTable()];

        int count = users.Count();
        string plan = _database.LastPlan();

        Assert.Equal(3, count);
        Assert.Contains("SELECT CAST(COUNT(*)", plan, StringComparison.Ordinal);
        Assert.Matches(ScanOfOwnUsers, plan);
    }

    private static void AssertSame(
        Sample sample,
        PureQLProjection expected,
        PureQLProjection actual
    )
    {
        if (sample.IsOrdered)
        {
            new ExpectedTable(expected).AssertOrdered(actual);
        }
        else
        {
            new ExpectedTable(expected).AssertUnordered(actual);
        }
    }

    private static IReadOnlyList<IStoredSchemaDataSet> InMemory()
    {
        return [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
    }

    // The datasets, the tables of the first under their conditions.
    private static IReadOnlyList<IStoredSchemaDataSet> Filtered(
        IReadOnlyList<IStoredSchemaDataSet> datasets
    )
    {
        return
        [
            new PureQLFilteredSchemaDataSet(
                datasets[0],
                new Dictionary<ITable, BooleanRow>(ReferenceEqualityComparer.Instance)
                {
                    [new UsersTable()] = OwnUsers,
                    [new OrdersTable()] = OpenOrders,
                    [new ProductsTable()] = InStock,
                    [new OrderItemsTable()] = SeveralUnits,
                }
            ),
            new PureQLFilteredSchemaDataSet(datasets[1], []),
        ];
    }

    // from users u left join orders o on o.order_user_id = u.user_id
    // select u.user_name, o.order_total
    private static PureQLQuery UsersWithOrders()
    {
        const string users = "schema_with_foreign_keys.users";
        const string orders = "schema_with_foreign_keys.orders";

        return new MainPlainQueryWithoutPagination(
            from: new FromEntity(users, "u"),
            joins:
            [
                new JoinEntity(
                    JoinKind.Left,
                    orders,
                    new EqualUuidRow(
                        new FieldUuid("o", "order_user_id"),
                        new FieldUuid("u", "user_id")
                    ),
                    "o"
                ),
            ],
            select:
            [
                new SelectItemProjection("user_name", new FieldString("u", "user_name")),
                new SelectItemProjection(
                    "order_total",
                    new FieldDecimalNullable("o", "order_total")
                ),
            ]
        );
    }
}
