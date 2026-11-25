using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.GroupExpressions;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.TotalExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// Set operations: unionAll returns every row of every input, union each
// distinct row once, intersect the distinct rows every input returns and
// except the distinct rows of the first input that no other input returns.
// Rows are compared column by column with equal (two nulls are one value,
// 1 and 1.0 are). Columns are matched by alias, come in the order of the
// first input and take the least type that accepts every input. A combined
// query stands wherever a query does and is filtered, sorted and paged by a
// query that reads it as a subquery.
public sealed class SetOperationTests
{
    // The distinct pairs (n, s): (1, x), (2, y), (null, null).
    private static readonly IStoredSchemaDataSet First = new InlineTable(
        "t",
        "first",
        ("id", new LongColumnType()),
        ("n", new LongColumnType()),
        ("s", new StringColumnType())
    ).With(
        ["1", "1", "x"],
        ["2", "1", "x"],
        ["3", "2", "y"],
        ["4", "", ""],
        ["5", "", ""]
    );

    // The distinct pairs (n, s): (1, x), (3, z), (null, null).
    private static readonly IStoredSchemaDataSet Second = new InlineTable(
        "t",
        "second",
        ("id", new LongColumnType()),
        ("n", new LongColumnType()),
        ("d", new DoubleColumnType()),
        ("s", new StringColumnType())
    ).With(
        ["1", "1", "1.0", "x"],
        ["2", "1", "1.0", "x"],
        ["3", "1", "1.00", "x"],
        ["4", "3", "3.5", "z"],
        ["5", "", "", ""]
    );

    // The distinct pairs (n, s): (1, x), (2, y), (9, w).
    private static readonly IStoredSchemaDataSet Third = new InlineTable(
        "t",
        "third",
        ("id", new LongColumnType()),
        ("n", new LongColumnType()),
        ("s", new StringColumnType())
    ).With(["1", "1", "x"], ["2", "2", "y"], ["3", "9", "w"]);

    private static readonly IStoredSchemaDataSet Dated = new InlineTable(
        "t",
        "dated",
        ("id", new LongColumnType()),
        ("day", new DateColumnType()),
        ("at", new DateTimeColumnType()),
        ("flag", new BoolColumnType())
    ).With(
        ["1", "2024-01-01", "2024-01-01T00:00:00Z", "True"],
        ["2", "2024-01-02", "2024-01-02T00:00:00Z", "False"]
    );

    private static readonly IStoredSchemaDataSet[] Tables =
    [
        First,
        Second,
        Third,
        Dated,
    ];

    [Fact]
    public void UnionAllReturnsEveryRowOfEveryInputDuplicatesKept()
    {
        // (1, x) comes twice from the first input and three times from the
        // second: it is returned five times.
        AssertUnordered(
            MainQuery("unionAll", Pairs("t.first"), Pairs("t.second")),
            "1|x",
            "1|x",
            "1|x",
            "1|x",
            "1|x",
            "2|y",
            "3|z",
            "|",
            "|",
            "|"
        );
    }

    [Fact]
    public void UnionReturnsEachDistinctRowOnceAndTwoNullsAreTheSameValue()
    {
        AssertUnordered(
            MainQuery("union", Pairs("t.first"), Pairs("t.second")),
            "1|x",
            "2|y",
            "3|z",
            "|"
        );
    }

    [Fact]
    public void IntersectReturnsEachDistinctRowThatOccursInEveryInput()
    {
        AssertUnordered(
            MainQuery("intersect", Pairs("t.first"), Pairs("t.second")),
            "1|x",
            "|"
        );
    }

    [Theory]
    [InlineData("t.first", "t.second", "2|y")]
    [InlineData("t.second", "t.first", "3|z")]
    [InlineData("t.third", "t.second", "2|y", "9|w")]
    public void ExceptReturnsEachDistinctRowOfTheFirstInputThatOccursInNoOther(
        string first,
        string other,
        params string[] expected
    )
    {
        AssertUnordered(MainQuery("except", Pairs(first), Pairs(other)), expected);
    }

    [Fact]
    public void ExceptReturnsARepeatedRowOfTheFirstInputOnce()
    {
        // The first input returns (1, x) twice and (null, null) twice.
        AssertUnordered(
            MainQuery("except", Pairs("t.first"), Pairs("t.third")),
            "|"
        );
    }

    [Fact]
    public void RowsThatDifferInOneColumnAreDifferentRows()
    {
        // (1, 1) and (1, 2) share n; (2, 1) is not (1, 2).
        AssertUnordered(
            MainQuery(
                "union",
                Literals(new LiteralInteger(1), new LiteralInteger(1)),
                Literals(new LiteralInteger(1), new LiteralInteger(2)),
                Literals(new LiteralInteger(2), new LiteralInteger(1)),
                Literals(new LiteralInteger(1), new LiteralInteger(2))
            ),
            "1|1",
            "1|2",
            "2|1"
        );
    }

    [Fact]
    public void StringsAreComparedCodePointByCodePoint()
    {
        AssertUnordered(
            MainQuery(
                "union",
                Literals(new LiteralString("a"), new LiteralString("b")),
                Literals(new LiteralString("A"), new LiteralString("b")),
                Literals(new LiteralString("a"), new LiteralString("b"))
            ),
            "A|b",
            "a|b"
        );
    }

    [Fact]
    public void UnionAllOfThreeInputsReturnsTheRowsOfAll()
    {
        AssertUnordered(
            MainQuery("unionAll", Pairs("t.first"), Pairs("t.third"), Pairs("t.first")),
            "1|x",
            "1|x",
            "1|x",
            "1|x",
            "1|x",
            "2|y",
            "2|y",
            "2|y",
            "9|w",
            "|",
            "|",
            "|",
            "|"
        );
    }

    [Fact]
    public void UnionOfThreeInputsReturnsEachDistinctRowOnce()
    {
        AssertUnordered(
            MainQuery("union", Pairs("t.first"), Pairs("t.second"), Pairs("t.third")),
            "1|x",
            "2|y",
            "3|z",
            "9|w",
            "|"
        );
    }

    [Fact]
    public void IntersectOfThreeInputsReturnsTheRowsOfEveryInput()
    {
        // (null, null) is in the first two inputs only, (2, y) in the first
        // and the third only.
        AssertUnordered(
            MainQuery("intersect", Pairs("t.first"), Pairs("t.second"), Pairs("t.third")),
            "1|x"
        );
    }

    [Theory]
    [InlineData("t.second", "t.first", "t.third", "3|z")]
    [InlineData("t.second", "t.third", "t.first", "3|z")]
    [InlineData("t.third", "t.first", "t.second", "9|w")]
    [InlineData("t.first", "t.second", "t.third")]
    public void ExceptOfThreeInputsDropsTheRowsOfEveryOtherInput(
        string first,
        string second,
        string third,
        params string[] expected
    )
    {
        AssertUnordered(
            MainQuery("except", Pairs(first), Pairs(second), Pairs(third)),
            expected
        );
    }

    [Theory]
    [InlineData("union", "", "1", "2", "3.5")]
    [InlineData("intersect", "", "1")]
    [InlineData("except", "2")]
    public void AnIntegerAndADecimalOfOneValueAreTheSameRow(
        string operation,
        params string[] expected
    )
    {
        // first.n holds 1, 1, 2, null, null and second.d 1.0, 1.0, 1.00,
        // 3.5, null.
        AssertUnordered(
            MainQuery(
                operation,
                Column("t.first", new FieldIntegerNullable("t.first", "n")),
                Column("t.second", new FieldDecimalNullable("t.second", "d"))
            ),
            expected
        );
    }

    [Fact]
    public void ExceptOfDecimalsDropsTheIntegersOfTheSameValue()
    {
        AssertUnordered(
            MainQuery(
                "except",
                Column("t.second", new FieldDecimalNullable("t.second", "d")),
                Column("t.first", new FieldIntegerNullable("t.first", "n"))
            ),
            "3.5"
        );
    }

    [Fact]
    public void UnionAllOfAnIntegerAndADecimalInputKeepsEveryValue()
    {
        AssertUnordered(
            MainQuery(
                "unionAll",
                Column("t.third", new FieldIntegerNullable("t.third", "n")),
                Column("t.second", new FieldDecimalNullable("t.second", "d"))
            ),
            "",
            "1",
            "1",
            "1",
            "1",
            "2",
            "3.5",
            "9"
        );
    }

    [Fact]
    public void UnionExceptIsAnExceptWhoseFirstInputIsAUnion()
    {
        // (first union second) except third.
        AssertUnordered(
            MainQuery(
                "except",
                Combined("union", Pairs("t.first"), Pairs("t.second")),
                Pairs("t.third")
            ),
            "3|z",
            "|"
        );
    }

    [Fact]
    public void ExceptOfAnIntersectDropsOnlyTheRowsOfTheIntersection()
    {
        // first except (second intersect third): the intersection is (1, x).
        AssertUnordered(
            MainQuery(
                "except",
                Pairs("t.first"),
                Combined("intersect", Pairs("t.second"), Pairs("t.third"))
            ),
            "2|y",
            "|"
        );
    }

    [Fact]
    public void UnionAllOfAUnionKeepsTheDistinctRowsOfTheUnionAndEveryOtherRow()
    {
        // (first union second) has four rows, third three.
        AssertUnordered(
            MainQuery(
                "unionAll",
                Combined("union", Pairs("t.first"), Pairs("t.second")),
                Pairs("t.third")
            ),
            "1|x",
            "1|x",
            "2|y",
            "2|y",
            "3|z",
            "9|w",
            "|"
        );
    }

    [Fact]
    public void UnionOfAUnionAllReturnsEachDistinctRowOnce()
    {
        AssertUnordered(
            MainQuery(
                "union",
                Pairs("t.third"),
                Combined("unionAll", Pairs("t.first"), Pairs("t.first"))
            ),
            "1|x",
            "2|y",
            "9|w",
            "|"
        );
    }

    [Fact]
    public void CombinedQueriesNestThreeLevelsDeep()
    {
        // ((first intersect second) union third) except second, the shape of
        // sample 109: {(1, x), (null, null)} union third is {(1, x),
        // (null, null), (2, y), (9, w)}.
        AssertUnordered(
            MainQuery(
                "except",
                Combined(
                    "union",
                    Combined("intersect", Pairs("t.first"), Pairs("t.second")),
                    Pairs("t.third")
                ),
                Pairs("t.second")
            ),
            "2|y",
            "9|w"
        );
    }

    [Theory]
    [InlineData(
        "unionAll",
        "grouped|1",
        "grouped|2",
        "grouped|2",
        "plain|1",
        "plain|2",
        "total|5"
    )]
    [InlineData("union", "grouped|1", "grouped|2", "plain|1", "plain|2", "total|5")]
    public void InputsMayBePlainGroupedAndTotalQueries(
        string operation,
        params string[] expected
    )
    {
        AssertUnordered(
            MainQuery(operation, PlainInput(), GroupedInput(), TotalInput()),
            expected
        );
    }

    [Theory]
    [InlineData("intersect", "grouped|2")]
    [InlineData("except", "grouped|1")]
    public void AGroupedInputIsComparedWithAPlainOne(
        string operation,
        params string[] expected
    )
    {
        // The groups of first by s have 2, 1 and 2 rows.
        AssertUnordered(
            MainQuery(
                operation,
                GroupedInput(),
                new PlainQueryWithoutPaginationWithoutOrderBy(
                    from: new FromEntity("t.third"),
                    select:
                    [
                        new SelectItemProjection("kind", new LiteralString("grouped")),
                        new SelectItemProjection("value", new LiteralInteger(2)),
                    ]
                )
            ),
            expected
        );
    }

    [Theory]
    [InlineData("unionAll", "0", "0")]
    [InlineData("union", "0")]
    [InlineData("intersect", "0")]
    [InlineData("except")]
    public void ATotalInputReturnsItsOneRowEvenOverNoRows(
        string operation,
        params string[] expected
    )
    {
        AssertUnordered(
            MainQuery(operation, CountOfNoRows("t.first"), CountOfNoRows("t.second")),
            expected
        );
    }

    [Theory]
    [InlineData("unionAll", "t.first", "1|x", "1|x", "2|y", "|", "|")]
    [InlineData("union", "t.first", "1|x", "2|y", "|")]
    [InlineData("intersect", "t.first")]
    [InlineData("except", "t.first")]
    public void AnInputWithoutRowsComesFirst(
        string operation,
        string other,
        params string[] expected
    )
    {
        AssertUnordered(MainQuery(operation, NoPairs(), Pairs(other)), expected);
    }

    [Theory]
    [InlineData("unionAll", "1|x", "1|x", "2|y", "|", "|")]
    [InlineData("union", "1|x", "2|y", "|")]
    [InlineData("intersect")]
    [InlineData("except", "1|x", "2|y", "|")]
    public void AnInputWithoutRowsComesLast(string operation, params string[] expected)
    {
        AssertUnordered(MainQuery(operation, Pairs("t.first"), NoPairs()), expected);
    }

    [Theory]
    [InlineData("unionAll")]
    [InlineData("union")]
    [InlineData("intersect")]
    [InlineData("except")]
    public void InputsWithoutRowsGiveNoRows(string operation)
    {
        AssertUnordered(MainQuery(operation, NoPairs(), NoPairs()));
    }

    [Theory]
    [InlineData("unionAll", "1|x", "1|x", "1|x", "1|x", "1|x", "2|y", "3|z", "|", "|", "|")]
    [InlineData("union", "1|x", "2|y", "3|z", "|")]
    [InlineData("intersect", "1|x", "|")]
    [InlineData("except", "2|y")]
    public void ColumnsAreMatchedByAliasWhateverTheirOrderInAnInput(
        string operation,
        params string[] expected
    )
    {
        PureQLProjection projection = new(
            Tables,
            MainQuery(operation, Pairs("t.first"), PairsInReverse("t.second"))
        );

        Assert.Equal(["n", "s"], ColumnNames(projection));
        AssertUnordered(projection, expected);
    }

    [Fact]
    public void ColumnsComeInTheOrderOfTheFirstInput()
    {
        PureQLProjection projection = new(
            Tables,
            MainQuery("union", PairsInReverse("t.first"), Pairs("t.second"))
        );

        Assert.Equal(["s", "n"], ColumnNames(projection));
        AssertUnordered(projection, "x|1", "y|2", "z|3", "|");
    }

    [Fact]
    public void ColumnsOfOneTypeAreMatchedByAliasNotByPosition()
    {
        // By position the second input would be the row (q, p).
        PureQLProjection projection = new(
            Tables,
            new MainCombinedQueryUnion(
                union:
                [
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromEntity("t.third"),
                        select:
                        [
                            new SelectItemProjection("a", new LiteralString("p")),
                            new SelectItemProjection("b", new LiteralString("q")),
                        ]
                    ),
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromEntity("t.third"),
                        select:
                        [
                            new SelectItemProjection("b", new LiteralString("q")),
                            new SelectItemProjection("a", new LiteralString("p")),
                        ]
                    ),
                ]
            )
        );

        Assert.Equal(["a", "b"], ColumnNames(projection));
        AssertUnordered(projection, "p|q");
    }

    [Theory]
    [InlineData("unionAll")]
    [InlineData("union")]
    [InlineData("intersect")]
    [InlineData("except")]
    public void AnIntegerAndADecimalColumnGiveADecimalColumn(string operation)
    {
        PureQLProjection projection = new(
            Tables,
            MainQuery(
                operation,
                Column("t.first", new FieldIntegerNullable("t.first", "n")),
                Column("t.second", new FieldDecimalNullable("t.second", "d"))
            )
        );

        Assert.Equal(["x:decimal"], ColumnTypes(projection));
    }

    [Fact]
    public void ADecimalAndAnIntegerColumnGiveADecimalColumn()
    {
        PureQLProjection projection = new(
            Tables,
            MainQuery(
                "unionAll",
                Column("t.second", new FieldDecimalNullable("t.second", "d")),
                Column("t.first", new FieldIntegerNullable("t.first", "n"))
            )
        );

        Assert.Equal(["x:decimal"], ColumnTypes(projection));
    }

    [Fact]
    public void IntegerColumnsGiveAnIntegerColumn()
    {
        PureQLProjection projection = new(
            Tables,
            MainQuery(
                "unionAll",
                Column("t.first", new FieldIntegerNullable("t.first", "n")),
                Column("t.second", new FieldInteger("t.second", "id"))
            )
        );

        Assert.Equal(["x:long"], ColumnTypes(projection));
    }

    [Fact]
    public void ADecimalColumnInTheLastOfThreeInputsGivesADecimalColumn()
    {
        PureQLProjection projection = new(
            Tables,
            MainQuery(
                "unionAll",
                Column("t.first", new FieldInteger("t.first", "id")),
                Column("t.third", new FieldInteger("t.third", "id")),
                Column("t.second", new FieldDecimalNullable("t.second", "d"))
            )
        );

        Assert.Equal(["x:decimal"], ColumnTypes(projection));
    }

    [Fact]
    public void ADecimalColumnInANestedInputGivesADecimalColumn()
    {
        PureQLProjection projection = new(
            Tables,
            MainQuery(
                "unionAll",
                Column("t.first", new FieldInteger("t.first", "id")),
                Combined(
                    "union",
                    Column("t.third", new FieldInteger("t.third", "id")),
                    Column("t.second", new FieldDecimalNullable("t.second", "d"))
                )
            )
        );

        Assert.Equal(["x:decimal"], ColumnTypes(projection));
        AssertUnordered(projection, "", "1", "1", "2", "2", "3", "3", "3.5", "4", "5");
    }

    [Fact]
    public void EveryColumnTakesItsOwnLeastType()
    {
        PureQLProjection projection = new(
            Tables,
            new MainCombinedQueryUnionAll(
                unionAll:
                [
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromEntity("t.dated"),
                        select:
                        [
                            new SelectItemProjection(
                                "day",
                                new FieldDate("t.dated", "day")
                            ),
                            new SelectItemProjection(
                                "at",
                                new FieldDatetime("t.dated", "at")
                            ),
                            new SelectItemProjection(
                                "flag",
                                new FieldBoolean("t.dated", "flag")
                            ),
                            new SelectItemProjection(
                                "number",
                                new FieldInteger("t.dated", "id")
                            ),
                            new SelectItemProjection("text", new LiteralString("a")),
                        ]
                    ),
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromEntity("t.dated"),
                        where: new EqualDecimalRow(
                            new FieldInteger("t.dated", "id"),
                            new LiteralInteger(1)
                        ),
                        select:
                        [
                            new SelectItemProjection("text", new LiteralStringNullable()),
                            new SelectItemProjection(
                                "number",
                                new LiteralDecimal(0.5m)
                            ),
                            new SelectItemProjection("flag", new LiteralBoolean(false)),
                            new SelectItemProjection(
                                "at",
                                new FieldDatetime("t.dated", "at")
                            ),
                            new SelectItemProjection(
                                "day",
                                new FieldDate("t.dated", "day")
                            ),
                        ]
                    ),
                ]
            )
        );

        Assert.Equal(
            ["day:date", "at:datetime", "flag:bool", "number:decimal", "text:string"],
            ColumnTypes(projection)
        );
        AssertUnordered(
            projection,
            "2024-01-01|2024-01-01T00:00:00Z|True|1|a",
            "2024-01-02|2024-01-02T00:00:00Z|False|2|a",
            "2024-01-01|2024-01-01T00:00:00Z|False|0.5|"
        );
    }

    [Fact]
    public void AColumnOfAnIntegerAndADecimalInputIsReadAsADecimal()
    {
        AssertRows(
            ReadingColumn(
                Combined(
                    "union",
                    Column("t.third", new FieldInteger("t.third", "id")),
                    Column("t.third", new LiteralDecimal(2.5m))
                ),
                new FieldDecimal("u", "x")
            ),
            "1",
            "2",
            "2.5",
            "3"
        );
    }

    [Fact]
    public void AColumnOfAnIntegerAndADecimalInputIsNotAnInteger()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            ReadingColumn(
                Combined(
                    "union",
                    Column("t.third", new FieldInteger("t.third", "id")),
                    Column("t.third", new LiteralDecimal(2.5m))
                ),
                new FieldInteger("u", "x")
            )
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AColumnIsNullableIfItIsNullableInAnyInput(bool nullableFirst)
    {
        Query nullable = Column("t.first", new FieldIntegerNullable("t.first", "n"));
        Query nonNull = Column("t.third", new FieldInteger("t.third", "id"));

        AssertRows(
            ReadingColumn(
                nullableFirst
                    ? Combined("union", nullable, nonNull)
                    : Combined("union", nonNull, nullable),
                new FieldIntegerNullable("u", "x")
            ),
            "",
            "1",
            "2",
            "3"
        );
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void AColumnNullableInAnInputIsNotReadAsNonNull(bool nullableFirst)
    {
        Query nullable = Column("t.first", new FieldIntegerNullable("t.first", "n"));
        Query nonNull = Column("t.third", new FieldInteger("t.third", "id"));

        _ = Assert.Throws<ArgumentException>(() =>
            ReadingColumn(
                nullableFirst
                    ? Combined("union", nullable, nonNull)
                    : Combined("union", nonNull, nullable),
                new FieldInteger("u", "x")
            )
        );
    }

    [Fact]
    public void AColumnNonNullInEveryInputIsNonNull()
    {
        Query combined = Combined(
            "unionAll",
            Column("t.first", new FieldInteger("t.first", "id")),
            Column("t.third", new FieldInteger("t.third", "id"))
        );

        AssertRows(
            ReadingColumn(combined, new FieldInteger("u", "x")),
            "1",
            "1",
            "2",
            "2",
            "3",
            "3",
            "4",
            "5"
        );
        _ = Assert.Throws<ArgumentException>(() =>
            ReadingColumn(combined, new FieldIntegerNullable("u", "x"))
        );
    }

    [Fact]
    public void ANullableIntegerAndADecimalColumnGiveANullableDecimal()
    {
        Query combined = Combined(
            "union",
            Column("t.first", new FieldIntegerNullable("t.first", "n")),
            Column("t.third", new LiteralDecimal(0.5m))
        );

        AssertRows(
            ReadingColumn(combined, new FieldDecimalNullable("u", "x")),
            "",
            "0.5",
            "1",
            "2"
        );
        _ = Assert.Throws<ArgumentException>(() =>
            ReadingColumn(combined, new FieldDecimal("u", "x"))
        );
        _ = Assert.Throws<ArgumentException>(() =>
            ReadingColumn(combined, new FieldIntegerNullable("u", "x"))
        );
    }

    [Theory]
    [InlineData("unionAll", "1|x", "1|x", "1|x", "1|x", "1|x", "2|y", "3|z", "|", "|", "|")]
    [InlineData("union", "1|x", "2|y", "3|z", "|")]
    [InlineData("intersect", "1|x", "|")]
    [InlineData("except", "2|y")]
    public void ACombinedQueryIsTheQueryOfASubquery(
        string operation,
        params string[] expected
    )
    {
        AssertUnordered(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "pairs",
                        Combined(operation, Pairs("t.first"), Pairs("t.second"))
                    ),
                ],
                from: new FromSubquery("pairs"),
                select:
                [
                    new SelectItemProjection("n", new FieldIntegerNullable("pairs", "n")),
                    new SelectItemProjection("s", new FieldStringNullable("pairs", "s")),
                ]
            ),
            expected
        );
    }

    [Theory]
    [InlineData("unionAll", "1|x", "1|x", "1|x", "1|x", "1|x", "2|y", "3|z", "|", "|", "|")]
    [InlineData("union", "1|x", "2|y", "3|z", "|")]
    [InlineData("intersect", "1|x", "|")]
    [InlineData("except", "2|y")]
    public void ACombinedQueryWithoutSubqueriesRunsOnItsOwn(
        string operation,
        params string[] expected
    )
    {
        AssertUnordered(
            new PureQLProjection(
                Tables,
                Combined(operation, Pairs("t.first"), Pairs("t.second"))
            ),
            expected
        );
    }

    [Fact]
    public void ACombinedSubqueryIsFilteredSortedAndPagedByTheQueryThatReadsIt()
    {
        // The ids 1 to 5 of first and of second, each with its kind.
        AssertRows(
            new MainPlainQueryWithPagination(
                subqueries: [new SubqueryQuery("events", Events())],
                from: new FromSubquery("events"),
                where: new GreaterThanDecimalRow(
                    new FieldInteger("events", "id"),
                    new LiteralInteger(1)
                ),
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("events", "id")),
                    new SelectItemProjection("kind", new FieldString("events", "kind")),
                ],
                orderBy:
                [
                    new OrderItemProjectionColumn("id", SortDirection.Desc),
                    new OrderItemProjectionColumn("kind"),
                ],
                pagination: new Pagination(skip: 1L, take: 4L)
            ),
            "5|second",
            "4|first",
            "4|second",
            "3|first"
        );
    }

    [Fact]
    public void ACombinedSubqueryIsDeduplicatedByColumns()
    {
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries: [new SubqueryQuery("events", Events())],
                from: new FromSubquery("events"),
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("events", "id")),
                    new SelectItemProjection("kind", new FieldString("events", "kind")),
                ],
                orderBy:
                [
                    new OrderItemProjectionColumn("id"),
                    new OrderItemProjectionColumn("kind", SortDirection.Desc),
                ],
                distinctBy: ["id"]
            ),
            "1|second",
            "2|second",
            "3|second",
            "4|second",
            "5|second"
        );
    }

    [Fact]
    public void ACombinedSubqueryIsGrouped()
    {
        AssertRows(
            new MainGroupedQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "pairs",
                        Combined(
                            "unionAll",
                            Pairs("t.first"),
                            Pairs("t.second"),
                            Pairs("t.third")
                        )
                    ),
                ],
                from: new FromSubquery("pairs"),
                groupBy: [new GroupKey("s", new FieldStringNullable("pairs", "s"))],
                select:
                [
                    new SelectItemGroup("s", new KeyStringNullable("s")),
                    new SelectItemGroup("rows", new CountGroup()),
                ],
                orderBy: [new OrderItemGroupColumn("s")]
            ),
            "|3",
            "w|1",
            "x|6",
            "y|2",
            "z|1"
        );
    }

    [Fact]
    public void ACombinedSubqueryIsTheListOfIn()
    {
        // The ids of third whose n is in first except second: 2.
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "only",
                        Combined(
                            "except",
                            Column("t.first", new FieldIntegerNullable("t.first", "n")),
                            Column("t.second", new FieldIntegerNullable("t.second", "n"))
                        )
                    ),
                ],
                from: new FromEntity("t.third"),
                where: new InDecimalRow(
                    new FieldIntegerNullable("t.third", "n"),
                    new ListSubqueryColumnIntegerNullable("only", "x")
                ),
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("t.third", "id")),
                ],
                orderBy: [new OrderItemProjectionColumn("id")]
            ),
            "2"
        );
    }

    [Fact]
    public void ACombinedSubqueryIsJoined()
    {
        // The distinct n of first and second are 1, 2, 3 and null; a null
        // key matches nothing in third.
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "all",
                        Combined(
                            "union",
                            Column("t.first", new FieldIntegerNullable("t.first", "n")),
                            Column("t.second", new FieldIntegerNullable("t.second", "n"))
                        )
                    ),
                ],
                from: new FromSubquery("all"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Left,
                        "t.third",
                        new EqualDecimalRow(
                            new FieldIntegerNullable("t.third", "n"),
                            new FieldIntegerNullable("all", "x")
                        )
                    ),
                ],
                select:
                [
                    new SelectItemProjection("x", new FieldIntegerNullable("all", "x")),
                    new SelectItemProjection(
                        "s",
                        new FieldStringNullable("t.third", "s")
                    ),
                ],
                orderBy: [new OrderItemProjectionColumn("x")]
            ),
            "|",
            "1|x",
            "2|y",
            "3|"
        );
    }

    [Fact]
    public void ACombinedSubqueryIsAnInputOfALaterCombinedSubquery()
    {
        AssertUnordered(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "common",
                        Combined("intersect", Pairs("t.first"), Pairs("t.second"))
                    ),
                    new SubqueryQuery(
                        "rest",
                        Combined("except", Pairs("t.third"), SubqueryPairs("common"))
                    ),
                ],
                from: new FromSubquery("rest"),
                select:
                [
                    new SelectItemProjection("n", new FieldIntegerNullable("rest", "n")),
                    new SelectItemProjection("s", new FieldStringNullable("rest", "s")),
                ]
            ),
            "2|y",
            "9|w"
        );
    }

    [Theory]
    [InlineData("unionAll", "1|x", "1|x", "2|y", "9|w", "|")]
    [InlineData("union", "1|x", "2|y", "9|w", "|")]
    [InlineData("intersect", "1|x")]
    [InlineData("except", "|")]
    public void AMainCombinedQueryDeclaresSubqueriesItsInputsRead(
        string operation,
        params string[] expected
    )
    {
        Subquery[] subqueries =
        [
            new SubqueryQuery(
                "common",
                Combined("intersect", Pairs("t.first"), Pairs("t.second"))
            ),
        ];
        Query[] inputs = [SubqueryPairs("common"), Pairs("t.third")];

        AssertUnordered(
            operation switch
            {
                "unionAll" => new MainCombinedQueryUnionAll(inputs, subqueries),
                "union" => new MainCombinedQueryUnion(inputs, subqueries),
                "intersect" => new MainCombinedQueryIntersect(inputs, subqueries),
                _ => new MainCombinedQueryExcept(inputs, subqueries),
            },
            expected
        );
    }

    [Fact]
    public void AnInputReadsInlineRows()
    {
        AssertUnordered(
            new MainCombinedQueryExcept(
                except: [Pairs("t.first"), SubqueryPairs("written")],
                subqueries:
                [
                    new SubqueryRows(
                        "written",
                        [
                            [
                                new RowItem("n", new LiteralIntegerNullable()),
                                new RowItem("s", new LiteralStringNullable()),
                            ],
                            [
                                new RowItem("n", new LiteralInteger(2)),
                                new RowItem("s", new LiteralString("y")),
                            ],
                        ]
                    ),
                ]
            ),
            "1|x"
        );
    }

    [Fact]
    public void AnInputWithOrderByAndPaginationGivesItsPage()
    {
        // The two highest ids of each input.
        AssertUnordered(
            MainQuery("unionAll", Latest("t.first", "first"), Latest("t.second", "second")),
            "4|first",
            "4|second",
            "5|first",
            "5|second"
        );
    }

    [Fact]
    public void AnInputWithOrderByAndDistinctByGivesItsFirstRows()
    {
        // The row of the highest id for each s of first, then every pair of
        // third.
        AssertUnordered(
            MainQuery(
                "unionAll",
                new PlainQueryWithoutPaginationWithOrderBy(
                    from: new FromEntity("t.first"),
                    select:
                    [
                        new SelectItemProjection(
                            "n",
                            new AsNullableIntegerNullableProjection(
                                new FieldInteger("t.first", "id")
                            )
                        ),
                        new SelectItemProjection(
                            "s",
                            new FieldStringNullable("t.first", "s")
                        ),
                    ],
                    orderBy: [new OrderItemProjectionColumn("n", SortDirection.Desc)],
                    distinctBy: ["s"]
                ),
                Pairs("t.third")
            ),
            "1|x",
            "2|x",
            "2|y",
            "3|y",
            "5|",
            "9|w"
        );
    }

    [Theory]
    [InlineData("unionAll")]
    [InlineData("union")]
    [InlineData("intersect")]
    [InlineData("except")]
    public void InputsWithDifferentAliasesAreRejected(string operation)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                MainQuery(
                    operation,
                    Column("t.first", new FieldInteger("t.first", "id")),
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromEntity("t.third"),
                        select:
                        [
                            new SelectItemProjection(
                                "y",
                                new FieldInteger("t.third", "id")
                            ),
                        ]
                    )
                )
            )
        );
    }

    [Fact]
    public void AnInputWithAColumnMoreIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                MainQuery(
                    "unionAll",
                    Column("t.first", new FieldIntegerNullable("t.first", "n")),
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromEntity("t.third"),
                        select:
                        [
                            new SelectItemProjection(
                                "x",
                                new FieldIntegerNullable("t.third", "n")
                            ),
                            new SelectItemProjection(
                                "s",
                                new FieldStringNullable("t.third", "s")
                            ),
                        ]
                    )
                )
            )
        );
    }

    [Fact]
    public void AnInputWithAColumnLessIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                MainQuery(
                    "union",
                    Pairs("t.first"),
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromEntity("t.third"),
                        select:
                        [
                            new SelectItemProjection(
                                "n",
                                new FieldIntegerNullable("t.third", "n")
                            ),
                        ]
                    )
                )
            )
        );
    }

    [Fact]
    public void AThirdInputWithAnotherAliasIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                MainQuery(
                    "intersect",
                    Pairs("t.first"),
                    Pairs("t.second"),
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromEntity("t.third"),
                        select:
                        [
                            new SelectItemProjection(
                                "n",
                                new FieldIntegerNullable("t.third", "n")
                            ),
                            new SelectItemProjection(
                                "S",
                                new FieldStringNullable("t.third", "s")
                            ),
                        ]
                    )
                )
            )
        );
    }

    [Fact]
    public void AnAliasTwiceInEveryInputIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                MainQuery("unionAll", Twice("t.first"), Twice("t.third"))
            )
        );
    }

    [Fact]
    public void AnAliasTwiceInOneInputIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                MainQuery(
                    "unionAll",
                    Column("t.first", new FieldInteger("t.first", "id")),
                    Twice("t.third")
                )
            )
        );
    }

    [Theory]
    [InlineData("unionAll")]
    [InlineData("union")]
    [InlineData("intersect")]
    [InlineData("except")]
    public void ADateAndADatetimeColumnDoNotMeet(string operation)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                MainQuery(
                    operation,
                    Column("t.dated", new FieldDate("t.dated", "day")),
                    Column("t.dated", new FieldDatetime("t.dated", "at"))
                )
            )
        );
    }

    [Fact]
    public void AnIntegerAndAStringColumnDoNotMeet()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                MainQuery(
                    "unionAll",
                    Column("t.third", new FieldInteger("t.third", "id")),
                    Column("t.third", new LiteralString("1"))
                )
            )
        );
    }

    [Fact]
    public void ABooleanAndAnIntegerColumnDoNotMeet()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                MainQuery(
                    "union",
                    Column("t.dated", new FieldBoolean("t.dated", "flag")),
                    Column("t.dated", new FieldInteger("t.dated", "id"))
                )
            )
        );
    }

    [Fact]
    public void ADecimalAndAStringColumnInTheThirdInputDoNotMeet()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                MainQuery(
                    "unionAll",
                    Column("t.third", new FieldInteger("t.third", "id")),
                    Column("t.third", new LiteralDecimal(1.5m)),
                    Column("t.third", new LiteralStringNullable())
                )
            )
        );
    }

    [Fact]
    public void ColumnsOfANestedCombinedQueryThatDoNotMeetAreRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                MainQuery(
                    "except",
                    Column("t.dated", new FieldDate("t.dated", "day")),
                    Combined(
                        "union",
                        Column("t.dated", new FieldDate("t.dated", "day")),
                        Column("t.dated", new FieldDatetime("t.dated", "at"))
                    )
                )
            )
        );
    }

    [Fact]
    public void ANestedCombinedQueryWhoseTypeDoesNotMeetTheOtherInputIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                MainQuery(
                    "except",
                    Column("t.dated", new FieldDatetime("t.dated", "at")),
                    Combined(
                        "union",
                        Column("t.dated", new FieldDate("t.dated", "day")),
                        Column("t.dated", new FieldDate("t.dated", "day"))
                    )
                )
            )
        );
    }

    [Fact]
    public void InputsOfACombinedSubqueryWithDifferentAliasesAreRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            ReadingColumn(
                Combined(
                    "unionAll",
                    Column("t.first", new FieldInteger("t.first", "id")),
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromEntity("t.third"),
                        select:
                        [
                            new SelectItemProjection(
                                "x",
                                new FieldInteger("t.third", "id")
                            ),
                            new SelectItemProjection(
                                "y",
                                new FieldInteger("t.third", "id")
                            ),
                        ]
                    )
                ),
                new FieldInteger("u", "x")
            )
        );
    }

    [Fact]
    public void ColumnsOfACombinedSubqueryThatDoNotMeetAreRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            ReadingColumn(
                Combined(
                    "unionAll",
                    Column("t.dated", new FieldDatetime("t.dated", "at")),
                    Column("t.dated", new FieldDate("t.dated", "day"))
                ),
                new FieldDatetime("u", "x")
            )
        );
    }

    [Fact]
    public void AColumnACombinedSubqueryDoesNotHaveIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            ReadingColumn(
                Combined(
                    "unionAll",
                    Column("t.first", new FieldInteger("t.first", "id")),
                    Column("t.third", new FieldInteger("t.third", "id"))
                ),
                new FieldInteger("u", "id")
            )
        );
    }

    // select n, s from the entity.
    private static Query Pairs(string entity)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity(entity),
            select:
            [
                new SelectItemProjection("n", new FieldIntegerNullable(entity, "n")),
                new SelectItemProjection("s", new FieldStringNullable(entity, "s")),
            ]
        );
    }

    // select s, n from the entity.
    private static Query PairsInReverse(string entity)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity(entity),
            select:
            [
                new SelectItemProjection("s", new FieldStringNullable(entity, "s")),
                new SelectItemProjection("n", new FieldIntegerNullable(entity, "n")),
            ]
        );
    }

    // select n, s from a subquery that has them.
    private static Query SubqueryPairs(string subquery)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromSubquery(subquery),
            select:
            [
                new SelectItemProjection("n", new FieldIntegerNullable(subquery, "n")),
                new SelectItemProjection("s", new FieldStringNullable(subquery, "s")),
            ]
        );
    }

    // The pairs of first, of which where keeps none.
    private static Query NoPairs()
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.first"),
            where: new LessThanDecimalRow(
                new FieldInteger("t.first", "id"),
                new LiteralInteger(0)
            ),
            select:
            [
                new SelectItemProjection("n", new FieldIntegerNullable("t.first", "n")),
                new SelectItemProjection("s", new FieldStringNullable("t.first", "s")),
            ]
        );
    }

    // One column x for each row of the entity.
    private static Query Column(string entity, ExprProjection expression)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity(entity),
            select: [new SelectItemProjection("x", expression)]
        );
    }

    // The column x twice for each row of the entity.
    private static Query Twice(string entity)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity(entity),
            select:
            [
                new SelectItemProjection("x", new FieldInteger(entity, "id")),
                new SelectItemProjection("x", new FieldInteger(entity, "id")),
            ]
        );
    }

    // One row (a, b) of two values.
    private static Query Literals(ExprProjection a, ExprProjection b)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.third"),
            where: new EqualDecimalRow(
                new FieldInteger("t.third", "id"),
                new LiteralInteger(1)
            ),
            select: [new SelectItemProjection("a", a), new SelectItemProjection("b", b)]
        );
    }

    // (plain, id) for the ids 1 and 2 of first.
    private static Query PlainInput()
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.first"),
            where: new LessThanDecimalRow(
                new FieldInteger("t.first", "id"),
                new LiteralInteger(3)
            ),
            select:
            [
                new SelectItemProjection("kind", new LiteralString("plain")),
                new SelectItemProjection("value", new FieldInteger("t.first", "id")),
            ]
        );
    }

    // (grouped, rows) for each s of first: 2 rows of x, 1 of y, 2 of null.
    private static Query GroupedInput()
    {
        return new GroupedQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.first"),
            groupBy: [new GroupKey("s", new FieldStringNullable("t.first", "s"))],
            select:
            [
                new SelectItemGroup("kind", new LiteralString("grouped")),
                new SelectItemGroup("value", new CountGroup()),
            ]
        );
    }

    // (total, rows of first).
    private static Query TotalInput()
    {
        return new TotalQuery(
            from: new FromEntity("t.first"),
            aggregate:
            [
                new SelectItemTotal("kind", new LiteralString("total")),
                new SelectItemTotal("value", new CountTotal()),
            ]
        );
    }

    // The number of rows of the entity that where keeps: none.
    private static Query CountOfNoRows(string entity)
    {
        return new TotalQuery(
            from: new FromEntity(entity),
            where: new LessThanDecimalRow(
                new FieldInteger(entity, "id"),
                new LiteralInteger(0)
            ),
            aggregate: [new SelectItemTotal("rows", new CountTotal())]
        );
    }

    // (id, first) for every row of first and (id, second) for every row of
    // second.
    private static Query Events()
    {
        return Combined("unionAll", Kind("t.first", "first"), Kind("t.second", "second"));
    }

    private static Query Kind(string entity, string kind)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity(entity),
            select:
            [
                new SelectItemProjection("id", new FieldInteger(entity, "id")),
                new SelectItemProjection("kind", new LiteralString(kind)),
            ]
        );
    }

    // The two rows of the highest id, each with its kind.
    private static Query Latest(string entity, string kind)
    {
        return new PlainQueryWithPagination(
            from: new FromEntity(entity),
            select:
            [
                new SelectItemProjection("id", new FieldInteger(entity, "id")),
                new SelectItemProjection("kind", new LiteralString(kind)),
            ],
            orderBy: [new OrderItemProjectionColumn("id", SortDirection.Desc)],
            pagination: new Pagination(take: 2L)
        );
    }

    private static Query Combined(string operation, params Query[] inputs)
    {
        return operation switch
        {
            "unionAll" => new CombinedQueryUnionAll(inputs),
            "union" => new CombinedQueryUnion(inputs),
            "intersect" => new CombinedQueryIntersect(inputs),
            "except" => new CombinedQueryExcept(inputs),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
    }

    private static PureQLQuery MainQuery(string operation, params Query[] inputs)
    {
        return operation switch
        {
            "unionAll" => new MainCombinedQueryUnionAll(inputs),
            "union" => new MainCombinedQueryUnion(inputs),
            "intersect" => new MainCombinedQueryIntersect(inputs),
            "except" => new MainCombinedQueryExcept(inputs),
            _ => throw new ArgumentOutOfRangeException(nameof(operation)),
        };
    }

    // The column x of a combined query declared as the subquery u, read
    // through the given field and sorted.
    private static PureQLProjection ReadingColumn(Query combined, ExprProjection field)
    {
        return new PureQLProjection(
            Tables,
            new MainPlainQueryWithoutPagination(
                subqueries: [new SubqueryQuery("u", combined)],
                from: new FromSubquery("u"),
                select: [new SelectItemProjection("x", field)],
                orderBy: [new OrderItemProjectionColumn("x")]
            )
        );
    }

    private static void AssertRows(PureQLQuery query, params string[] expected)
    {
        AssertRows(new PureQLProjection(Tables, query), expected);
    }

    private static void AssertRows(PureQLProjection projection, params string[] expected)
    {
        Assert.Equal(expected.AsEnumerable(), Rows(projection));
    }

    private static void AssertUnordered(PureQLQuery query, params string[] expected)
    {
        AssertUnordered(new PureQLProjection(Tables, query), expected);
    }

    // The result of a combined query is unordered: the rows are compared as
    // a multiset.
    private static void AssertUnordered(
        PureQLProjection projection,
        params string[] expected
    )
    {
        Assert.Equal(
            expected.Order(StringComparer.Ordinal),
            Rows(projection).Order(StringComparer.Ordinal)
        );
    }

    // The cell texts of every row, in the order of the result's columns.
    private static IEnumerable<string> Rows(PureQLProjection projection)
    {
        IReadOnlyList<string> columns = ColumnNames(projection);

        return
        [
            .. projection
                .AsEnumerable()
                .Select(row =>
                    string.Join(
                        "|",
                        columns.Select(column =>
                            row.Cells.Single(cell =>
                                cell.Key.Name.TextValue == column
                            ).Value.Value.TextValue
                        )
                    )
                ),
        ];
    }

    private static IReadOnlyList<string> ColumnNames(PureQLProjection projection)
    {
        return [.. projection.TableSchema.Columns.Select(column => column.Name.TextValue)];
    }

    private static IReadOnlyList<string> ColumnTypes(PureQLProjection projection)
    {
        return
        [
            .. projection.TableSchema.Columns.Select(column =>
                $"{column.Name.TextValue}:{column.Type.Name.TextValue}"
            ),
        ];
    }
}
