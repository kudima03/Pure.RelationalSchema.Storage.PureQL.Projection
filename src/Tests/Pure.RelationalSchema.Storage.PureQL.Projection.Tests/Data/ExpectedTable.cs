using System.Globalization;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Storage.Abstractions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// Compares a projection with an expected table: the same columns (names and
// types, in order) and the same rows. The samples type a decimal column
// double and hold its expected value rounded and converted to double, so a
// decimal column is compared as the samples' double, to 12 significant
// digits; datetime cells compare as instants. Rows compare in order when the
// query orders them, as a multiset otherwise.
internal sealed class ExpectedTable(IStoredTableDataSet expected)
{
    private readonly IStoredTableDataSet _expected = expected;

    public void AssertOrdered(IStoredTableDataSet actual)
    {
        AssertColumns(actual);
        Assert.Equal(Rows(_expected), Rows(actual));
    }

    public void AssertUnordered(IStoredTableDataSet actual)
    {
        AssertColumns(actual);
        Assert.Equal(
            Rows(_expected).Order(StringComparer.Ordinal),
            Rows(actual).Order(StringComparer.Ordinal)
        );
    }

    private void AssertColumns(IStoredTableDataSet actual)
    {
        Assert.Equal(Columns(_expected), Columns(actual));
    }

    private static List<string> Columns(IStoredTableDataSet table)
    {
        return
        [
            .. table.TableSchema.Columns.Select(column =>
                $"{column.Name.TextValue}:{Type(column.Type.Name.TextValue)}"
            ),
        ];
    }

    private static string Type(string columnType)
    {
        return columnType == "decimal" ? "double" : columnType;
    }

    private static List<string> Rows(IStoredTableDataSet table)
    {
        List<IColumn> columns = [.. table.TableSchema.Columns];

        return
        [
            .. table
                .AsEnumerable()
                .Select(row =>
                    string.Join(
                        " | ",
                        columns.Select(column =>
                            Canonical(
                                row.Cells.Single(cell =>
                                        cell.Key.Name.TextValue == column.Name.TextValue
                                    )
                                    .Value.Value.TextValue,
                                column.Type.Name.TextValue
                            )
                        )
                    )
                ),
        ];
    }

    private static string Canonical(string text, string columnType)
    {
        return text.Length == 0
            ? "NULL"
            : columnType switch
            {
                "double" or "decimal" => double
                    .Parse(text, CultureInfo.InvariantCulture)
                    .ToString("G12", CultureInfo.InvariantCulture),
                "datetime" => DateTime
                    .Parse(
                        text,
                        CultureInfo.InvariantCulture,
                        DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal
                    )
                    .ToString("O", CultureInfo.InvariantCulture),
                _ => text,
            };
    }
}
