using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.GroupExpressions;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.TotalExpressions;
using IParameter = Pure.RelationalSchema.Storage.PureQL.Projection.Parameters.Abstractions.Parameter.IParameter;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Edge cases of the aggregates of the specification's "Aggregates" and
// "Aggregate evaluation" sections: predicates that leave no row, integer sums
// whose partial sums leave 64 bits, means of integers, min / max of every
// type, strings ordered by code point above the BMP, first / last with null
// and tied keys, concatAll with empty strings and repeats far apart, over:
// all against where and having, having over the new aggregates, aggregates
// over an empty table, over a paged or deduplicated subquery, over a combined
// query and over an outer join. Every grouped column is read in the order of
// its key g.
public sealed class AggregateEdgeTests
{
    private const string Max = "9223372036854775807";

    // a: a sum whose partial sum MAX + 1 leaves 64 bits; b: small values.
    private static readonly IStoredSchemaDataSet Orders = new InlineTable(
        "t",
        "orders",
        ("id", new LongColumnType()),
        ("g", new StringColumnType()),
        ("qty", new LongColumnType()),
        ("price", new DoubleColumnType()),
        ("note", new StringColumnType()),
        ("day", new DateColumnType())
    ).With(
        ["1", "a", Max, "1.5", "x", "2024-01-01"],
        ["2", "a", "1", "2", "", "2024-01-03"],
        ["3", "a", "-1", "", "y", ""],
        ["4", "b", "1", "0.1", "x", "2024-02-01"],
        ["5", "b", "2", "0.2", "", "2024-02-02"]
    );

    private static readonly IStoredSchemaDataSet Tags = new InlineTable(
        "t",
        "tags",
        ("order_id", new LongColumnType()),
        ("tag", new StringColumnType())
    ).With(["1", "p"], ["1", "q"], ["4", "p"]);

    // Strings that UTF-16 code units order differently from code points:
    // U+FFFF comes before U+10000 by code point, after it by code unit.
    private static readonly IStoredSchemaDataSet Words = new InlineTable(
        "t",
        "words",
        ("id", new LongColumnType()),
        ("g", new StringColumnType()),
        ("s", new StringColumnType())
    ).With(
        ["1", "a", "￿"],
        ["2", "a", "\U00010000"],
        ["3", "a", "z"],
        ["4", "b", "\U00010001"],
        ["5", "b", "\U00010000"],
        ["6", "b", "\U00010000"]
    );

    // A null key, a value that is null, and two rows tied on k.
    private static readonly IStoredSchemaDataSet Events = new InlineTable(
        "t",
        "events",
        ("id", new LongColumnType()),
        ("k", new LongColumnType()),
        ("v", new StringColumnType())
    ).With(
        ["1", "2", "two"],
        ["2", "", "none"],
        ["3", "1", ""],
        ["4", "3", "three"],
        ["5", "3", "later"]
    );

    // Non-null strings: an empty cell is the empty string.
    private static readonly IStoredSchemaDataSet Notes = new InlineTable(
        "t",
        "notes",
        ("id", new LongColumnType()),
        ("s", new StringColumnType())
    ).With(["1", "x"], ["2", ""], ["3", "y"], ["4", "x"], ["5", ""]);

    // The extremes of every type, a datetime stored with an offset, and uuids
    // whose first digit is 7 and 8.
    private static readonly IStoredSchemaDataSet Kinds = new InlineTable(
        "t",
        "kinds",
        ("id", new LongColumnType()),
        ("n", new LongColumnType()),
        ("d", new DoubleColumnType()),
        ("b", new BoolColumnType()),
        ("day", new DateColumnType()),
        ("t", new TimeColumnType()),
        ("at", new DateTimeColumnType()),
        ("u", new UuidColumnType())
    ).With(
        [
            "1",
            "-9223372036854775808",
            "1.50",
            "true",
            "0001-01-01",
            "23:59:59.999999",
            "2024-01-01T03:00:00+03:00",
            "7fffffff-ffff-ffff-ffff-ffffffffffff",
        ],
        [
            "2",
            Max,
            "-0.0000000000000000000000000001",
            "false",
            "9999-12-31",
            "00:00:00",
            "2023-12-31T23:59:59.999999Z",
            "80000000-0000-0000-0000-000000000000",
        ],
        [
            "3",
            "0",
            "9999999999999999999999999999",
            "",
            "2024-02-29",
            "12:00:00",
            "2024-01-01T00:00:00.000001Z",
            "00000000-0000-0000-8000-000000000000",
        ]
    );

    // One instant written three ways, and another one.
    private static readonly IStoredSchemaDataSet Instants = new InlineTable(
        "t",
        "instants",
        ("id", new LongColumnType()),
        ("at", new DateTimeColumnType())
    ).With(
        ["1", "2024-01-01T03:00:00+03:00"],
        ["2", "2024-01-01T00:00:00Z"],
        ["3", "2023-12-31T19:00:00-05:00"],
        ["4", "2023-06-01T00:00:00Z"]
    );

    private static readonly IStoredSchemaDataSet Empty = new InlineTable(
        "t",
        "empty",
        ("id", new LongColumnType()),
        ("g", new StringColumnType()),
        ("x", new DoubleColumnType()),
        ("s", new StringColumnType())
    ).With();

    private static readonly IStoredSchemaDataSet Left = new InlineTable(
        "t",
        "left",
        ("v", new LongColumnType())
    ).With(["1"], ["2"], ["2"]);

    private static readonly IStoredSchemaDataSet Right = new InlineTable(
        "t",
        "right",
        ("w", new DoubleColumnType())
    ).With(["1.0"], ["3"], [""]);

    private static readonly FieldInteger Id = new("t.orders", "id");

    private static readonly FieldString G = new("t.orders", "g");

    private static readonly FieldInteger Qty = new("t.orders", "qty");

    private static readonly FieldDecimalNullable Price = new("t.orders", "price");

    private static readonly FieldStringNullable Note = new("t.orders", "note");

    private static readonly FieldDateNullable Day = new("t.orders", "day");

    private static readonly EqualStringRow InA = new(G, new LiteralString("a"));

    private static readonly EqualStringRow InZ = new(G, new LiteralString("z"));

    private static readonly FieldInteger WordId = new("t.words", "id");

    private static readonly FieldString Word = new("t.words", "s");

    private static readonly FieldInteger EventId = new("t.events", "id");

    private static readonly FieldIntegerNullable EventKey = new("t.events", "k");

    private static readonly FieldStringNullable EventValue = new("t.events", "v");

    private static readonly FieldInteger NoteId = new("t.notes", "id");

    private static readonly FieldString NoteText = new("t.notes", "s");

    [Fact]
    public void CountSumAndCountDistinctOfAGroupWhosePredicateKeepsNoRowAreZero()
    {
        PureQLProjection projection = Grouped(
            Orders,
            [
                new SelectItemGroup("count", new CountGroup(InA)),
                new SelectItemGroup("units", new SumIntegerGroup(Qty, InA)),
                new SelectItemGroup("revenue", new SumDecimalGroup(Price, InA)),
                new SelectItemGroup("notes", new CountDistinctGroup(Note, InA)),
                new SelectItemGroup(
                    "joined",
                    new ConcatAllGroup(
                        Note,
                        new LiteralString(","),
                        [new OrderItemRow(Id)],
                        InA
                    )
                ),
                new SelectItemGroup("any", new AnyGroup(InA)),
            ]
        );

        Assert.Equal(["3", "0"], ResultColumn.Texts(projection, "count"));
        Assert.Equal([Max, "0"], ResultColumn.Texts(projection, "units"));
        Assert.Equal(["3.5", "0"], ResultColumn.Texts(projection, "revenue"));
        Assert.Equal(["2", "0"], ResultColumn.Texts(projection, "notes"));
        Assert.Equal(["x,y", ""], ResultColumn.Texts(projection, "joined"));
        Assert.Equal(["True", "False"], ResultColumn.Texts(projection, "any"));
    }

    [Fact]
    public void ValueAggregatesOfAGroupWhosePredicateKeepsNoRowAreNull()
    {
        PureQLProjection projection = Grouped(
            Orders,
            [
                new SelectItemGroup("mean", new AverageDecimalNullableGroup(Price, InA)),
                new SelectItemGroup("least", new MinIntegerNullableGroup(Qty, InA)),
                new SelectItemGroup("latest", new MaxDateNullableGroup(Day, InA)),
                new SelectItemGroup(
                    "first",
                    new FirstStringNullableGroup(Note, [new OrderItemRow(Id)], InA)
                ),
                new SelectItemGroup(
                    "last",
                    new LastIntegerNullableGroup(Qty, [new OrderItemRow(Id)], InA)
                ),
            ]
        );

        Assert.Equal(["1.75", ""], ResultColumn.Texts(projection, "mean"));
        Assert.Equal(["-1", ""], ResultColumn.Texts(projection, "least"));
        Assert.Equal(["2024-01-03", ""], ResultColumn.Texts(projection, "latest"));
        Assert.Equal(["x", ""], ResultColumn.Texts(projection, "first"));
        Assert.Equal(["-1", ""], ResultColumn.Texts(projection, "last"));
    }

    [Fact]
    public void TotalQueryAggregatesWithAPredicateThatKeepsNoRow()
    {
        PureQLProjection projection = Total(
            Orders,
            [
                new SelectItemTotal("count", new CountTotal(InZ)),
                new SelectItemTotal("units", new SumIntegerTotal(Qty, InZ)),
                new SelectItemTotal("revenue", new SumDecimalTotal(Price, InZ)),
                new SelectItemTotal("notes", new CountDistinctTotal(Note, InZ)),
                new SelectItemTotal(
                    "joined",
                    new ConcatAllTotal(
                        Note,
                        new LiteralString(","),
                        [new OrderItemRow(Id)],
                        InZ
                    )
                ),
                new SelectItemTotal("mean", new AverageDecimalNullableTotal(Price, InZ)),
                new SelectItemTotal("least", new MinIntegerNullableTotal(Qty, InZ)),
                new SelectItemTotal(
                    "first",
                    new FirstIntegerNullableTotal(Qty, [new OrderItemRow(Id)], InZ)
                ),
                new SelectItemTotal("any", new AnyTotal(InZ)),
                new SelectItemTotal("all", new AllTotal(InZ)),
            ]
        );

        Assert.Equal(["0"], ResultColumn.Texts(projection, "count"));
        Assert.Equal(["0"], ResultColumn.Texts(projection, "units"));
        Assert.Equal(["0"], ResultColumn.Texts(projection, "revenue"));
        Assert.Equal(["0"], ResultColumn.Texts(projection, "notes"));
        Assert.Equal([""], ResultColumn.Texts(projection, "joined"));
        Assert.Equal([""], ResultColumn.Texts(projection, "mean"));
        Assert.Equal([""], ResultColumn.Texts(projection, "least"));
        Assert.Equal([""], ResultColumn.Texts(projection, "first"));
        Assert.Equal(["False"], ResultColumn.Texts(projection, "any"));
        Assert.Equal(["False"], ResultColumn.Texts(projection, "all"));
    }

    [Fact]
    public void PlainQueryAggregatesWithAPredicateThatKeepsNoRow()
    {
        PureQLProjection projection = Plain(
            Orders,
            [
                new SelectItemProjection("count", new CountProjection(InZ)),
                new SelectItemProjection("units", new SumIntegerProjection(Qty, InZ)),
                new SelectItemProjection(
                    "notes",
                    new CountDistinctProjection(Note, InZ)
                ),
                new SelectItemProjection(
                    "joined",
                    new ConcatAllProjection(
                        Note,
                        new LiteralString(","),
                        [new OrderItemRow(Id)],
                        InZ
                    )
                ),
                new SelectItemProjection(
                    "mean",
                    new AverageDecimalNullableProjection(Price, InZ)
                ),
            ]
        );

        Assert.Equal(Enumerable.Repeat("0", 5), ResultColumn.Texts(projection, "count"));
        Assert.Equal(Enumerable.Repeat("0", 5), ResultColumn.Texts(projection, "units"));
        Assert.Equal(Enumerable.Repeat("0", 5), ResultColumn.Texts(projection, "notes"));
        Assert.Equal(Enumerable.Repeat("", 5), ResultColumn.Texts(projection, "joined"));
        Assert.Equal(Enumerable.Repeat("", 5), ResultColumn.Texts(projection, "mean"));
    }

    // "sum of the integer values 9223372036854775807, 1 and -1 is
    // 9223372036854775807, whatever the order of the rows" (Integer and
    // decimal arithmetic, Aggregates), over a group, all rows and a total.
    [Fact]
    public void IntegerSumIgnoresPartialSumsBeyondSixtyFourBitsInEveryContext()
    {
        PureQLProjection grouped = Grouped(
            Orders,
            [new SelectItemGroup("units", new SumIntegerGroup(Qty))]
        );
        PureQLProjection total = Total(
            Orders,
            [new SelectItemTotal("units", new SumIntegerTotal(Qty))],
            InA
        );
        PureQLProjection predicated = Total(
            Orders,
            [new SelectItemTotal("units", new SumIntegerTotal(Qty, InA))]
        );
        PureQLProjection plain = Plain(
            Orders,
            [new SelectItemProjection("units", new SumIntegerProjection(Qty))],
            InA
        );

        Assert.Equal([Max, "3"], ResultColumn.Texts(grouped, "units"));
        Assert.Equal([Max], ResultColumn.Texts(total, "units"));
        Assert.Equal([Max], ResultColumn.Texts(predicated, "units"));
        Assert.Equal([Max, Max, Max], ResultColumn.Texts(plain, "units"));
    }

    [Fact]
    public void IntegerSumOverAllBeyondSixtyFourBitsFailsTheGroupedQuery()
    {
        PureQLProjection projection = Grouped(
            Orders,
            [
                new SelectItemGroup(
                    "units",
                    new SumIntegerGroup(Qty, over: AggregateOver.All)
                ),
            ]
        );

        _ = Assert.Throws<OverflowException>(() => projection.ToList());
    }

    // average: "Numbers: sum divided as by divide", from exact values: the
    // sum of a's integers leaves 64 bits on the way, that of all rows at the
    // end, and the means are still exact.
    [Fact]
    public void AverageOfIntegersIsTheExactSumDividedOnce()
    {
        PureQLProjection projection = Grouped(
            Orders,
            [
                new SelectItemGroup("mean", new AverageDecimalGroup(Qty)),
                new SelectItemGroup(
                    "overall",
                    new AverageDecimalGroup(Qty, AggregateOver.All)
                ),
            ]
        );

        Assert.Equal(
            ["3074457345618258602.333333333", "1.5"],
            ResultColumn.Texts(projection, "mean")
        );
        Assert.Equal(
            ["1844674407370955162", "1844674407370955162"],
            ResultColumn.Texts(projection, "overall")
        );
    }

    [Fact]
    public void MinAndMaxOfEveryTypeFollowTheOrderOfTheType()
    {
        const string entity = "t.kinds";
        PureQLProjection projection = Total(
            Kinds,
            [
                new SelectItemTotal(
                    "n_min",
                    new MinIntegerNullableTotal(new FieldInteger(entity, "n"))
                ),
                new SelectItemTotal(
                    "n_max",
                    new MaxIntegerNullableTotal(new FieldInteger(entity, "n"))
                ),
                new SelectItemTotal(
                    "d_min",
                    new MinDecimalNullableTotal(new FieldDecimal(entity, "d"))
                ),
                new SelectItemTotal(
                    "d_max",
                    new MaxDecimalNullableTotal(new FieldDecimal(entity, "d"))
                ),
                new SelectItemTotal(
                    "b_min",
                    new MinBooleanNullableTotal(new FieldBooleanNullable(entity, "b"))
                ),
                new SelectItemTotal(
                    "b_max",
                    new MaxBooleanNullableTotal(new FieldBooleanNullable(entity, "b"))
                ),
                new SelectItemTotal(
                    "day_min",
                    new MinDateNullableTotal(new FieldDate(entity, "day"))
                ),
                new SelectItemTotal(
                    "day_max",
                    new MaxDateNullableTotal(new FieldDate(entity, "day"))
                ),
                new SelectItemTotal(
                    "t_min",
                    new MinTimeNullableTotal(new FieldTime(entity, "t"))
                ),
                new SelectItemTotal(
                    "t_max",
                    new MaxTimeNullableTotal(new FieldTime(entity, "t"))
                ),
                new SelectItemTotal(
                    "at_min",
                    new MinDatetimeNullableTotal(new FieldDatetime(entity, "at"))
                ),
                new SelectItemTotal(
                    "at_max",
                    new MaxDatetimeNullableTotal(new FieldDatetime(entity, "at"))
                ),
                new SelectItemTotal(
                    "u_min",
                    new MinUuidNullableTotal(new FieldUuid(entity, "u"))
                ),
                new SelectItemTotal(
                    "u_max",
                    new MaxUuidNullableTotal(new FieldUuid(entity, "u"))
                ),
            ]
        );

        Assert.Equal(["-9223372036854775808"], ResultColumn.Texts(projection, "n_min"));
        Assert.Equal([Max], ResultColumn.Texts(projection, "n_max"));
        Assert.Equal(
            ["-0.0000000000000000000000000001"],
            ResultColumn.Texts(projection, "d_min")
        );
        Assert.Equal(
            ["9999999999999999999999999999"],
            ResultColumn.Texts(projection, "d_max")
        );
        Assert.Equal(["False"], ResultColumn.Texts(projection, "b_min"));
        Assert.Equal(["True"], ResultColumn.Texts(projection, "b_max"));
        Assert.Equal(["0001-01-01"], ResultColumn.Texts(projection, "day_min"));
        Assert.Equal(["9999-12-31"], ResultColumn.Texts(projection, "day_max"));
        Assert.Equal(["00:00:00"], ResultColumn.Texts(projection, "t_min"));
        Assert.Equal(["23:59:59.999999"], ResultColumn.Texts(projection, "t_max"));
        Assert.Equal(
            ["2023-12-31T23:59:59.999999Z"],
            ResultColumn.Texts(projection, "at_min")
        );
        Assert.Equal(
            ["2024-01-01T00:00:00.000001Z"],
            ResultColumn.Texts(projection, "at_max")
        );
        Assert.Equal(
            ["00000000-0000-0000-8000-000000000000"],
            ResultColumn.Texts(projection, "u_min")
        );
        Assert.Equal(
            ["80000000-0000-0000-0000-000000000000"],
            ResultColumn.Texts(projection, "u_max")
        );
    }

    [Fact]
    public void MinAndMaxOfDecimalsAreWrittenInTheirOneSpelling()
    {
        PureQLProjection projection = Total(
            Kinds,
            [
                new SelectItemTotal(
                    "d_min",
                    new MinDecimalNullableTotal(new FieldDecimal("t.kinds", "d"))
                ),
            ],
            new LessThanDecimalRow(
                new FieldInteger("t.kinds", "id"),
                new LiteralInteger(2)
            )
        );

        Assert.Equal(["1.5"], ResultColumn.Texts(projection, "d_min"));
    }

    // countDistinct: "Values are compared as equal compares them"; datetime:
    // "equal ... work[s] on the instant" (Temporal types).
    [Fact]
    public void InstantsAreCountedAndPickedByTheInstantNotByTheOffsetStored()
    {
        FieldDatetime at = new("t.instants", "at");
        PureQLProjection projection = Total(
            Instants,
            [
                new SelectItemTotal("distinct", new CountDistinctTotal(at)),
                new SelectItemTotal("mode", new ModeDatetimeNullableTotal(at)),
                new SelectItemTotal("min", new MinDatetimeNullableTotal(at)),
                new SelectItemTotal("max", new MaxDatetimeNullableTotal(at)),
            ]
        );

        Assert.Equal(["2"], ResultColumn.Texts(projection, "distinct"));
        Assert.Equal(["2024-01-01T00:00:00Z"], ResultColumn.Texts(projection, "mode"));
        Assert.Equal(["2023-06-01T00:00:00Z"], ResultColumn.Texts(projection, "min"));
        Assert.Equal(["2024-01-01T00:00:00Z"], ResultColumn.Texts(projection, "max"));
    }

    [Fact]
    public void CountDistinctOfBooleansAndUuidsSkipsNulls()
    {
        PureQLProjection projection = Total(
            Kinds,
            [
                new SelectItemTotal(
                    "flags",
                    new CountDistinctTotal(new FieldBooleanNullable("t.kinds", "b"))
                ),
                new SelectItemTotal(
                    "ids",
                    new CountDistinctTotal(new FieldUuid("t.kinds", "u"))
                ),
            ]
        );

        Assert.Equal(["2"], ResultColumn.Texts(projection, "flags"));
        Assert.Equal(["3"], ResultColumn.Texts(projection, "ids"));
    }

    // Strings: "Ordering, used by comparisons, orderBy, min / max ... is
    // lexicographic by code point".
    [Fact]
    public void MinAndMaxOfStringsOrderByCodePointAboveTheBmp()
    {
        PureQLProjection grouped = Grouped(
            Words,
            [
                new SelectItemGroup("min", new MinStringGroup(Word)),
                new SelectItemGroup("max", new MaxStringGroup(Word)),
            ]
        );
        PureQLProjection total = Total(
            Words,
            [
                new SelectItemTotal("min", new MinStringNullableTotal(Word)),
                new SelectItemTotal("max", new MaxStringNullableTotal(Word)),
            ]
        );
        PureQLProjection plain = Plain(
            Words,
            [new SelectItemProjection("max", new MaxStringProjection(Word))]
        );

        Assert.Equal(["z", "\U00010000"], ResultColumn.Texts(grouped, "min"));
        Assert.Equal(["\U00010000", "\U00010001"], ResultColumn.Texts(grouped, "max"));
        Assert.Equal(["z"], ResultColumn.Texts(total, "min"));
        Assert.Equal(["\U00010001"], ResultColumn.Texts(total, "max"));
        Assert.Equal(
            Enumerable.Repeat("\U00010001", 6),
            ResultColumn.Texts(plain, "max")
        );
    }

    [Fact]
    public void CountDistinctTellsApartStringsThatDifferOnlyAboveTheBmp()
    {
        PureQLProjection grouped = Grouped(
            Words,
            [new SelectItemGroup("distinct", new CountDistinctGroup(Word))]
        );
        PureQLProjection total = Total(
            Words,
            [new SelectItemTotal("distinct", new CountDistinctTotal(Word))]
        );

        Assert.Equal(["3", "2"], ResultColumn.Texts(grouped, "distinct"));
        Assert.Equal(["4"], ResultColumn.Texts(total, "distinct"));
    }

    [Fact]
    public void FirstAndLastOrderStringKeysByCodePoint()
    {
        PureQLProjection projection = Grouped(
            Words,
            [
                new SelectItemGroup(
                    "first",
                    new FirstIntegerGroup(
                        WordId,
                        [new OrderItemRow(Word), new OrderItemRow(WordId)]
                    )
                ),
                new SelectItemGroup(
                    "last",
                    new LastIntegerGroup(
                        WordId,
                        [new OrderItemRow(Word), new OrderItemRow(WordId)]
                    )
                ),
            ]
        );

        Assert.Equal(["3", "5"], ResultColumn.Texts(projection, "first"));
        Assert.Equal(["2", "4"], ResultColumn.Texts(projection, "last"));
    }

    [Fact]
    public void ConcatAllOrdersStringsByCodePoint()
    {
        PureQLProjection projection = Grouped(
            Words,
            [
                new SelectItemGroup(
                    "all",
                    new ConcatAllGroup(
                        Word,
                        new LiteralString(","),
                        [new OrderItemRow(Word), new OrderItemRow(WordId)]
                    )
                ),
                new SelectItemGroup(
                    "distinct",
                    new ConcatAllGroup(
                        Word,
                        new LiteralString(","),
                        [new OrderItemRow(Word, SortDirection.Desc)],
                        distinct: true
                    )
                ),
            ]
        );

        Assert.Equal(
            ["z,￿,\U00010000", "\U00010000,\U00010000,\U00010001"],
            ResultColumn.Texts(projection, "all")
        );
        Assert.Equal(
            ["\U00010000,￿,z", "\U00010001,\U00010000"],
            ResultColumn.Texts(projection, "distinct")
        );
    }

    // mode: "among several, the least by the order of the type".
    [Fact]
    public void ModeBreaksATieByCodePointAboveTheBmp()
    {
        PureQLProjection grouped = Grouped(
            Words,
            [new SelectItemGroup("mode", new ModeStringGroup(Word))]
        );
        PureQLProjection total = Total(
            Words,
            [new SelectItemTotal("mode", new ModeStringNullableTotal(Word))],
            new LessThanDecimalRow(WordId, new LiteralInteger(3))
        );

        Assert.Equal(["z", "\U00010000"], ResultColumn.Texts(grouped, "mode"));
        Assert.Equal(["￿"], ResultColumn.Texts(total, "mode"));
    }

    // first / last: "null first in ascending order, exactly as the query's
    // orderBy sorts", so null last in descending order; the keys apply in
    // turn.
    [Theory]
    [InlineData(false, SortDirection.Asc, SortDirection.Asc, "none")]
    [InlineData(true, SortDirection.Asc, SortDirection.Asc, "later")]
    [InlineData(true, SortDirection.Asc, SortDirection.Desc, "three")]
    [InlineData(false, SortDirection.Desc, SortDirection.Asc, "three")]
    [InlineData(false, SortDirection.Desc, SortDirection.Desc, "later")]
    [InlineData(true, SortDirection.Desc, SortDirection.Asc, "none")]
    public void FirstAndLastSortNullKeysAndBreakTiesWithTheNextKey(
        bool last,
        SortDirection key,
        SortDirection id,
        string expected
    )
    {
        OrderItemRow[] order = [new(EventKey, key), new(EventId, id)];
        ExprTotal aggregate = last
            ? new LastStringNullableTotal(EventValue, order)
            : new FirstStringNullableTotal(EventValue, order);

        Assert.Equal(
            [expected],
            ResultColumn.Texts(
                Total(Events, [new SelectItemTotal("v", aggregate)]),
                "v"
            )
        );
    }

    [Fact]
    public void FirstAndLastWithAPredicateSortOnlyTheRowsItKeeps()
    {
        OrderItemRow[] order = [new(EventKey), new(EventId)];
        PureQLProjection projection = Total(
            Events,
            [
                new SelectItemTotal(
                    "keyed",
                    new FirstStringNullableTotal(
                        EventValue,
                        order,
                        new NotEqualDecimalRow(EventKey, new LiteralIntegerNullable())
                    )
                ),
                new SelectItemTotal(
                    "valued",
                    new FirstStringNullableTotal(
                        EventValue,
                        order,
                        new NotEqualStringRow(EventValue, new LiteralStringNullable())
                    )
                ),
                new SelectItemTotal(
                    "below",
                    new FirstStringNullableTotal(
                        EventValue,
                        [new(EventKey, SortDirection.Desc), new(EventId)],
                        new LessThanDecimalRow(EventKey, new LiteralInteger(3))
                    )
                ),
            ]
        );

        Assert.Equal([""], ResultColumn.Texts(projection, "keyed"));
        Assert.Equal(["none"], ResultColumn.Texts(projection, "valued"));
        Assert.Equal(["two"], ResultColumn.Texts(projection, "below"));
    }

    [Fact]
    public void PlainQueryLastPlacesTheNullKeyLastInDescendingOrder()
    {
        PureQLProjection projection = new(
            [Events],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.events"),
                select:
                [
                    new SelectItemProjection(
                        "v",
                        new LastStringNullableProjection(
                            EventValue,
                            [new(EventKey, SortDirection.Desc), new(EventId)]
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(EventId)]
            )
        );

        Assert.Equal(Enumerable.Repeat("none", 5), ResultColumn.Texts(projection, "v"));
    }

    // concatAll: "An empty string is a value like any other and is joined
    // in"; with distinct "a value equal to one before it in that order is
    // dropped, so each value appears once, at its first position".
    [Fact]
    public void ConcatAllJoinsEmptyStringsAndDropsRepeatsWhereverTheyAre()
    {
        PureQLProjection projection = Total(
            Notes,
            [
                new SelectItemTotal(
                    "all",
                    new ConcatAllTotal(
                        NoteText,
                        new LiteralString(","),
                        [new OrderItemRow(NoteId)]
                    )
                ),
                new SelectItemTotal(
                    "distinct",
                    new ConcatAllTotal(
                        NoteText,
                        new LiteralString(","),
                        [new OrderItemRow(NoteId)],
                        distinct: true
                    )
                ),
                new SelectItemTotal(
                    "backwards",
                    new ConcatAllTotal(
                        NoteText,
                        new LiteralString(","),
                        [new OrderItemRow(NoteId, SortDirection.Desc)],
                        distinct: true
                    )
                ),
                new SelectItemTotal(
                    "glued",
                    new ConcatAllTotal(
                        NoteText,
                        new LiteralString(""),
                        [new OrderItemRow(NoteText), new OrderItemRow(NoteId)]
                    )
                ),
            ]
        );

        Assert.Equal(["x,,y,x,"], ResultColumn.Texts(projection, "all"));
        Assert.Equal(["x,,y"], ResultColumn.Texts(projection, "distinct"));
        Assert.Equal([",x,y"], ResultColumn.Texts(projection, "backwards"));
        Assert.Equal(["xxy"], ResultColumn.Texts(projection, "glued"));
    }

    [Fact]
    public void ConcatAllReadsItsSeparatorFromAParameter()
    {
        PureQLProjection projection = new(
            [Notes],
            new MainTotalQuery(
                from: new FromEntity("t.notes"),
                aggregate:
                [
                    new SelectItemTotal(
                        "all",
                        new ConcatAllTotal(
                            NoteText,
                            new ParamString("separator"),
                            [new OrderItemRow(NoteId)]
                        )
                    ),
                ]
            ),
            new Dictionary<string, IParameter>
            {
                ["separator"] = new TextParameter("string", " \U0001F600 "),
            }
        );

        Assert.Equal(
            ["x \U0001F600  \U0001F600 y \U0001F600 x \U0001F600 "],
            ResultColumn.Texts(projection, "all")
        );
    }

    // "An aggregate sees ... every row left by where (... with over:
    // "all")"; having keeps groups, it does not remove rows.
    [Fact]
    public void AggregatesOverAllFollowWhereButNotHaving()
    {
        PureQLProjection projection = Grouped(
            Orders,
            [
                new SelectItemGroup("g", new KeyString("g")),
                new SelectItemGroup("rows", new CountGroup()),
                new SelectItemGroup("all_rows", new CountGroup(over: AggregateOver.All)),
                new SelectItemGroup(
                    "all_qty",
                    new CountDistinctGroup(Qty, over: AggregateOver.All)
                ),
                new SelectItemGroup(
                    "all_g",
                    new ConcatAllGroup(
                        G,
                        new LiteralString(","),
                        [new OrderItemRow(Id)],
                        over: AggregateOver.All
                    )
                ),
            ],
            new GreaterThanDecimalRow(Qty, new LiteralInteger(0)),
            new EqualStringGroup(new KeyString("g"), new LiteralString("b"))
        );

        Assert.Equal(["b"], ResultColumn.Texts(projection, "g"));
        Assert.Equal(["2"], ResultColumn.Texts(projection, "rows"));
        Assert.Equal(["4"], ResultColumn.Texts(projection, "all_rows"));
        Assert.Equal(["3"], ResultColumn.Texts(projection, "all_qty"));
        Assert.Equal(["a,a,b,b"], ResultColumn.Texts(projection, "all_g"));
    }

    public static TheoryData<string, string> Havings()
    {
        return new TheoryData<string, string>
        {
            { "concatAll", "a" },
            { "median", "b" },
            { "varianceSample", "a" },
            { "countDistinct", "b" },
            { "first", "b" },
            { "sum", "a" },
        };
    }

    [Theory]
    [MemberData(nameof(Havings))]
    public void HavingKeepsTheGroupsByTheNewAggregates(string aggregate, string kept)
    {
        BooleanGroup having = aggregate switch
        {
            "concatAll" => new EqualStringGroup(
                new ConcatAllGroup(Note, new LiteralString("|"), [new OrderItemRow(Id)]),
                new LiteralString("x|y")
            ),
            "median" => new EqualDecimalGroup(
                new MedianDecimalNullableGroup(Price),
                new LiteralDecimal(0.15m)
            ),
            "varianceSample" => new GreaterThanDecimalGroup(
                new VarianceSampleGroup(Price),
                new LiteralDecimal(0.1m)
            ),
            "countDistinct" => new EqualDecimalGroup(
                new CountDistinctGroup(Note),
                new LiteralInteger(1)
            ),
            "first" => new EqualDecimalGroup(
                new FirstIntegerGroup(
                    Qty,
                    [new OrderItemRow(Price, SortDirection.Desc), new OrderItemRow(Id)]
                ),
                new LiteralInteger(2)
            ),
            _ => new EqualDecimalGroup(
                new SumIntegerGroup(Qty),
                new LiteralInteger(long.MaxValue)
            ),
        };

        PureQLProjection projection = Grouped(
            Orders,
            [new SelectItemGroup("g", new KeyString("g"))],
            having: having
        );

        Assert.Equal([kept], ResultColumn.Texts(projection, "g"));
    }

    // A null aggregate in having compares as null does: "null == null is
    // true", and with two nulls greaterThanOrEqual and lessThanOrEqual hold
    // while greaterThan does not (Null semantics). The median of a's rows is
    // null for b, which has none.
    [Theory]
    [InlineData("equal", "b")]
    [InlineData("notEqual", "a")]
    [InlineData("greaterThanOrEqual", "b")]
    [InlineData("lessThanOrEqual", "b")]
    [InlineData("greaterThan", "")]
    public void HavingComparesANullAggregateWithNull(string comparison, string kept)
    {
        MedianDecimalNullableGroup median = new(Price, InA);
        LiteralDecimalNullable none = new();
        BooleanGroup having = comparison switch
        {
            "equal" => new EqualDecimalGroup(median, none),
            "notEqual" => new NotEqualDecimalGroup(median, none),
            "greaterThanOrEqual" => new GreaterThanOrEqualDecimalGroup(median, none),
            "lessThanOrEqual" => new LessThanOrEqualDecimalGroup(none, median),
            _ => new GreaterThanDecimalGroup(median, none),
        };

        PureQLProjection projection = Grouped(
            Orders,
            [new SelectItemGroup("g", new KeyString("g"))],
            having: having
        );

        Assert.Equal(
            kept.Length == 0 ? [] : [kept],
            ResultColumn.Texts(projection, "g")
        );
    }

    // A selector that reads no field is still read once per row the
    // aggregate sees: over no rows it gives what no rows give.
    [Fact]
    public void AggregatesOfALiteralSelectorReadEveryRowTheySee()
    {
        PureQLProjection grouped = Grouped(
            Orders,
            [
                new SelectItemGroup("sum", new SumIntegerGroup(new LiteralInteger(2))),
                new SelectItemGroup(
                    "distinct",
                    new CountDistinctGroup(new LiteralString("x"))
                ),
                new SelectItemGroup(
                    "nulls",
                    new CountDistinctGroup(new LiteralStringNullable())
                ),
                new SelectItemGroup(
                    "joined",
                    new ConcatAllGroup(
                        new LiteralString("x"),
                        new LiteralString("-"),
                        [new OrderItemRow(Id)]
                    )
                ),
                new SelectItemGroup(
                    "variance",
                    new VariancePopulationDecimalGroup(new LiteralInteger(7))
                ),
                new SelectItemGroup(
                    "dropped",
                    new SumIntegerGroup(new LiteralInteger(2), InA)
                ),
            ]
        );
        PureQLProjection total = Total(
            Orders,
            [
                new SelectItemTotal("sum", new SumIntegerTotal(new LiteralInteger(2))),
                new SelectItemTotal(
                    "distinct",
                    new CountDistinctTotal(new LiteralString("x"))
                ),
                new SelectItemTotal(
                    "max",
                    new MaxDecimalNullableTotal(new LiteralDecimal(1.5m))
                ),
                new SelectItemTotal(
                    "joined",
                    new ConcatAllTotal(
                        new LiteralString("x"),
                        new LiteralString("-"),
                        [new OrderItemRow(Id)]
                    )
                ),
                new SelectItemTotal(
                    "median",
                    new MedianDecimalNullableTotal(new LiteralInteger(5))
                ),
                new SelectItemTotal(
                    "first",
                    new FirstIntegerNullableTotal(
                        new LiteralInteger(9),
                        [new OrderItemRow(Id)]
                    )
                ),
            ],
            InZ
        );

        Assert.Equal(["6", "4"], ResultColumn.Texts(grouped, "sum"));
        Assert.Equal(["1", "1"], ResultColumn.Texts(grouped, "distinct"));
        Assert.Equal(["0", "0"], ResultColumn.Texts(grouped, "nulls"));
        Assert.Equal(["x-x-x", "x-x"], ResultColumn.Texts(grouped, "joined"));
        Assert.Equal(["0", "0"], ResultColumn.Texts(grouped, "variance"));
        Assert.Equal(["6", "0"], ResultColumn.Texts(grouped, "dropped"));
        Assert.Equal(["0"], ResultColumn.Texts(total, "sum"));
        Assert.Equal(["0"], ResultColumn.Texts(total, "distinct"));
        Assert.Equal([""], ResultColumn.Texts(total, "max"));
        Assert.Equal([""], ResultColumn.Texts(total, "joined"));
        Assert.Equal([""], ResultColumn.Texts(total, "median"));
        Assert.Equal([""], ResultColumn.Texts(total, "first"));
    }

    // "null for a non-null parameter" is an error (Execution errors), also
    // for the separator of concatAll.
    [Fact]
    public void ASeparatorParameterBoundToNullIsAnError()
    {
        _ = Assert.ThrowsAny<ArgumentException>(() =>
            new PureQLProjection(
                [Notes],
                new MainTotalQuery(
                    from: new FromEntity("t.notes"),
                    aggregate:
                    [
                        new SelectItemTotal(
                            "all",
                            new ConcatAllTotal(
                                NoteText,
                                new ParamString("separator"),
                                [new OrderItemRow(NoteId)]
                            )
                        ),
                    ]
                ),
                new Dictionary<string, IParameter>
                {
                    ["separator"] = new TextParameter(
                        new TextParameterType("string", true),
                        ""
                    ),
                }
            ).ToList()
        );
    }

    // Validation: "Column aliases are unique within a select (and within an
    // aggregate)".
    [Fact]
    public void RepeatedAggregateAliasesAreAnError()
    {
        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Total(
                    Orders,
                    [
                        new SelectItemTotal("v", new CountTotal()),
                        new SelectItemTotal("v", new SumIntegerTotal(Qty)),
                    ]
                )
                .ToList()
        );
        _ = Assert.ThrowsAny<ArgumentException>(() =>
            Grouped(
                    Orders,
                    [
                        new SelectItemGroup("v", new CountGroup()),
                        new SelectItemGroup("v", new MedianDecimalNullableGroup(Price)),
                    ]
                )
                .ToList()
        );
    }

    [Fact]
    public void GroupsAreSortedByAnAggregate()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                groupBy: [new GroupKey("g", G)],
                select: [new SelectItemGroup("g", new KeyString("g"))],
                orderBy: [new OrderItemGroupExpression(new CountGroup())]
            )
        );

        Assert.Equal(["b", "a"], ResultColumn.Texts(projection, "g"));
    }

    // A total query "returns its row even over no rows"; count, countDistinct
    // and sum are 0, concatAll the empty string, any false, all true and
    // every other aggregate null (Aggregate evaluation).
    [Fact]
    public void TotalQueryOverAnEmptyTableReturnsOneRowOfEmptyValues()
    {
        FieldDecimal x = new("t.empty", "x");
        FieldString s = new("t.empty", "s");
        FieldInteger id = new("t.empty", "id");
        PureQLProjection projection = Total(
            Empty,
            [
                new SelectItemTotal("count", new CountTotal()),
                new SelectItemTotal("ids", new SumIntegerTotal(id)),
                new SelectItemTotal("sum", new SumDecimalTotal(x)),
                new SelectItemTotal("distinct", new CountDistinctTotal(s)),
                new SelectItemTotal(
                    "joined",
                    new ConcatAllTotal(s, new LiteralString(","), [new OrderItemRow(id)])
                ),
                new SelectItemTotal("any", new AnyTotal(new LiteralBoolean(true))),
                new SelectItemTotal("all", new AllTotal(new LiteralBoolean(false))),
                new SelectItemTotal("mean", new AverageDecimalNullableTotal(x)),
                new SelectItemTotal("min", new MinStringNullableTotal(s)),
                new SelectItemTotal(
                    "first",
                    new FirstIntegerNullableTotal(id, [new OrderItemRow(id)])
                ),
                new SelectItemTotal("median", new MedianDecimalNullableTotal(x)),
                new SelectItemTotal("mode", new ModeStringNullableTotal(s)),
                new SelectItemTotal(
                    "percentile",
                    new PercentileDiscreteIntegerNullableTotal(
                        id,
                        new LiteralDecimal(1m)
                    )
                ),
                new SelectItemTotal("variance", new VariancePopulationTotal(x)),
                new SelectItemTotal("covariance", new CovariancePopulationTotal(x, x)),
                new SelectItemTotal("slope", new RegressionSlopeTotal(x, x)),
            ]
        );

        Assert.Equal(["0"], ResultColumn.Texts(projection, "count"));
        Assert.Equal(["0"], ResultColumn.Texts(projection, "ids"));
        Assert.Equal(["0"], ResultColumn.Texts(projection, "sum"));
        Assert.Equal(["0"], ResultColumn.Texts(projection, "distinct"));
        Assert.Equal([""], ResultColumn.Texts(projection, "joined"));
        Assert.Equal(["False"], ResultColumn.Texts(projection, "any"));
        Assert.Equal(["True"], ResultColumn.Texts(projection, "all"));
        Assert.Equal([""], ResultColumn.Texts(projection, "mean"));
        Assert.Equal([""], ResultColumn.Texts(projection, "min"));
        Assert.Equal([""], ResultColumn.Texts(projection, "first"));
        Assert.Equal([""], ResultColumn.Texts(projection, "median"));
        Assert.Equal([""], ResultColumn.Texts(projection, "mode"));
        Assert.Equal([""], ResultColumn.Texts(projection, "percentile"));
        Assert.Equal([""], ResultColumn.Texts(projection, "variance"));
        Assert.Equal([""], ResultColumn.Texts(projection, "covariance"));
        Assert.Equal([""], ResultColumn.Texts(projection, "slope"));
    }

    // "A group always has at least one row, so a grouped query over no rows
    // returns no rows"; a plain query yields one row per row.
    [Fact]
    public void GroupedAndPlainQueriesOverAnEmptyTableReturnNoRows()
    {
        PureQLProjection grouped = Grouped(
            Empty,
            [
                new SelectItemGroup("count", new CountGroup()),
                new SelectItemGroup(
                    "all",
                    new CountGroup(over: AggregateOver.All)
                ),
            ]
        );
        PureQLProjection plain = new(
            [Empty],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.empty"),
                select:
                [
                    new SelectItemProjection("count", new CountProjection()),
                    new SelectItemProjection(
                        "sum",
                        new SumDecimalProjection(new FieldDecimal("t.empty", "x"))
                    ),
                ]
            )
        );

        Assert.Empty(grouped.ToList());
        Assert.Empty(plain.ToList());
    }

    // A subquery's "orderBy only decides which rows its pagination and
    // distinctBy keep": the query that aggregates it reads those rows.
    [Theory]
    [InlineData(null, 2L, "2", "3.5", "1", "2")]
    [InlineData(1L, 2L, "2", "1.7", "1", "5")]
    [InlineData(3L, null, "2", "0.1", "3", "4")]
    public void AggregatesOverAPagedSubqueryReadTheRowsItsPaginationKeeps(
        long? skip,
        long? take,
        string count,
        string sum,
        string min,
        string max
    )
    {
        PureQLProjection projection = new(
            [Orders],
            new MainTotalQuery(
                subqueries:
                [
                    new SubqueryQuery(
                        "top",
                        new PlainQueryWithPagination(
                            from: new FromEntity("t.orders"),
                            select:
                            [
                                new SelectItemProjection("id", Id),
                                new SelectItemProjection("price", Price),
                            ],
                            orderBy:
                            [
                                new OrderItemProjectionColumn(
                                    "price",
                                    SortDirection.Desc
                                ),
                            ],
                            pagination: skip is long skipped
                                ? take is long taken
                                    ? new Pagination(skipped, taken)
                                    : new Pagination(skip: skipped)
                                : new Pagination(take: take.GetValueOrDefault())
                        )
                    ),
                ],
                from: new FromSubquery("top"),
                aggregate:
                [
                    new SelectItemTotal("count", new CountTotal()),
                    new SelectItemTotal(
                        "sum",
                        new SumDecimalTotal(new FieldDecimalNullable("top", "price"))
                    ),
                    new SelectItemTotal(
                        "min",
                        new MinIntegerNullableTotal(new FieldInteger("top", "id"))
                    ),
                    new SelectItemTotal(
                        "max",
                        new MaxIntegerNullableTotal(new FieldInteger("top", "id"))
                    ),
                ]
            )
        );

        Assert.Equal([count], ResultColumn.Texts(projection, "count"));
        Assert.Equal([sum], ResultColumn.Texts(projection, "sum"));
        Assert.Equal([min], ResultColumn.Texts(projection, "min"));
        Assert.Equal([max], ResultColumn.Texts(projection, "max"));
    }

    // "Subqueries behave as if each ran once, before the main query": the
    // where of the query that aggregates the two most expensive orders (2
    // and 1) filters those two rows, it does not choose them.
    [Fact]
    public void WhereOverAPagedSubqueryFiltersTheRowsItsPaginationKept()
    {
        FieldInteger id = new("top", "id");
        Subquery top = new SubqueryQuery(
            "top",
            new PlainQueryWithPagination(
                from: new FromEntity("t.orders"),
                select:
                [
                    new SelectItemProjection("id", Id),
                    new SelectItemProjection("g", G),
                    new SelectItemProjection("price", Price),
                ],
                orderBy: [new OrderItemProjectionColumn("price", SortDirection.Desc)],
                pagination: new Pagination(take: 2L)
            )
        );
        GreaterThanDecimalRow later = new(id, new LiteralInteger(1));
        PureQLProjection total = new(
            [Orders],
            new MainTotalQuery(
                subqueries: [top],
                from: new FromSubquery("top"),
                where: later,
                aggregate:
                [
                    new SelectItemTotal("count", new CountTotal()),
                    new SelectItemTotal(
                        "sum",
                        new SumDecimalTotal(new FieldDecimalNullable("top", "price"))
                    ),
                ]
            )
        );
        PureQLProjection grouped = new(
            [Orders],
            new MainGroupedQueryWithoutPagination(
                subqueries: [top],
                from: new FromSubquery("top"),
                where: later,
                groupBy: [new GroupKey("g", new FieldString("top", "g"))],
                select:
                [
                    new SelectItemGroup("g", new KeyString("g")),
                    new SelectItemGroup("count", new CountGroup()),
                ],
                orderBy: [new OrderItemGroupColumn("g")]
            )
        );

        Assert.Equal(["1"], ResultColumn.Texts(total, "count"));
        Assert.Equal(["2"], ResultColumn.Texts(total, "sum"));
        Assert.Equal(["a"], ResultColumn.Texts(grouped, "g"));
        Assert.Equal(["1"], ResultColumn.Texts(grouped, "count"));
    }

    // The subquery keeps (a, 3) and (b, 5); the where of the query that
    // reads it keeps (a, 3) of those, not the latest row below 5 of each g.
    [Fact]
    public void WhereOverADeduplicatedSubqueryFiltersTheRowsItsDistinctByKept()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainTotalQuery(
                subqueries:
                [
                    new SubqueryQuery(
                        "latest",
                        new PlainQueryWithoutPaginationWithOrderBy(
                            from: new FromEntity("t.orders"),
                            distinctBy: ["g"],
                            select:
                            [
                                new SelectItemProjection("g", G),
                                new SelectItemProjection("id", Id),
                            ],
                            orderBy:
                            [
                                new OrderItemProjectionColumn("id", SortDirection.Desc),
                            ]
                        )
                    ),
                ],
                from: new FromSubquery("latest"),
                where: new LessThanDecimalRow(
                    new FieldInteger("latest", "id"),
                    new LiteralInteger(5)
                ),
                aggregate:
                [
                    new SelectItemTotal("count", new CountTotal()),
                    new SelectItemTotal(
                        "ids",
                        new SumIntegerTotal(new FieldInteger("latest", "id"))
                    ),
                ]
            )
        );

        Assert.Equal(["1"], ResultColumn.Texts(projection, "count"));
        Assert.Equal(["3"], ResultColumn.Texts(projection, "ids"));
    }

    [Fact]
    public void AggregatesOverADeduplicatedSubqueryReadTheRowsItsOrderKeeps()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainTotalQuery(
                subqueries:
                [
                    new SubqueryQuery(
                        "latest",
                        new PlainQueryWithoutPaginationWithOrderBy(
                            from: new FromEntity("t.orders"),
                            distinctBy: ["g"],
                            select:
                            [
                                new SelectItemProjection("g", G),
                                new SelectItemProjection("id", Id),
                            ],
                            orderBy:
                            [
                                new OrderItemProjectionColumn("id", SortDirection.Desc),
                            ]
                        )
                    ),
                ],
                from: new FromSubquery("latest"),
                aggregate:
                [
                    new SelectItemTotal(
                        "ids",
                        new SumIntegerTotal(new FieldInteger("latest", "id"))
                    ),
                    new SelectItemTotal(
                        "groups",
                        new ConcatAllTotal(
                            new FieldString("latest", "g"),
                            new LiteralString(","),
                            [new OrderItemRow(new FieldInteger("latest", "id"))]
                        )
                    ),
                ]
            )
        );

        Assert.Equal(["8"], ResultColumn.Texts(projection, "ids"));
        Assert.Equal(["a,b"], ResultColumn.Texts(projection, "groups"));
    }

    // distinctBy "drops each row equal, on the listed columns ... to a row
    // before it": the subquery keeps the rows (a, 3) and (b, 5), so in over
    // its id column holds only for 3 and 5, as count over it says.
    [Fact(
        Skip = "Bug: in over a subquery with distinctBy and no pagination must see "
            + "only the rows distinctBy keeps (ids 3, 5); QueryTree drops distinctBy "
            + "for QueryUse.Membership, so every id 1-5 matches"
    )]
    public void InOverADeduplicatedSubqueryReadsOnlyTheRowsItKeeps()
    {
        Subquery latest = new SubqueryQuery(
            "latest",
            new PlainQueryWithoutPaginationWithOrderBy(
                from: new FromEntity("t.orders"),
                distinctBy: ["g"],
                select:
                [
                    new SelectItemProjection("g", G),
                    new SelectItemProjection("id", Id),
                ],
                orderBy: [new OrderItemProjectionColumn("id", SortDirection.Desc)]
            )
        );
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                subqueries: [latest],
                from: new FromEntity("t.orders"),
                where: new InDecimalRow(
                    Id,
                    new ListSubqueryColumnInteger("latest", "id")
                ),
                select: [new SelectItemProjection("id", Id)],
                orderBy: [new OrderItemProjectionColumn("id")]
            )
        );
        PureQLProjection counted = new(
            [Orders],
            new MainTotalQuery(
                subqueries: [latest],
                from: new FromEntity("t.orders"),
                where: new InDecimalRow(
                    Id,
                    new ListSubqueryColumnInteger("latest", "id")
                ),
                aggregate: [new SelectItemTotal("count", new CountTotal())]
            )
        );

        Assert.Equal(["3", "5"], ResultColumn.Texts(projection, "id"));
        Assert.Equal(["2"], ResultColumn.Texts(counted, "count"));
    }

    // Set operations "compare rows column by column with equal, so two nulls
    // are the same value, and 1 and 1.0 are"; the left rows are 1, 2, 2 and
    // the right ones 1.0, 3, null.
    [Theory]
    [InlineData("unionAll", "6", "3", "9", "1.8")]
    [InlineData("union", "4", "3", "6", "2")]
    [InlineData("intersect", "1", "1", "1", "1")]
    [InlineData("except", "1", "1", "2", "2")]
    public void AggregatesOverACombinedSubquery(
        string operation,
        string count,
        string distinct,
        string sum,
        string mean
    )
    {
        FieldDecimalNullable x = new("c", "x");
        PureQLProjection projection = new(
            [Left, Right],
            new MainTotalQuery(
                subqueries: [new SubqueryQuery("c", Combined(operation))],
                from: new FromSubquery("c"),
                aggregate:
                [
                    new SelectItemTotal("count", new CountTotal()),
                    new SelectItemTotal("distinct", new CountDistinctTotal(x)),
                    new SelectItemTotal("sum", new SumDecimalTotal(x)),
                    new SelectItemTotal("mean", new AverageDecimalNullableTotal(x)),
                ]
            )
        );

        Assert.Equal([count], ResultColumn.Texts(projection, "count"));
        Assert.Equal([distinct], ResultColumn.Texts(projection, "distinct"));
        Assert.Equal([sum], ResultColumn.Texts(projection, "sum"));
        Assert.Equal([mean], ResultColumn.Texts(projection, "mean"));
    }

    // groupBy: "1 and 1.0 are the same decimal key", and all null keys form
    // one group.
    [Fact]
    public void GroupsOfACombinedSubqueryPutOneAndOnePointZeroTogether()
    {
        PureQLProjection projection = new(
            [Left, Right],
            new MainGroupedQueryWithoutPagination(
                subqueries: [new SubqueryQuery("c", Combined("unionAll"))],
                from: new FromSubquery("c"),
                groupBy: [new GroupKey("x", new FieldDecimalNullable("c", "x"))],
                select:
                [
                    new SelectItemGroup("x", new KeyDecimalNullable("x")),
                    new SelectItemGroup("rows", new CountGroup()),
                ],
                orderBy: [new OrderItemGroupColumn("x")]
            )
        );

        Assert.Equal(["", "1", "2", "3"], ResultColumn.Texts(projection, "x"));
        Assert.Equal(["1", "2", "2", "1"], ResultColumn.Texts(projection, "rows"));
    }

    // Aggregates of a plain query run "over every row after where", before
    // distinctBy and pagination.
    [Fact]
    public void PlainQueryAggregatesIgnoreDistinctByAndPagination()
    {
        PureQLProjection distinct = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                select:
                [
                    new SelectItemProjection("g", G),
                    new SelectItemProjection("count", new CountProjection()),
                    new SelectItemProjection("total", new SumDecimalProjection(Price)),
                ],
                distinctBy: ["g"],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );
        PureQLProjection paged = new(
            [Orders],
            new MainPlainQueryWithPagination(
                from: new FromEntity("t.orders"),
                pagination: new Pagination(skip: 1L, take: 1L),
                select:
                [
                    new SelectItemProjection("id", Id),
                    new SelectItemProjection("count", new CountProjection()),
                ],
                orderBy: [new OrderItemProjectionExpression(Id)]
            )
        );

        Assert.Equal(["a", "b"], ResultColumn.Texts(distinct, "g"));
        Assert.Equal(["5", "5"], ResultColumn.Texts(distinct, "count"));
        Assert.Equal(["3.8", "3.8"], ResultColumn.Texts(distinct, "total"));
        Assert.Equal(["2"], ResultColumn.Texts(paged, "id"));
        Assert.Equal(["5"], ResultColumn.Texts(paged, "count"));
    }

    [Fact]
    public void TotalQueryOverALeftJoinAggregatesEveryJoinedRow()
    {
        FieldStringNullable tag = new("t.tags", "tag");
        PureQLProjection projection = new(
            [Orders, Tags],
            new MainTotalQuery(
                from: new FromEntity("t.orders"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Left,
                        "t.tags",
                        new EqualDecimalRow(
                            Id,
                            new FieldIntegerNullable("t.tags", "order_id")
                        )
                    ),
                ],
                aggregate:
                [
                    new SelectItemTotal("count", new CountTotal()),
                    new SelectItemTotal(
                        "tagged",
                        new CountTotal(
                            new NotEqualStringRow(tag, new LiteralStringNullable())
                        )
                    ),
                    new SelectItemTotal("tags", new CountDistinctTotal(tag)),
                    new SelectItemTotal("orders", new CountDistinctTotal(Id)),
                    new SelectItemTotal(
                        "joined",
                        new ConcatAllTotal(
                            tag,
                            new LiteralString(","),
                            [new OrderItemRow(tag), new OrderItemRow(Id)]
                        )
                    ),
                    new SelectItemTotal("revenue", new SumDecimalTotal(Price)),
                ]
            )
        );

        Assert.Equal(["6"], ResultColumn.Texts(projection, "count"));
        Assert.Equal(["3"], ResultColumn.Texts(projection, "tagged"));
        Assert.Equal(["2"], ResultColumn.Texts(projection, "tags"));
        Assert.Equal(["5"], ResultColumn.Texts(projection, "orders"));
        Assert.Equal(["p,p,q"], ResultColumn.Texts(projection, "joined"));
        Assert.Equal(["5.3"], ResultColumn.Texts(projection, "revenue"));
    }

    [Theory]
    [InlineData(SortDirection.Desc, "a", "3")]
    [InlineData(SortDirection.Asc, "b", "2")]
    public void AggregatesOverAPagedGroupedSubqueryReadTheGroupsItKeeps(
        SortDirection direction,
        string group,
        string rows
    )
    {
        PureQLProjection projection = new(
            [Orders],
            new MainTotalQuery(
                subqueries:
                [
                    new SubqueryQuery(
                        "big",
                        new GroupedQueryWithPagination(
                            from: new FromEntity("t.orders"),
                            pagination: new Pagination(take: 1L),
                            groupBy: [new GroupKey("g", G)],
                            select:
                            [
                                new SelectItemGroup("g", new KeyString("g")),
                                new SelectItemGroup("rows", new CountGroup()),
                            ],
                            orderBy: [new OrderItemGroupColumn("rows", direction)]
                        )
                    ),
                ],
                from: new FromSubquery("big"),
                aggregate:
                [
                    new SelectItemTotal(
                        "groups",
                        new ConcatAllTotal(
                            new FieldString("big", "g"),
                            new LiteralString(","),
                            [new OrderItemRow(new FieldString("big", "g"))]
                        )
                    ),
                    new SelectItemTotal(
                        "rows",
                        new SumIntegerTotal(new FieldInteger("big", "rows"))
                    ),
                ]
            )
        );

        Assert.Equal([group], ResultColumn.Texts(projection, "groups"));
        Assert.Equal([rows], ResultColumn.Texts(projection, "rows"));
    }

    // "The result is the rows of every level": 1, 2, 3, 4, 5.
    [Fact]
    public void AggregatesOverARecursiveSubqueryReadEveryLevel()
    {
        FieldInteger n = new("r", "n");
        PureQLProjection projection = new(
            [Left],
            new MainTotalQuery(
                subqueries:
                [
                    new SubqueryRecursive(
                        "r",
                        new Recursion(
                            seed: new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.left"),
                                where: new EqualDecimalRow(
                                    new FieldInteger("t.left", "v"),
                                    new LiteralInteger(1)
                                ),
                                select:
                                [
                                    new SelectItemProjection(
                                        "n",
                                        new FieldInteger("t.left", "v")
                                    ),
                                ]
                            ),
                            next: new PlainNextQuery(
                                from: new FromSubquery("r"),
                                where: new LessThanDecimalRow(n, new LiteralInteger(5)),
                                select:
                                [
                                    new SelectItemRow(
                                        "n",
                                        new AddIntegerRow([n, new LiteralInteger(1)])
                                    ),
                                ]
                            ),
                            maxDepth: 10L
                        )
                    ),
                ],
                from: new FromSubquery("r"),
                aggregate:
                [
                    new SelectItemTotal("count", new CountTotal()),
                    new SelectItemTotal("sum", new SumIntegerTotal(n)),
                    new SelectItemTotal("median", new MedianDecimalNullableTotal(n)),
                    new SelectItemTotal("variance", new VariancePopulationTotal(n)),
                    new SelectItemTotal(
                        "last",
                        new LastIntegerNullableTotal(n, [new OrderItemRow(n)])
                    ),
                ]
            )
        );

        Assert.Equal(["5"], ResultColumn.Texts(projection, "count"));
        Assert.Equal(["15"], ResultColumn.Texts(projection, "sum"));
        Assert.Equal(["3"], ResultColumn.Texts(projection, "median"));
        Assert.Equal(["2"], ResultColumn.Texts(projection, "variance"));
        Assert.Equal(["5"], ResultColumn.Texts(projection, "last"));
    }

    // The order of strings by code point holds over rows no table stores.
    [Fact]
    public void StringAggregatesOverInlineRowsOrderByCodePoint()
    {
        FieldString s = new("cells", "s");
        PureQLProjection projection = new(
            [],
            new MainTotalQuery(
                subqueries:
                [
                    new SubqueryRows(
                        "cells",
                        [
                            [new RowItem("s", new LiteralString("\U00010000"))],
                            [new RowItem("s", new LiteralString("￿"))],
                            [new RowItem("s", new LiteralString("a"))],
                        ]
                    ),
                ],
                from: new FromSubquery("cells"),
                aggregate:
                [
                    new SelectItemTotal("min", new MinStringNullableTotal(s)),
                    new SelectItemTotal("max", new MaxStringNullableTotal(s)),
                    new SelectItemTotal(
                        "first",
                        new FirstStringNullableTotal(
                            s,
                            [new OrderItemRow(s, SortDirection.Desc)]
                        )
                    ),
                    new SelectItemTotal(
                        "joined",
                        new ConcatAllTotal(
                            s,
                            new LiteralString(","),
                            [new OrderItemRow(s)]
                        )
                    ),
                    new SelectItemTotal(
                        "discrete",
                        new PercentileDiscreteStringNullableTotal(
                            s,
                            new LiteralDecimal(0.5m)
                        )
                    ),
                    new SelectItemTotal("mode", new ModeStringNullableTotal(s)),
                ]
            )
        );

        Assert.Equal(["a"], ResultColumn.Texts(projection, "min"));
        Assert.Equal(["\U00010000"], ResultColumn.Texts(projection, "max"));
        Assert.Equal(["\U00010000"], ResultColumn.Texts(projection, "first"));
        Assert.Equal(
            ["a,￿,\U00010000"],
            ResultColumn.Texts(projection, "joined")
        );
        Assert.Equal(["￿"], ResultColumn.Texts(projection, "discrete"));
        Assert.Equal(["a"], ResultColumn.Texts(projection, "mode"));
    }

    // A total subquery returns its row over no rows, with a null max; "A null
    // value is in a list exactly when the list contains null".
    [Theory]
    [InlineData(false, "3")]
    [InlineData(true, "2")]
    public void InReadsTheNullOfATotalSubqueryOverNoRows(bool rows, string expected)
    {
        PureQLProjection projection = new(
            [Orders],
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "top",
                        new TotalQuery(
                            from: new FromEntity("t.orders"),
                            where: new LiteralBoolean(rows),
                            aggregate:
                            [
                                new SelectItemTotal(
                                    "m",
                                    new MaxDecimalNullableTotal(Price)
                                ),
                            ]
                        )
                    ),
                ],
                from: new FromEntity("t.orders"),
                where: new InDecimalRow(
                    Price,
                    new ListSubqueryColumnDecimalNullable("top", "m")
                ),
                select: [new SelectItemProjection("id", Id)]
            )
        );

        Assert.Equal([expected], ResultColumn.Texts(projection, "id"));
    }

    // over: "all" reads "every row left by where", after the joins.
    [Fact]
    public void AggregatesOverAllReadTheJoinedRowsLeftByWhere()
    {
        FieldString tag = new("t.tags", "tag");
        PureQLProjection projection = new(
            [Orders, Tags],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("t.orders"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Inner,
                        "t.tags",
                        new EqualDecimalRow(Id, new FieldInteger("t.tags", "order_id"))
                    ),
                ],
                where: new EqualStringRow(tag, new LiteralString("p")),
                groupBy: [new GroupKey("g", G)],
                select:
                [
                    new SelectItemGroup("rows", new CountGroup()),
                    new SelectItemGroup("all", new CountGroup(over: AggregateOver.All)),
                    new SelectItemGroup(
                        "revenue",
                        new SumDecimalGroup(Price, over: AggregateOver.All)
                    ),
                ],
                orderBy: [new OrderItemGroupExpression(new KeyString("g"))]
            )
        );

        Assert.Equal(["1", "1"], ResultColumn.Texts(projection, "rows"));
        Assert.Equal(["2", "2"], ResultColumn.Texts(projection, "all"));
        Assert.Equal(["1.6", "1.6"], ResultColumn.Texts(projection, "revenue"));
    }

    // "aggregate yields exactly one row", so a total subquery over no rows is
    // one row for the query that aggregates it.
    [Fact]
    public void ATotalSubqueryOverNoRowsIsOneRowToAggregate()
    {
        PureQLProjection projection = new(
            [Orders],
            new MainTotalQuery(
                subqueries:
                [
                    new SubqueryQuery(
                        "none",
                        new TotalQuery(
                            from: new FromEntity("t.orders"),
                            where: new LiteralBoolean(false),
                            aggregate:
                            [
                                new SelectItemTotal("rows", new CountTotal()),
                                new SelectItemTotal(
                                    "mean",
                                    new AverageDecimalNullableTotal(Price)
                                ),
                            ]
                        )
                    ),
                ],
                from: new FromSubquery("none"),
                aggregate:
                [
                    new SelectItemTotal("count", new CountTotal()),
                    new SelectItemTotal(
                        "rows",
                        new SumIntegerTotal(new FieldInteger("none", "rows"))
                    ),
                    new SelectItemTotal(
                        "means",
                        new CountDistinctTotal(new FieldDecimalNullable("none", "mean"))
                    ),
                ]
            )
        );

        Assert.Equal(["1"], ResultColumn.Texts(projection, "count"));
        Assert.Equal(["0"], ResultColumn.Texts(projection, "rows"));
        Assert.Equal(["0"], ResultColumn.Texts(projection, "means"));
    }

    // first / last sort by "the order of its type": false before true, a
    // uuid by its hexadecimal digits, a datetime by its instant.
    [Fact]
    public void FirstAndLastSortKeysOfEveryTypeByTheOrderOfTheType()
    {
        const string entity = "t.kinds";
        FieldInteger id = new(entity, "id");
        PureQLProjection projection = Total(
            Kinds,
            [
                new SelectItemTotal(
                    "by_flag",
                    new FirstIntegerNullableTotal(
                        id,
                        [new OrderItemRow(new FieldBooleanNullable(entity, "b"))]
                    )
                ),
                new SelectItemTotal(
                    "by_flag_desc",
                    new FirstIntegerNullableTotal(
                        id,
                        [
                            new OrderItemRow(
                                new FieldBooleanNullable(entity, "b"),
                                SortDirection.Desc
                            ),
                        ]
                    )
                ),
                new SelectItemTotal(
                    "by_uuid",
                    new FirstIntegerNullableTotal(
                        id,
                        [new OrderItemRow(new FieldUuid(entity, "u"))]
                    )
                ),
                new SelectItemTotal(
                    "by_uuid_last",
                    new LastIntegerNullableTotal(
                        id,
                        [new OrderItemRow(new FieldUuid(entity, "u"))]
                    )
                ),
                new SelectItemTotal(
                    "by_instant",
                    new FirstIntegerNullableTotal(
                        id,
                        [new OrderItemRow(new FieldDatetime(entity, "at"))]
                    )
                ),
                new SelectItemTotal(
                    "by_instant_last",
                    new LastIntegerNullableTotal(
                        id,
                        [new OrderItemRow(new FieldDatetime(entity, "at"))]
                    )
                ),
                new SelectItemTotal(
                    "by_number_last",
                    new LastIntegerNullableTotal(
                        id,
                        [new OrderItemRow(new FieldDecimal(entity, "d"))]
                    )
                ),
            ]
        );

        // b: true, false, null; u: 7fff..., 8000..., 0000...-8000-...; at:
        // 00:00, 23:59:59.999999 the day before, 00:00:00.000001.
        Assert.Equal(["3"], ResultColumn.Texts(projection, "by_flag"));
        Assert.Equal(["1"], ResultColumn.Texts(projection, "by_flag_desc"));
        Assert.Equal(["3"], ResultColumn.Texts(projection, "by_uuid"));
        Assert.Equal(["2"], ResultColumn.Texts(projection, "by_uuid_last"));
        Assert.Equal(["2"], ResultColumn.Texts(projection, "by_instant"));
        Assert.Equal(["3"], ResultColumn.Texts(projection, "by_instant_last"));
        Assert.Equal(["3"], ResultColumn.Texts(projection, "by_number_last"));
    }

    [Fact]
    public void ConcatAllTakesAnEmptySeparatorParameter()
    {
        PureQLProjection projection = new(
            [Notes],
            new MainTotalQuery(
                from: new FromEntity("t.notes"),
                aggregate:
                [
                    new SelectItemTotal(
                        "all",
                        new ConcatAllTotal(
                            NoteText,
                            new ParamString("separator"),
                            [new OrderItemRow(NoteId)]
                        )
                    ),
                ]
            ),
            new Dictionary<string, IParameter>
            {
                ["separator"] = new TextParameter("string", ""),
            }
        );

        Assert.Equal(["xyx"], ResultColumn.Texts(projection, "all"));
    }

    private static Query Combined(string operation)
    {
        Query left = new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.left"),
            select: [new SelectItemProjection("x", new FieldInteger("t.left", "v"))]
        );
        Query right = new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.right"),
            select:
            [
                new SelectItemProjection("x", new FieldDecimalNullable("t.right", "w")),
            ]
        );

        return operation switch
        {
            "unionAll" => new CombinedQueryUnionAll([left, right]),
            "union" => new CombinedQueryUnion([left, right]),
            "intersect" => new CombinedQueryIntersect([left, right]),
            _ => new CombinedQueryExcept([left, right]),
        };
    }

    private static string Entity(IStoredSchemaDataSet table)
    {
        return "t." + table.Schema.Tables.Single().Name.TextValue;
    }

    private static PureQLProjection Grouped(
        IStoredSchemaDataSet table,
        IEnumerable<SelectItemGroup> select,
        BooleanRow? where = null,
        BooleanGroup? having = null
    )
    {
        string entity = Entity(table);

        return new PureQLProjection(
            [table],
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity(entity),
                groupBy: [new GroupKey("g", new FieldString(entity, "g"))],
                select: select,
                where: where,
                having: having,
                orderBy: [new OrderItemGroupExpression(new KeyString("g"))]
            )
        );
    }

    private static PureQLProjection Total(
        IStoredSchemaDataSet table,
        IEnumerable<SelectItemTotal> aggregate,
        BooleanRow? where = null
    )
    {
        return new PureQLProjection(
            [table],
            new MainTotalQuery(
                from: new FromEntity(Entity(table)),
                aggregate: aggregate,
                where: where
            )
        );
    }

    private static PureQLProjection Plain(
        IStoredSchemaDataSet table,
        IEnumerable<SelectItemProjection> select,
        BooleanRow? where = null
    )
    {
        string entity = Entity(table);

        return new PureQLProjection(
            [table],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity(entity),
                select: select,
                where: where,
                orderBy:
                [
                    new OrderItemProjectionExpression(new FieldInteger(entity, "id")),
                ]
            )
        );
    }
}
