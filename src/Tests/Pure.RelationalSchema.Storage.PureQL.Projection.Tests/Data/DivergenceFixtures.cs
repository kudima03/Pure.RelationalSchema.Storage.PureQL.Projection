using Npgsql;
using NpgsqlTypes;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PostgreSQL.Querying;
using Testcontainers.PostgreSql;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// Small tables chosen where SQL and the specification may disagree (signs of
// remainders, rounding, decimal precision, nulls in comparisons, lists and
// set operations, string order by code point, null placement in sorting,
// aggregates over no rows), held in memory (Memory) and stored in a
// PostgreSQL database of their own (Stored), read through the provider of
// Pure.RelationalSchema.Storage.PostgreSQL.Querying. The tables are created
// and filled as PostgreSqlDatabase creates and fills the sample fixtures: an
// empty cell is NULL. A decimal column is a double column, whose values here
// have at most 15 significant digits, so a double holds each of them as its
// shortest text. The database starts when Stored is first read. Needs Docker.
public sealed class DivergenceFixtures : IDisposable
{
    private const int MaxIdentifierLength = 63;

    // Integers: remainders and quotients of every sign, nulls on either side.
    internal static readonly IStoredSchemaDataSet Ints = new InlineTable(
        "n",
        "ints",
        ("id", new LongColumnType()),
        ("a", new LongColumnType()),
        ("b", new LongColumnType())
    ).With(
        ["1", "7", "2"],
        ["2", "-7", "2"],
        ["3", "7", "-2"],
        ["4", "-7", "-2"],
        ["5", "", "3"],
        ["6", "3", ""],
        ["7", "", ""]
    );

    // Decimals: quotients that need 28 digits, halves to round, a product
    // with 30 decimal places, a null.
    internal static readonly IStoredSchemaDataSet Decimals = new InlineTable(
        "n",
        "decimals",
        ("id", new LongColumnType()),
        ("x", new DoubleColumnType()),
        ("y", new DoubleColumnType())
    ).With(
        ["1", "1", "3"],
        ["2", "2", "3"],
        ["3", "-2", "3"],
        ["4", "2.5", "1"],
        ["5", "-2.5", "1"],
        ["6", "0.111111111111111", "0.111111111111111"],
        ["7", "", "2"],
        ["8", "1", "7"]
    );

    // The integers 2^63 - 1, 1 and -1, whose sum fits although a partial sum
    // may not.
    internal static readonly IStoredSchemaDataSet Big = new InlineTable(
        "n",
        "big",
        ("id", new LongColumnType()),
        ("v", new LongColumnType())
    ).With(["1", "9223372036854775807"], ["2", "1"], ["3", "-1"]);

    // Integer values in groups: 1, 2, 2; 1, 2; and a null alone.
    internal static readonly IStoredSchemaDataSet Grouped = new InlineTable(
        "n",
        "grouped",
        ("id", new LongColumnType()),
        ("g", new LongColumnType()),
        ("v", new LongColumnType())
    ).With(
        ["1", "1", "1"],
        ["2", "1", "2"],
        ["3", "1", "2"],
        ["4", "2", "1"],
        ["5", "2", "2"],
        ["6", "3", ""]
    );

    // Strings whose code point order differs from a linguistic collation and
    // from UTF-16 order, and a null.
    internal static readonly IStoredSchemaDataSet Words = new InlineTable(
        "s",
        "words",
        ("id", new LongColumnType()),
        ("w", new StringColumnType())
    ).With(
        ["1", "a"],
        ["2", "B"],
        ["3", "b"],
        ["4", "A"],
        ["5", "ab"],
        ["6", ""],
        ["7", "\U0001F600"],
        ["8", "�"],
        ["9", "é"],
        ["10", "Z"]
    );

    // Strings holding the wildcards and the escape character of LIKE.
    internal static readonly IStoredSchemaDataSet Patterns = new InlineTable(
        "s",
        "patterns",
        ("id", new LongColumnType()),
        ("p", new StringColumnType())
    ).With(
        ["1", "100%"],
        ["2", "1000"],
        ["3", "a_c"],
        ["4", "abc"],
        ["5", "a\\c"],
        ["6", "a\\\\c"],
        ["7", ""],
        ["8", "A_C"]
    );

    // Dates, times, instants, uuids and booleans, with a null of each.
    internal static readonly IStoredSchemaDataSet Moments = new InlineTable(
        "m",
        "moments",
        ("id", new LongColumnType()),
        ("d", new DateColumnType()),
        ("t", new TimeColumnType()),
        ("dt", new DateTimeColumnType()),
        ("u", new UuidColumnType()),
        ("f", new BoolColumnType())
    ).With(
        [
            "1",
            "2024-01-31",
            "23:00:00",
            "2024-01-31 23:59:59.999999",
            "00000000-0000-0000-0000-000000000003",
            "True",
        ],
        [
            "2",
            "2023-01-31",
            "00:30:00.25",
            "2024-02-29 00:00:00.5",
            "10000000-0000-0000-0000-000000000000",
            "False",
        ],
        [
            "3",
            "2024-12-30",
            "12:00:00",
            "2021-01-01 12:00:00",
            "0000000a-0000-0000-0000-000000000000",
            "True",
        ],
        [
            "4",
            "2021-01-03",
            "06:15:30.5",
            "1999-12-31 23:59:59",
            "ffffffff-0000-0000-0000-000000000000",
            "False",
        ],
        [
            "5",
            "2024-02-29",
            "18:30:15.25",
            "2024-08-15 18:30:15.25",
            "00000000-0000-0000-0001-000000000000",
            "True",
        ],
        ["6", "", "", "", "", ""]
    );

    // Two tables keyed by nullable integers: 1, null, 2 and 1, null, 3.
    internal static readonly IStoredSchemaDataSet Left = new InlineTable(
        "k",
        "left",
        ("id", new LongColumnType()),
        ("k", new LongColumnType())
    ).With(["1", "1"], ["2", ""], ["3", "2"]);

    internal static readonly IStoredSchemaDataSet Right = new InlineTable(
        "k",
        "right",
        ("id", new LongColumnType()),
        ("k", new LongColumnType())
    ).With(["10", "1"], ["11", ""], ["12", "3"]);

    // Pairs (n, s) with repeated rows and repeated null rows, and (n, s, d)
    // whose decimals equal the integers of the first.
    internal static readonly IStoredSchemaDataSet First = new InlineTable(
        "c",
        "first",
        ("id", new LongColumnType()),
        ("n", new LongColumnType()),
        ("s", new StringColumnType())
    ).With(
        ["1", "1", "x"],
        ["2", "1", "x"],
        ["3", "2", "y"],
        ["4", "", ""],
        ["5", "", ""],
        ["6", "4", "X"]
    );

    internal static readonly IStoredSchemaDataSet Second = new InlineTable(
        "c",
        "second",
        ("id", new LongColumnType()),
        ("n", new LongColumnType()),
        ("d", new DoubleColumnType()),
        ("s", new StringColumnType())
    ).With(
        ["1", "1", "1", "x"],
        ["2", "1", "1", "x"],
        ["3", "3", "3.5", "z"],
        ["4", "", "", ""],
        ["5", "4", "4", "x"]
    );

    internal static readonly IReadOnlyList<IStoredSchemaDataSet> Memory =
    [
        Ints,
        Decimals,
        Big,
        Grouped,
        Words,
        Patterns,
        Moments,
        Left,
        Right,
        First,
        Second,
    ];

    private readonly PostgreSqlContainer _postgres;

    private readonly NpgsqlConnection _connection = new();

    private readonly Lazy<IReadOnlyList<IStoredSchemaDataSet>> _stored;

    public DivergenceFixtures()
    {
        _postgres = new PostgreSqlBuilder("postgres")
            .WithDatabase("testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        _stored = new Lazy<IReadOnlyList<IStoredSchemaDataSet>>(Started);
    }

    public IReadOnlyList<IStoredSchemaDataSet> Stored => _stored.Value;

    public void Dispose()
    {
        _connection.Dispose();
        _postgres.DisposeAsync().AsTask().GetAwaiter().GetResult();
    }

    private IReadOnlyList<IStoredSchemaDataSet> Started()
    {
        _postgres.StartAsync().GetAwaiter().GetResult();

        _connection.ConnectionString = _postgres.GetConnectionString();
        _connection.Open();

        return [.. Memory.Select(Store)];
    }

    private IStoredSchemaDataSet Store(IStoredSchemaDataSet dataset)
    {
        string schema = Name(new SchemaHash(dataset.Schema));
        Execute($"CREATE SCHEMA {schema};");

        foreach (KeyValuePair<ITable, IStoredTableDataSet> table in dataset)
        {
            string name = $"{schema}.{Name(new TableHash(table.Key))}";
            IColumn[] columns = [.. table.Key.Columns];

            Execute(
                $"CREATE TABLE {name} ("
                    + string.Join(
                        ", ",
                        columns.Select(column =>
                            $"{Name(new ColumnHash(column))} {SqlType(column)}"
                        )
                    )
                    + ");"
            );

            foreach (IRow row in table.Value)
            {
                Insert(name, columns, row);
            }
        }

        return new PostgreSqlStoredSchemaDataSet(dataset.Schema, _connection);
    }

    private void Insert(string table, IColumn[] columns, IRow row)
    {
        using NpgsqlCommand command = _connection.CreateCommand();

        command.CommandText =
            $"INSERT INTO {table} ("
            + string.Join(", ", columns.Select(column => Name(new ColumnHash(column))))
            + ") VALUES ("
            + string.Join(
                ", ",
                columns.Select((column, index) => $"CAST(@p{index} AS {SqlType(column)})")
            )
            + ");";

        for (int index = 0; index < columns.Length; index++)
        {
            string text = row.Cells[columns[index]].Value.TextValue;
            _ = command.Parameters.Add(
                new NpgsqlParameter($"p{index}", NpgsqlDbType.Text)
                {
                    Value = text.Length == 0 ? DBNull.Value : text,
                }
            );
        }

        _ = command.ExecuteNonQuery();
    }

    private void Execute(string statement)
    {
        using NpgsqlCommand command = _connection.CreateCommand();
        command.CommandText = statement;
        _ = command.ExecuteNonQuery();
    }

    private static string Name(IEnumerable<byte> hash)
    {
        return $"\"{new HexString(hash).TextValue[..MaxIdentifierLength]}\"";
    }

    private static string SqlType(IColumn column)
    {
        return column.Type.Name.TextValue switch
        {
            "long" => "bigint",
            "bool" => "boolean",
            "string" => "text",
            "date" => "date",
            "time" => "time",
            "datetime" => "timestamp",
            "double" => "double precision",
            "uuid" => "uuid",
            _ => throw new NotSupportedException(
                $"Column type {column.Type.Name.TextValue}."
            ),
        };
    }
}
