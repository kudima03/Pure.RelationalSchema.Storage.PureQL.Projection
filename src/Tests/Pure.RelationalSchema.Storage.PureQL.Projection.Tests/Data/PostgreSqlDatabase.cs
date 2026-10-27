using Npgsql;
using NpgsqlTypes;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.HashCodes;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PostgreSQL.Querying;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using Testcontainers.PostgreSql;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// The Pure.RelationalSchema.Storage.Samples fixtures the sample catalogue
// reads, stored in a PostgreSQL database and read through the provider of
// Pure.RelationalSchema.Storage.PostgreSQL.Querying, which translates a query
// to one SQL statement or refuses it. The tables are created and filled here,
// not by the Creation and Insertion packages: those declare every column NOT
// NULL and write an empty cell as '', while the fixtures hold NULLs (empty
// cells) in columns of every type. Objects are named as those packages name
// them: the first 63 hexadecimal characters of the model's hash. The database
// starts when the datasets are first read, so a class whose tests are all
// skipped needs no Docker.
public sealed class PostgreSqlDatabase : IDisposable
{
    private const int MaxIdentifierLength = 63;

    private readonly PostgreSqlContainer _postgres;

    private readonly NpgsqlConnection _connection = new();

    private readonly Lazy<IReadOnlyList<IStoredSchemaDataSet>> _datasets;

    public PostgreSqlDatabase()
    {
        _postgres = new PostgreSqlBuilder("postgres")
            .WithDatabase("testdb")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

        _datasets = new Lazy<IReadOnlyList<IStoredSchemaDataSet>>(Started);
    }

    public IReadOnlyList<IStoredSchemaDataSet> Datasets => _datasets.Value;

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

        return [Stored(new SchemaDataSetWithForeignKeys()), Stored(new AuditSchemaDataSet())];
    }

    // The dataset's tables, created and filled with its rows, read back from the
    // database.
    private IStoredSchemaDataSet Stored(IStoredSchemaDataSet dataset)
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

    // A row, each cell's text cast to its column's type; an empty cell is NULL.
    private void Insert(string table, IColumn[] columns, IRow row)
    {
        using NpgsqlCommand command = _connection.CreateCommand();

        command.CommandText =
            $"INSERT INTO {table} ("
            + string.Join(", ", columns.Select(column => Name(new ColumnHash(column))))
            + ") VALUES ("
            + string.Join(
                ", ",
                columns.Select(
                    (column, index) => $"CAST(@p{index} AS {SqlType(column)})"
                )
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

    // The column's type as the Creation package declares it.
    private static string SqlType(IColumn column)
    {
        return column.Type.Name.TextValue switch
        {
            "int" or "ushort" => "integer",
            "long" or "uint" => "bigint",
            "ulong" => "numeric",
            "bool" => "boolean",
            "string" => "text",
            "date" => "date",
            "time" => "time",
            "datetime" => "timestamp",
            "double" => "double precision",
            "float" => "real",
            "uuid" => "uuid",
            "determined_hash" => "bytea",
            _ => throw new NotSupportedException(
                $"Column type {column.Type.Name.TextValue}."
            ),
        };
    }
}
