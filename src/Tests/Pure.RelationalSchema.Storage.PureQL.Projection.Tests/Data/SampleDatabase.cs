using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// The single, deterministic, typed, multi-table sample dataset shared by every
// test. It exposes:
//   - Datasets: the IEnumerable<IStoredSchemaDataSet> fed to PureQLProjection.
//   - Metadata (schema/table/column names + "schema.table" entity paths) as
//     constants, so tests build fully explicit queries.
//   - Ground-truth record lists (UserRows, OrderRows, ...) so tests compute
//     expected results independently.
//
// Every column name is globally unique because the translator resolves fields
// by bare column name across merged (joined) rows; unique names keep joins and
// projections unambiguous. Column types match the field kind used to select
// them (uuid/string/double/bool/date/datetime/time).
internal sealed class SampleDatabase
{
    public const string SchemaName = "shop";

    public static class Users
    {
        public const string TableName = "users";
        public const string Entity = "shop.users";
        public const string Id = "user_id";
        public const string Name = "user_name";
        public const string Age = "user_age";
        public const string Active = "user_active";
        public const string SignupDate = "signup_date";
        public const string LastLogin = "last_login";
        public const string ShiftStart = "shift_start";
    }

    public static class Orders
    {
        public const string TableName = "orders";
        public const string Entity = "shop.orders";
        public const string Id = "order_id";
        public const string UserId = "order_user_id";
        public const string Total = "order_total";
        public const string Status = "order_status";
        public const string PlacedAt = "placed_at";
        public const string PlacedOn = "placed_on";
    }

    public static class Products
    {
        public const string TableName = "products";
        public const string Entity = "shop.products";
        public const string Id = "product_id";
        public const string Name = "product_name";
        public const string Price = "product_price";
        public const string InStock = "product_in_stock";
    }

    public static class OrderItems
    {
        public const string TableName = "order_items";
        public const string Entity = "shop.order_items";
        public const string Id = "item_id";
        public const string OrderId = "item_order_id";
        public const string ProductId = "item_product_id";
        public const string Qty = "item_qty";
    }

    private static readonly IReadOnlyList<IColumn> UserColumns =
    [
        new Column.Column(new String(Users.Id), new UuidColumnType()),
        new Column.Column(new String(Users.Name), new StringColumnType()),
        new Column.Column(new String(Users.Age), new DoubleColumnType()),
        new Column.Column(new String(Users.Active), new BoolColumnType()),
        new Column.Column(new String(Users.SignupDate), new DateColumnType()),
        new Column.Column(new String(Users.LastLogin), new DateTimeColumnType()),
        new Column.Column(new String(Users.ShiftStart), new TimeColumnType()),
    ];

    private static readonly IReadOnlyList<IColumn> OrderColumns =
    [
        new Column.Column(new String(Orders.Id), new UuidColumnType()),
        new Column.Column(new String(Orders.UserId), new UuidColumnType()),
        new Column.Column(new String(Orders.Total), new DoubleColumnType()),
        new Column.Column(new String(Orders.Status), new StringColumnType()),
        new Column.Column(new String(Orders.PlacedAt), new DateTimeColumnType()),
        new Column.Column(new String(Orders.PlacedOn), new DateColumnType()),
    ];

    private static readonly IReadOnlyList<IColumn> ProductColumns =
    [
        new Column.Column(new String(Products.Id), new UuidColumnType()),
        new Column.Column(new String(Products.Name), new StringColumnType()),
        new Column.Column(new String(Products.Price), new DoubleColumnType()),
        new Column.Column(new String(Products.InStock), new BoolColumnType()),
    ];

    private static readonly IReadOnlyList<IColumn> OrderItemColumns =
    [
        new Column.Column(new String(OrderItems.Id), new UuidColumnType()),
        new Column.Column(new String(OrderItems.OrderId), new UuidColumnType()),
        new Column.Column(new String(OrderItems.ProductId), new UuidColumnType()),
        new Column.Column(new String(OrderItems.Qty), new DoubleColumnType()),
    ];

    private readonly IReadOnlyList<IStoredSchemaDataSet> _datasets;

    public SampleDatabase()
    {
        ITable usersTable = new Table.Table(
            new String(Users.TableName),
            UserColumns,
            []
        );
        ITable ordersTable = new Table.Table(
            new String(Orders.TableName),
            OrderColumns,
            []
        );
        ITable productsTable = new Table.Table(
            new String(Products.TableName),
            ProductColumns,
            []
        );
        ITable orderItemsTable = new Table.Table(
            new String(OrderItems.TableName),
            OrderItemColumns,
            []
        );

        ISchema schema = new Schema.Schema(
            new String(SchemaName),
            [usersTable, ordersTable, productsTable, orderItemsTable],
            []
        );

        IEnumerable<IStoredTableDataSet> tableDatasets =
        [
            new SampleTableDataset(usersTable, BuildUserRows()),
            new SampleTableDataset(ordersTable, BuildOrderRows()),
            new SampleTableDataset(productsTable, BuildProductRows()),
            new SampleTableDataset(orderItemsTable, BuildOrderItemRows()),
        ];

        IReadOnlyDictionary<ITable, IStoredTableDataSet> datasetsByTable =
            new Collections.Generic.Dictionary<
                IStoredTableDataSet,
                ITable,
                IStoredTableDataSet
            >(
                tableDatasets,
                dataset => dataset.TableSchema,
                dataset => dataset,
                table => new TableHash(table)
            );

        _datasets = [new StoredSchemaDataset(schema, datasetsByTable)];
    }

    public IEnumerable<IStoredSchemaDataSet> Datasets => _datasets;

    public IReadOnlyList<UserRow> UserRows { get; } = [
        new(
            Id(1),
            "Ann",
            30,
            true,
            new DateOnly(2020, 1, 15),
            new DateTime(2024, 6, 1, 8, 30, 0),
            new TimeOnly(9, 0, 0)
        ),
        new(
            Id(2),
            "Bob",
            25,
            false,
            new DateOnly(2021, 3, 20),
            new DateTime(2024, 6, 2, 9, 15, 0),
            new TimeOnly(10, 0, 0)
        ),
        new(
            Id(3),
            "Cara",
            30,
            true,
            new DateOnly(2019, 7, 10),
            new DateTime(2024, 5, 30, 14, 0, 0),
            new TimeOnly(9, 0, 0)
        ),
        new(
            Id(4),
            "Dan",
            42,
            true,
            new DateOnly(2022, 11, 5),
            new DateTime(2024, 6, 3, 18, 45, 0),
            new TimeOnly(11, 30, 0)
        ),
        new(
            Id(5),
            "Eve",
            25,
            false,
            new DateOnly(2023, 2, 28),
            new DateTime(2024, 6, 4, 7, 5, 0),
            new TimeOnly(8, 0, 0)
        ),
    ];

    public IReadOnlyList<OrderRow> OrderRows { get; } = [
        new(
            Id(101),
            Id(1),
            100.50,
            "shipped",
            new DateTime(2024, 6, 1, 10, 0, 0),
            new DateOnly(2024, 6, 1)
        ),
        new(
            Id(102),
            Id(1),
            50.00,
            "pending",
            new DateTime(2024, 6, 2, 11, 0, 0),
            new DateOnly(2024, 6, 2)
        ),
        new(
            Id(103),
            Id(2),
            200.00,
            "shipped",
            new DateTime(2024, 6, 3, 12, 0, 0),
            new DateOnly(2024, 6, 3)
        ),
        new(
            Id(104),
            Id(3),
            75.25,
            "cancelled",
            new DateTime(2024, 6, 4, 13, 0, 0),
            new DateOnly(2024, 6, 4)
        ),
        new(
            Id(105),
            Id(3),
            300.00,
            "shipped",
            new DateTime(2024, 6, 5, 14, 0, 0),
            new DateOnly(2024, 6, 5)
        ),
        new(
            Id(106),
            Id(4),
            100.50,
            "pending",
            new DateTime(2024, 6, 6, 15, 0, 0),
            new DateOnly(2024, 6, 6)
        ),
    ];

    public IReadOnlyList<ProductRow> ProductRows { get; } = [
        new(Id(201), "Widget", 9.99, true),
        new(Id(202), "Gadget", 19.99, false),
        new(Id(203), "Gizmo", 4.50, true),
    ];

    public IReadOnlyList<OrderItemRow> OrderItemRows { get; } = [
        new(Id(301), Id(101), Id(201), 2),
        new(Id(302), Id(101), Id(202), 1),
        new(Id(303), Id(103), Id(203), 5),
        new(Id(304), Id(105), Id(201), 3),
    ];

    private static Guid Id(int seed)
    {
        return new Guid(seed, 0, 0, new byte[8]);
    }

    private IReadOnlyList<IRow> BuildUserRows()
    {
        return
        [
            .. UserRows.Select(user =>
                BuildRow(
                    UserColumns,
                    new Dictionary<string, string>
                    {
                        [Users.Id] = CellText.From(user.UserId),
                        [Users.Name] = CellText.From(user.UserName),
                        [Users.Age] = CellText.From(user.UserAge),
                        [Users.Active] = CellText.From(user.UserActive),
                        [Users.SignupDate] = CellText.From(user.SignupDate),
                        [Users.LastLogin] = CellText.From(user.LastLogin),
                        [Users.ShiftStart] = CellText.From(user.ShiftStart),
                    }
                )
            ),
        ];
    }

    private IReadOnlyList<IRow> BuildOrderRows()
    {
        return
        [
            .. OrderRows.Select(order =>
                BuildRow(
                    OrderColumns,
                    new Dictionary<string, string>
                    {
                        [Orders.Id] = CellText.From(order.OrderId),
                        [Orders.UserId] = CellText.From(order.OrderUserId),
                        [Orders.Total] = CellText.From(order.OrderTotal),
                        [Orders.Status] = CellText.From(order.OrderStatus),
                        [Orders.PlacedAt] = CellText.From(order.PlacedAt),
                        [Orders.PlacedOn] = CellText.From(order.PlacedOn),
                    }
                )
            ),
        ];
    }

    private IReadOnlyList<IRow> BuildProductRows()
    {
        return
        [
            .. ProductRows.Select(product =>
                BuildRow(
                    ProductColumns,
                    new Dictionary<string, string>
                    {
                        [Products.Id] = CellText.From(product.ProductId),
                        [Products.Name] = CellText.From(product.ProductName),
                        [Products.Price] = CellText.From(product.ProductPrice),
                        [Products.InStock] = CellText.From(product.ProductInStock),
                    }
                )
            ),
        ];
    }

    private IReadOnlyList<IRow> BuildOrderItemRows()
    {
        return
        [
            .. OrderItemRows.Select(item =>
                BuildRow(
                    OrderItemColumns,
                    new Dictionary<string, string>
                    {
                        [OrderItems.Id] = CellText.From(item.ItemId),
                        [OrderItems.OrderId] = CellText.From(item.ItemOrderId),
                        [OrderItems.ProductId] = CellText.From(item.ItemProductId),
                        [OrderItems.Qty] = CellText.From(item.ItemQty),
                    }
                )
            ),
        ];
    }

    private static IRow BuildRow(
        IEnumerable<IColumn> columns,
        IReadOnlyDictionary<string, string> textByName
    )
    {
        return new Row(
            new Collections.Generic.Dictionary<IColumn, IColumn, ICell>(
                columns,
                column => column,
                column => new Cell(new String(textByName[column.Name.TextValue])),
                column => new ColumnHash(column)
            )
        );
    }
}
