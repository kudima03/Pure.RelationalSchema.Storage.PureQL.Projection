using System.Diagnostics;
using System.Globalization;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// A sum, a mean and a count over a frame are read in memory from the running
// sums of the partition, not folded from the frame's rows for every row. They
// are the values folding gives, for every kind of frame, with and without a
// predicate and with null values, and a running total over many rows costs
// its rows, not their square.
public sealed class WindowRunningSumTests
{
    private const int Count = 240;

    private static readonly FieldInteger Id = new("t.rows", "id");

    private static readonly FieldInteger Group = new("t.rows", "g");

    private static readonly FieldIntegerNullable Number = new("t.rows", "n");

    private static readonly FieldDecimalNullable Amount = new("t.rows", "d");

    // Row i: group i mod 3, an integer that is null every seventh row and
    // negative every fourth, a decimal that is null every fifth row.
    private static readonly (long Id, long Group, long? Number, decimal? Amount)[] Rows =
    [
        .. Enumerable
            .Range(1, Count)
            .Select(i =>
                (
                    (long)i,
                    (long)(i % 3),
                    i % 7 == 0 ? (long?)null : (i % 4 == 0 ? -i : i * 3L),
                    i % 5 == 0 ? (decimal?)null : (i * 1.25m) + (i % 9 * 0.001m)
                )
            ),
    ];

    private static readonly IStoredSchemaDataSet Data = new InlineTable(
        "t",
        "rows",
        ("id", new LongColumnType()),
        ("g", new LongColumnType()),
        ("n", new LongColumnType()),
        ("d", new DoubleColumnType())
    ).With([
        .. Rows.Select(row =>
            new[] { Text(row.Id), Text(row.Group), Text(row.Number), Text(row.Amount) }
        ),
    ]);

    public static TheoryData<int, int> Frames =>
        new()
        {
            { int.MaxValue, 0 },
            { 0, int.MaxValue },
            { 6, 0 },
            { 2, 3 },
            { 0, 1 },
            { int.MaxValue, 5 },
        };

    [Theory]
    [MemberData(nameof(Frames))]
    public void SumsMeansAndCountsOverAFrameAreThoseOfItsRows(
        int preceding,
        int following
    )
    {
        Window window = new WindowWithFrame(
            orderBy: [new OrderItemRow(Id)],
            frame: new Frame(Side(preceding), Side(following)),
            partitionBy: [Group]
        );
        BooleanRow positive = new GreaterThanDecimalRow(Number, new LiteralInteger(0));

        PureQLProjection projection = new(
            [Data],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.rows"),
                select:
                [
                    new SelectItemProjection("id", Id),
                    new SelectItemProjection(
                        "integers",
                        new SumIntegerProjection(Number, over: window)
                    ),
                    new SelectItemProjection(
                        "decimals",
                        new SumDecimalProjection(Amount, over: window)
                    ),
                    new SelectItemProjection(
                        "kept",
                        new SumDecimalProjection(Amount, positive, window)
                    ),
                    new SelectItemProjection(
                        "mean",
                        new AverageDecimalNullableProjection(Amount, over: window)
                    ),
                    new SelectItemProjection("rows", new CountProjection(over: window)),
                    new SelectItemProjection(
                        "matching",
                        new CountProjection(positive, window)
                    ),
                ],
                orderBy: [new OrderItemProjectionColumn("id")]
            )
        );

        List<IRow> actual = [.. projection.AsEnumerable()];

        foreach ((long Id, long Group, long? Number, decimal? Amount) row in Rows)
        {
            List<(long Id, long Group, long? Number, decimal? Amount)> partition =
            [
                .. Rows.Where(other => other.Group == row.Group),
            ];
            int index = partition.IndexOf(row);
            int first = (int)Math.Max(0L, index - (long)preceding);
            int last = (int)Math.Min(partition.Count - 1L, index + (long)following);
            List<(long Id, long Group, long? Number, decimal? Amount)> frame = partition[
                first..(last + 1)
            ];
            List<decimal> amounts =
            [
                .. frame.Select(other => other.Amount).OfType<decimal>(),
            ];
            string at = $"row {row.Id}";

            Assert.True(
                Text(frame.Sum(other => other.Number ?? 0))
                    == Cell(actual, row.Id, "integers"),
                at
            );
            Assert.True(Text(amounts.Sum()) == Cell(actual, row.Id, "decimals"), at);
            Assert.True(
                Text(
                    frame
                        .Where(other => other.Number > 0)
                        .Sum(other => other.Amount ?? 0m)
                ) == Cell(actual, row.Id, "kept"),
                at
            );
            Assert.True(
                (
                    amounts.Count == 0
                        ? string.Empty
                        : Text(PureQLMath.Divide(amounts.Sum(), amounts.Count))
                ) == Cell(actual, row.Id, "mean"),
                at
            );
            Assert.True(Text(frame.Count) == Cell(actual, row.Id, "rows"), at);
            Assert.True(
                Text(frame.Count(other => other.Number > 0))
                    == Cell(actual, row.Id, "matching"),
                at
            );
        }
    }

    [Fact]
    public void RunningTotalOverManyRowsCostsItsRowsNotTheirSquare()
    {
        const int many = 30000;
        IStoredSchemaDataSet data = new InlineTable(
            "t",
            "rows",
            ("id", new LongColumnType()),
            ("d", new DoubleColumnType())
        ).With([
            .. Enumerable
                .Range(1, many)
                .Select(i => new[] { Text(i), Text(i * 0.5m) }),
        ]);
        PureQLProjection projection = new(
            [data],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.rows"),
                select:
                [
                    new SelectItemProjection(
                        "running",
                        new SumDecimalProjection(
                            new FieldDecimal("t.rows", "d"),
                            over: new WindowWithFrame(
                                [new OrderItemRow(Id)],
                                new Frame(preceding: new All())
                            )
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        Stopwatch watch = Stopwatch.StartNew();
        IReadOnlyList<string> running = ResultColumn.Texts(projection, "running");

        Assert.Equal(many, running.Count);
        Assert.Equal(Text(many * (many + 1L) / 4m), running[^1]);
        Assert.True(
            watch.Elapsed < TimeSpan.FromSeconds(10),
            $"A running total over {many} rows took {watch.Elapsed}."
        );
    }

    private static FrameRows? Side(int rows)
    {
        return rows == 0 ? null
            : rows == int.MaxValue ? new All()
            : rows;
    }

    private static string Cell(List<IRow> rows, long id, string column)
    {
        return rows[(int)id - 1]
            .Cells.Single(cell => cell.Key.Name.TextValue == column)
            .Value.Value.TextValue;
    }

    private static string Text(long? value)
    {
        return value?.ToString(CultureInfo.InvariantCulture) ?? string.Empty;
    }

    private static string Text(decimal? value)
    {
        return value is decimal number
            ? PureQLConversions.AsString(number)
            : string.Empty;
    }
}
