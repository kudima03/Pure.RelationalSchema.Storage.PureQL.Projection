using System.Globalization;
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

// Edge cases of set operations, by the specification's Semantics (Set
// operations: rows compared column by column with equal, two nulls are one
// value, 1 and 1.0 are; each input behaves as if it ran once, on its own)
// and Query Clauses (Set operations: columns matched by alias, the least
// type that accepts every input).
public sealed class SetOperationEdgeTests
{
    private static readonly IStoredSchemaDataSet One = new InlineTable(
        "t",
        "one",
        ("id", new LongColumnType())
    ).With(["1"]);

    // n: 1, 1, null, 2, null; d: 1.5, 1.50, null, 2, null.
    private static readonly IStoredSchemaDataSet Nums = new InlineTable(
        "t",
        "nums",
        ("id", new LongColumnType()),
        ("n", new LongColumnType()),
        ("d", new DoubleColumnType()),
        ("s", new StringColumnType()),
        ("b", new BoolColumnType())
    ).With(
        ["1", "1", "1.5", "x", "True"],
        ["2", "1", "1.50", "x", "True"],
        ["3", "", "", "", ""],
        ["4", "2", "2", "y", "False"],
        ["5", "", "", "", ""]
    );

    // One instant written with two offsets, one uuid in two cases, and the
    // number 10.
    private static readonly IStoredSchemaDataSet Spellings = new InlineTable(
        "t",
        "spelled",
        ("id", new LongColumnType()),
        ("at", new DateTimeColumnType()),
        ("uuid", new UuidColumnType()),
        ("ten", new DoubleColumnType())
    ).With(
        ["1", "2024-01-01T00:00:00Z", "0000000a-0000-0000-0000-00000000000b", "10"],
        ["2", "2024-01-01T01:00:00+01:00", "0000000A-0000-0000-0000-00000000000B", "10"]
    );

    private static readonly IStoredSchemaDataSet[] Tables = [One, Nums, Spellings];

    [Fact]
    public void AnEmptyStringAndANullStringAreDifferentRows()
    {
        // Equality is by code points (Ordering and equality): "" is a
        // value, null is not, so union keeps both.
        AssertRows(
            Counting(
                Combined(
                    "union",
                    Literal(new LiteralString("")),
                    Literal(new LiteralStringNullable()),
                    Literal(new LiteralString(""))
                ),
                new FieldStringNullable("u", "x"),
                new LiteralString("")
            ),
            "2|1|1"
        );
    }

    [Fact]
    public void AnEmptyStringAndANullStringHaveNoRowInCommon()
    {
        AssertRows(
            Counting(
                Combined(
                    "intersect",
                    Literal(new LiteralString("")),
                    Literal(new LiteralStringNullable())
                ),
                new FieldStringNullable("u", "x"),
                new LiteralString("")
            ),
            "0|0|0"
        );
    }

    [Fact]
    public void ExceptOfANullStringKeepsTheEmptyString()
    {
        AssertRows(
            Counting(
                Combined(
                    "except",
                    Literal(new LiteralString("")),
                    Literal(new LiteralStringNullable())
                ),
                new FieldStringNullable("u", "x"),
                new LiteralString("")
            ),
            "1|0|1"
        );
    }

    [Fact]
    public void ExceptOfTheEmptyStringKeepsTheNullString()
    {
        AssertRows(
            Counting(
                Combined(
                    "except",
                    Literal(new LiteralStringNullable()),
                    Literal(new LiteralString(""))
                ),
                new FieldStringNullable("u", "x"),
                new LiteralString("")
            ),
            "1|1|0"
        );
    }

    [Fact]
    public void StoredDecimalsOfDifferentScalesAreOneRow()
    {
        // d holds 1.5, 1.50, null, 2 and null.
        AssertUnordered(
            MainQuery(
                "union",
                Column(new FieldDecimalNullable("t.nums", "d")),
                Literal(new LiteralDecimal(1.5m))
            ),
            "",
            "1.5",
            "2"
        );
    }

    [Fact]
    public void AnExactSumIsTheSameRowAsItsLiteral()
    {
        // Integer and decimal arithmetic: 0.1 + 0.2 equals 0.3.
        AssertUnordered(
            MainQuery(
                "union",
                Literal(
                    new AddDecimalProjection([
                        new LiteralDecimal(0.1m),
                        new LiteralDecimal(0.2m),
                    ])
                ),
                Literal(new LiteralDecimal(0.3m))
            ),
            "0.3"
        );
    }

    [Fact]
    public void TheLargestIntegerAndTheSameDecimalAreOneRow()
    {
        AssertUnordered(
            MainQuery(
                "union",
                Literal(new LiteralInteger(long.MaxValue)),
                Literal(new LiteralDecimal(9223372036854775807m))
            ),
            "9223372036854775807"
        );
    }

    [Fact]
    public void TheLargestIntegerAndTheDecimalBelowItAreTwoRows()
    {
        AssertUnordered(
            MainQuery(
                "union",
                Literal(new LiteralInteger(long.MaxValue)),
                Literal(new LiteralDecimal(9223372036854775806m))
            ),
            "9223372036854775806",
            "9223372036854775807"
        );
    }

    [Fact]
    public void TheLargestIntegerIsInTheIntersectionWithTheSameDecimal()
    {
        AssertUnordered(
            MainQuery(
                "intersect",
                Literal(new LiteralDecimal(9223372036854775807m)),
                Literal(new LiteralInteger(long.MaxValue))
            ),
            "9223372036854775807"
        );
    }

    [Fact]
    public void AComposedAndAPrecomposedAccentAreDifferentRows()
    {
        // Strings are equal by code points, not by canonical equivalence.
        AssertUnordered(
            MainQuery(
                "union",
                Literal(new LiteralString("é")),
                Literal(new LiteralString("é")),
                Literal(new LiteralString("é"))
            ),
            "é",
            "é"
        );
    }

    [Fact]
    public void ValuesOfEveryTypeAreComparedByValue()
    {
        // The second row differs from the first in the time by one
        // microsecond only.
        AssertUnordered(
            MainQuery(
                "union",
                Typed(new TimeOnly(13, 5, 9, 0, 1)),
                Typed(new TimeOnly(13, 5, 9, 0, 2)),
                Typed(new TimeOnly(13, 5, 9, 0, 1))
            ),
            "2024-02-29|13:05:09.000001|2024-02-29T23:59:58Z"
                + "|0000000a-0000-0000-0000-00000000000b|True",
            "2024-02-29|13:05:09.000002|2024-02-29T23:59:58Z"
                + "|0000000a-0000-0000-0000-00000000000b|True"
        );
    }

    [Fact]
    public void ValuesOfEveryTypeIntersectByValue()
    {
        AssertUnordered(
            MainQuery(
                "intersect",
                Typed(new TimeOnly(13, 5, 9, 0, 1)),
                Typed(new TimeOnly(13, 5, 9, 0, 1))
            ),
            "2024-02-29|13:05:09.000001|2024-02-29T23:59:58Z"
                + "|0000000a-0000-0000-0000-00000000000b|True"
        );
    }

    [Fact]
    public void NullableBooleansAreThreeDistinctValues()
    {
        AssertUnordered(
            MainQuery(
                "union",
                Column(new FieldBooleanNullable("t.nums", "b")),
                Column(new FieldBooleanNullable("t.nums", "b"))
            ),
            "",
            "False",
            "True"
        );
    }

    [Fact]
    public void RowsWithANullInDifferentColumnsAreDifferentRows()
    {
        AssertUnordered(
            MainQuery(
                "union",
                Pair(new LiteralInteger(1), new LiteralIntegerNullable()),
                Pair(new LiteralIntegerNullable(), new LiteralInteger(1)),
                Pair(new LiteralInteger(1), new LiteralIntegerNullable())
            ),
            "1|",
            "|1"
        );
    }

    [Fact]
    public void RowsWithANullInTheSameColumnIntersect()
    {
        AssertUnordered(
            MainQuery(
                "intersect",
                Pair(new LiteralInteger(1), new LiteralIntegerNullable()),
                Pair(new LiteralInteger(1), new LiteralIntegerNullable())
            ),
            "1|"
        );
    }

    [Fact]
    public void RowsWithANullInDifferentColumnsDoNotIntersect()
    {
        AssertUnordered(
            MainQuery(
                "intersect",
                Pair(new LiteralInteger(1), new LiteralIntegerNullable()),
                Pair(new LiteralIntegerNullable(), new LiteralInteger(1))
            )
        );
    }

    [Fact]
    public void ExceptDropsTheNullsOfTheFirstInputThatTheOtherReturns()
    {
        AssertUnordered(
            MainQuery(
                "except",
                Column(new FieldIntegerNullable("t.nums", "n")),
                Literal(new LiteralIntegerNullable())
            ),
            "1",
            "2"
        );
    }

    [Fact]
    public void IntersectKeepsANullThatEveryInputReturns()
    {
        AssertUnordered(
            MainQuery(
                "intersect",
                Column(new FieldIntegerNullable("t.nums", "n")),
                Literal(new LiteralIntegerNullable()),
                Column(new FieldIntegerNullable("t.nums", "n"))
            ),
            ""
        );
    }

    [Theory]
    [InlineData("union", "")]
    [InlineData("intersect", "")]
    [InlineData("unionAll", "", "")]
    [InlineData("except")]
    public void ANullAggregateOfATotalInputIsTheSameRowAsANullLiteral(
        string operation,
        params string[] expected
    )
    {
        // A total query over no rows returns its row, with min null.
        AssertUnordered(
            MainQuery(
                operation,
                new TotalQuery(
                    from: new FromEntity("t.nums"),
                    where: new LessThanDecimalRow(
                        new FieldInteger("t.nums", "id"),
                        new LiteralInteger(0)
                    ),
                    aggregate:
                    [
                        new SelectItemTotal(
                            "x",
                            new MinIntegerNullableTotal(new FieldInteger("t.nums", "id"))
                        ),
                    ]
                ),
                Literal(new LiteralIntegerNullable())
            ),
            expected
        );
    }

    [Fact]
    public void ANullLiteralOfIntegerAndADecimalGiveANullableDecimalColumn()
    {
        PureQLProjection projection = new(
            Tables,
            MainQuery(
                "unionAll",
                Literal(new LiteralIntegerNullable()),
                Literal(new LiteralDecimal(0.5m))
            )
        );

        Assert.Equal(["x:decimal"], ColumnTypes(projection));
        AssertUnordered(projection, "", "0.5");
    }

    [Fact]
    public void ANullLiteralOfIntegerAndADecimalAreReadAsANullableDecimal()
    {
        Query combined = Combined(
            "unionAll",
            Literal(new LiteralIntegerNullable()),
            Literal(new LiteralDecimal(0.5m))
        );

        AssertRows(Reading(combined, new FieldDecimalNullable("u", "x")), "", "0.5");
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(combined, new FieldDecimal("u", "x"))
        );
        _ = Assert.Throws<ArgumentException>(() =>
            Reading(combined, new FieldIntegerNullable("u", "x"))
        );
    }

    [Fact]
    public void ACombinedSubqueryIsSortedWithNullsFirstAndPaged()
    {
        // x: 1, 1, null, 2, null and 3; null sorts first ascending.
        AssertRows(
            Sorted(SortDirection.Asc, new Pagination(take: 3L)),
            "",
            "",
            "1"
        );
    }

    [Fact]
    public void ACombinedSubqueryIsSortedWithNullsLastDescendingAndPaged()
    {
        AssertRows(
            Sorted(SortDirection.Desc, new Pagination(skip: 1L, take: 4L)),
            "2",
            "1",
            "1",
            ""
        );
    }

    [Fact]
    public void ACombinedSubqueryIsPagedPastItsNulls()
    {
        AssertRows(
            Sorted(SortDirection.Asc, new Pagination(skip: 4L)),
            "2",
            "3"
        );
    }

    [Fact]
    public void DistinctByEveryColumnOfACombinedSubqueryKeepsOneNullRow()
    {
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "u",
                        Combined("unionAll", PairsOfNums(), PairsOfNums())
                    ),
                ],
                from: new FromSubquery("u"),
                select:
                [
                    new SelectItemProjection("n", new FieldIntegerNullable("u", "n")),
                    new SelectItemProjection("s", new FieldStringNullable("u", "s")),
                ],
                orderBy:
                [
                    new OrderItemProjectionColumn("n"),
                    new OrderItemProjectionColumn("s"),
                ],
                distinctBy: ["n", "s"]
            ),
            "|",
            "1|x",
            "2|y"
        );
    }

    [Fact]
    public void ANullOperandIsInACombinedColumnThatHoldsNull()
    {
        // Null semantics: a null value is in a list exactly when the list
        // contains null.
        AssertRows(
            IdsWhere(
                NullOrOne(),
                new InDecimalRow(
                    new FieldIntegerNullable("t.nums", "n"),
                    new ListSubqueryColumnIntegerNullable("u", "x")
                )
            ),
            "1",
            "2",
            "3",
            "5"
        );
    }

    [Fact]
    public void NotInACombinedColumnThatHoldsNullKeepsOnlyTheOtherValues()
    {
        AssertRows(
            IdsWhere(
                NullOrOne(),
                new NotRow(
                    new InDecimalRow(
                        new FieldIntegerNullable("t.nums", "n"),
                        new ListSubqueryColumnIntegerNullable("u", "x")
                    )
                )
            ),
            "4"
        );
    }

    [Fact]
    public void ANullOperandIsNotInAnIntersectionWithoutNull()
    {
        AssertRows(
            IdsWhere(
                Combined(
                    "intersect",
                    Column(new FieldIntegerNullable("t.nums", "n")),
                    Literal(new LiteralInteger(2))
                ),
                new InDecimalRow(
                    new FieldIntegerNullable("t.nums", "n"),
                    new ListSubqueryColumnIntegerNullable("u", "x")
                )
            ),
            "4"
        );
    }

    [Fact]
    public void ACombinedSubqueryJoinedOnNullableKeysPairsTheNulls()
    {
        // equal pairs two nulls in on as in every clause.
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries: [new SubqueryQuery("u", NumsOrNine())],
                from: new FromEntity("t.nums"),
                joins:
                [
                    new JoinSubquery(
                        JoinKind.Inner,
                        "u",
                        new EqualDecimalRow(
                            new FieldIntegerNullable("t.nums", "n"),
                            new FieldIntegerNullable("u", "x")
                        )
                    ),
                ],
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("t.nums", "id")),
                    new SelectItemProjection("x", new FieldIntegerNullable("u", "x")),
                ],
                orderBy: [new OrderItemProjectionColumn("id")]
            ),
            "1|1",
            "2|1",
            "3|",
            "4|2",
            "5|"
        );
    }

    [Fact]
    public void ACombinedSubqueryLeftJoinedKeepsItsUnmatchedRow()
    {
        // The distinct x are 1, null, 2 and 9; only 9 matches no n.
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries: [new SubqueryQuery("u", NumsOrNine())],
                from: new FromSubquery("u"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Left,
                        "t.nums",
                        new EqualDecimalRow(
                            new FieldIntegerNullable("t.nums", "n"),
                            new FieldIntegerNullable("u", "x")
                        )
                    ),
                ],
                select:
                [
                    new SelectItemProjection("x", new FieldIntegerNullable("u", "x")),
                    new SelectItemProjection(
                        "id",
                        new FieldIntegerNullable("t.nums", "id")
                    ),
                ],
                orderBy:
                [
                    new OrderItemProjectionColumn("x"),
                    new OrderItemProjectionColumn("id"),
                ]
            ),
            "|3",
            "|5",
            "1|1",
            "1|2",
            "2|4",
            "9|"
        );
    }

    [Fact]
    public void ACombinedSubqueryJoinedWithItselfPairsEachDistinctRow()
    {
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "u",
                        Combined(
                            "union",
                            Column(new FieldIntegerNullable("t.nums", "n")),
                            Column(new FieldIntegerNullable("t.nums", "n"))
                        )
                    ),
                ],
                from: new FromSubquery("u", "a"),
                joins:
                [
                    new JoinSubquery(
                        JoinKind.Inner,
                        "u",
                        new EqualDecimalRow(
                            new FieldIntegerNullable("a", "x"),
                            new FieldIntegerNullable("b", "x")
                        ),
                        "b"
                    ),
                ],
                select:
                [
                    new SelectItemProjection("a", new FieldIntegerNullable("a", "x")),
                    new SelectItemProjection("b", new FieldIntegerNullable("b", "x")),
                ],
                orderBy: [new OrderItemProjectionColumn("a")]
            ),
            "|",
            "1|1",
            "2|2"
        );
    }

    [Theory]
    [InlineData("union", "3|3|2")]
    [InlineData("unionAll", "6|6|2")]
    [InlineData("intersect", "1|2|1")]
    [InlineData("except", "2|1|1")]
    public void ATotalQueryAggregatesTheRowsOfACombinedSubquery(
        string operation,
        string expected
    )
    {
        // n: 1, 1, null, 2, null against the row 2: union {1, null, 2},
        // unionAll 1, 1, null, 2, null, 2, intersect {2}, except {1, null}.
        AssertRows(
            new MainTotalQuery(
                subqueries:
                [
                    new SubqueryQuery(
                        "u",
                        Combined(
                            operation,
                            Column(new FieldIntegerNullable("t.nums", "n")),
                            Literal(new LiteralInteger(2))
                        )
                    ),
                ],
                from: new FromSubquery("u"),
                aggregate:
                [
                    new SelectItemTotal("rows", new CountTotal()),
                    new SelectItemTotal(
                        "sum",
                        new SumIntegerTotal(new FieldIntegerNullable("u", "x"))
                    ),
                    new SelectItemTotal(
                        "distinct",
                        new CountDistinctTotal(new FieldIntegerNullable("u", "x"))
                    ),
                ]
            ),
            expected
        );
    }

    [Fact]
    public void ASumOverAUnionOfIntegersAndDecimalsAddsEachDistinctValueOnce()
    {
        // 2 and 2.0 are one row: 1 + 2 + 3.5.
        AssertRows(
            new MainTotalQuery(
                subqueries:
                [
                    new SubqueryQuery(
                        "u",
                        Combined(
                            "union",
                            Literal(new LiteralInteger(1)),
                            Literal(new LiteralInteger(2)),
                            Literal(new LiteralDecimal(2m)),
                            Literal(new LiteralDecimal(3.5m))
                        )
                    ),
                ],
                from: new FromSubquery("u"),
                aggregate:
                [
                    new SelectItemTotal("rows", new CountTotal()),
                    new SelectItemTotal(
                        "sum",
                        new SumDecimalTotal(new FieldDecimal("u", "x"))
                    ),
                ]
            ),
            "3|6.5"
        );
    }

    [Fact]
    public void AnInputPageIsKeptWhenTheCombinedSubqueryIsAggregated()
    {
        // The page is the two highest ids, 5 and 4: 5 + 4 + 100.
        AssertRows(
            new MainTotalQuery(
                subqueries:
                [
                    new SubqueryQuery(
                        "u",
                        Combined("unionAll", HighestIds(), Literal(new LiteralInteger(100)))
                    ),
                ],
                from: new FromSubquery("u"),
                aggregate:
                [
                    new SelectItemTotal("rows", new CountTotal()),
                    new SelectItemTotal(
                        "sum",
                        new SumIntegerTotal(new FieldInteger("u", "x"))
                    ),
                ]
            ),
            "3|109"
        );
    }

    [Fact]
    public void AnInputPageIsKeptWhenTheCombinedSubqueryIsTheListOfIn()
    {
        AssertRows(
            IdsWhere(
                Combined("union", HighestIds(), Literal(new LiteralInteger(1))),
                new InDecimalRow(
                    new FieldInteger("t.nums", "id"),
                    new ListSubqueryColumnInteger("u", "x")
                )
            ),
            "1",
            "4",
            "5"
        );
    }

    [Fact]
    public void APagedSubqueryReadOnlyByInKeepsTheRowsOfItsOrder()
    {
        AssertRows(
            IdsWhere(
                HighestIds(),
                new InDecimalRow(
                    new FieldInteger("t.nums", "id"),
                    new ListSubqueryColumnInteger("u", "x")
                )
            ),
            "4",
            "5"
        );
    }

    [Fact(
        Skip = "Bug: a subquery with orderBy + distinctBy (no pagination) read by in"
            + " drops distinctBy (QueryUse.Membership); spec keeps only the first row"
            + " of each distinctBy group: ids 2, 4, 5, library gives 1..5"
    )]
    public void ASubqueryWithDistinctByReadOnlyByInKeepsOnlyTheRowsItKeeps()
    {
        // distinctBy keeps, of the rows equal on n, the first by orderBy:
        // the ids 2 (n 1), 4 (n 2) and 5 (n null); in reads their ids.
        AssertRows(
            IdsWhere(
                new PlainQueryWithoutPaginationWithOrderBy(
                    from: new FromEntity("t.nums"),
                    select:
                    [
                        new SelectItemProjection(
                            "n",
                            new FieldIntegerNullable("t.nums", "n")
                        ),
                        new SelectItemProjection("x", new FieldInteger("t.nums", "id")),
                    ],
                    orderBy: [new OrderItemProjectionColumn("x", SortDirection.Desc)],
                    distinctBy: ["n"]
                ),
                new InDecimalRow(
                    new FieldInteger("t.nums", "id"),
                    new ListSubqueryColumnInteger("u", "x")
                )
            ),
            "2",
            "4",
            "5"
        );
    }

    [Fact(
        Skip = "Bug: a subquery with orderBy + distinctBy (no pagination) read by in"
            + " drops distinctBy (QueryUse.Membership); spec keeps only the first row"
            + " of each distinctBy group: ids 2, 4, 5, library gives 1..5"
    )]
    public void AnInputThatReadsASubqueryWithDistinctByByInKeepsOnlyItsRows()
    {
        // The first input is the ids that are in u (2, 4, 5), the second
        // the id 1, so the union is 1, 2, 4 and 5.
        AssertUnordered(
            new MainCombinedQueryUnion(
                [
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromEntity("t.nums"),
                        where: new InDecimalRow(
                            new FieldInteger("t.nums", "id"),
                            new ListSubqueryColumnInteger("u", "id")
                        ),
                        select:
                        [
                            new SelectItemProjection(
                                "x",
                                new FieldInteger("t.nums", "id")
                            ),
                        ]
                    ),
                    Literal(new LiteralInteger(1)),
                ],
                [new SubqueryQuery("u", LatestOfEachN())]
            ),
            "1",
            "2",
            "4",
            "5"
        );
    }

    [Fact]
    public void APagedSubqueryReadOnlyByAnAggregateKeepsTheRowsOfItsOrder()
    {
        AssertRows(
            new MainTotalQuery(
                subqueries: [new SubqueryQuery("u", HighestIds())],
                from: new FromSubquery("u"),
                aggregate:
                [
                    new SelectItemTotal(
                        "sum",
                        new SumIntegerTotal(new FieldInteger("u", "x"))
                    ),
                ]
            ),
            "9"
        );
    }

    [Fact]
    public void AnInputDistinctByIsKeptWhenTheCombinedSubqueryIsAggregated()
    {
        // The latest id of each n: 2 for 1, 4 for 2, 5 for null; and 100.
        AssertRows(
            new MainTotalQuery(
                subqueries:
                [
                    new SubqueryQuery(
                        "u",
                        Combined(
                            "unionAll",
                            LatestOfEachN(),
                            new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.one"),
                                select:
                                [
                                    new SelectItemProjection(
                                        "n",
                                        new LiteralIntegerNullable()
                                    ),
                                    new SelectItemProjection(
                                        "id",
                                        new LiteralInteger(100)
                                    ),
                                ]
                            )
                        )
                    ),
                ],
                from: new FromSubquery("u"),
                aggregate:
                [
                    new SelectItemTotal("rows", new CountTotal()),
                    new SelectItemTotal(
                        "sum",
                        new SumIntegerTotal(new FieldInteger("u", "id"))
                    ),
                ]
            ),
            "4|111"
        );
    }

    [Fact]
    public void TheSymmetricDifferenceIsTheUnionExceptTheIntersection()
    {
        // {1, null, 2} and {2, 3}: the union except the intersection {2}.
        Query left = Column(new FieldIntegerNullable("t.nums", "n"));
        Query right = Combined(
            "unionAll",
            Literal(new LiteralInteger(2)),
            Literal(new LiteralInteger(3))
        );

        AssertUnordered(
            MainQuery(
                "except",
                Combined("union", left, right),
                Combined("intersect", left, right)
            ),
            "",
            "1",
            "3"
        );
    }

    [Fact]
    public void AnInputExceptItselfHasNoRows()
    {
        AssertUnordered(
            MainQuery(
                "except",
                Column(new FieldIntegerNullable("t.nums", "n")),
                Column(new FieldIntegerNullable("t.nums", "n"))
            )
        );
    }

    [Fact]
    public void AnInputIntersectItselfIsItsDistinctRows()
    {
        AssertUnordered(
            MainQuery("intersect", PairsOfNums(), PairsOfNums()),
            "|",
            "1|x",
            "2|y"
        );
    }

    [Fact]
    public void AnExceptAsTheSecondInputOfAnExceptDropsOnlyItsRows()
    {
        // n except ({1, null, 2} except {2}) = n except {1, null} = {2}.
        AssertUnordered(
            MainQuery(
                "except",
                Column(new FieldIntegerNullable("t.nums", "n")),
                Combined(
                    "except",
                    Column(new FieldIntegerNullable("t.nums", "n")),
                    Literal(new LiteralInteger(2))
                )
            ),
            "2"
        );
    }

    [Fact]
    public void OneInstantStoredWithTwoOffsetsIsOneRow()
    {
        // Temporal types: equality of a datetime is by instant.
        AssertUnordered(
            MainQuery(
                "union",
                Spelled(new FieldDatetime("t.spelled", "at")),
                Literal(
                    new LiteralDatetime(
                        new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
                    )
                )
            ),
            "2024-01-01T00:00:00Z"
        );
    }

    [Fact]
    public void OneUuidStoredInTwoCasesIsOneRow()
    {
        // Ordering and equality: a uuid is equal by value.
        AssertUnordered(
            MainQuery(
                "union",
                Spelled(new FieldUuid("t.spelled", "uuid")),
                Literal(new LiteralUuid(new Guid("0000000a-0000-0000-0000-00000000000b")))
            ),
            "0000000a-0000-0000-0000-00000000000b"
        );
    }

    [Fact]
    public void AQuotientRoundedToTwentyEightDigitsIsTheSameRowAsItsLiteral()
    {
        // divide rounds to 28 significant digits: 10 / 3 is
        // 3.333333333333333333333333333 (Integer and decimal arithmetic).
        string third = "3." + new string('3', 27);

        AssertUnordered(
            MainQuery(
                "union",
                Spelled(
                    new DivideDecimalProjection([
                        new FieldDecimal("t.spelled", "ten"),
                        new LiteralInteger(3),
                    ])
                ),
                Literal(new LiteralDecimal(decimal.Parse(third, CultureInfo.InvariantCulture)))
            ),
            third
        );
    }

    [Fact]
    public void ColumnsInAnotherOrderAndOfAnotherTypeAreMergedByAlias()
    {
        // The first input has (n integer, s string), the second (s
        // nullable string, n decimal): n is a decimal and s a nullable
        // string, in the order of the first input.
        PureQLProjection projection = new(
            Tables,
            MainQuery(
                "unionAll",
                Pair("n", new LiteralInteger(1), "s", new LiteralString("x")),
                Pair("s", new LiteralStringNullable(), "n", new LiteralDecimal(0.5m))
            )
        );

        Assert.Equal(["n:decimal", "s:string"], ColumnTypes(projection));
        AssertUnordered(projection, "0.5|", "1|x");
    }

    [Fact]
    public void ColumnsInAnotherOrderAndOfAnotherTypeAreReadByTheirMergedType()
    {
        Query combined = Combined(
            "union",
            Pair("n", new LiteralInteger(1), "s", new LiteralString("x")),
            Pair("s", new LiteralStringNullable(), "n", new LiteralDecimal(0.5m))
        );

        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries: [new SubqueryQuery("u", combined)],
                from: new FromSubquery("u"),
                select:
                [
                    new SelectItemProjection("n", new FieldDecimal("u", "n")),
                    new SelectItemProjection("s", new FieldStringNullable("u", "s")),
                ],
                orderBy: [new OrderItemProjectionColumn("n")]
            ),
            "0.5|",
            "1|x"
        );
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                new MainPlainQueryWithoutPagination(
                    subqueries: [new SubqueryQuery("u", combined)],
                    from: new FromSubquery("u"),
                    select: [new SelectItemProjection("s", new FieldString("u", "s"))]
                )
            )
        );
    }

    [Fact]
    public void AGroupedInputWithItsOwnPageIsCombined()
    {
        // The s of nums with the most rows (x and null have two each, the
        // order desc then by s puts x first: null sorts last descending)
        // unioned with y.
        AssertUnordered(
            MainQuery(
                "union",
                new GroupedQueryWithPagination(
                    from: new FromEntity("t.nums"),
                    groupBy: [new GroupKey("s", new FieldStringNullable("t.nums", "s"))],
                    select:
                    [
                        new SelectItemGroup("x", new KeyStringNullable("s")),
                        new SelectItemGroup("rows", new CountGroup()),
                    ],
                    orderBy:
                    [
                        new OrderItemGroupColumn("rows", SortDirection.Desc),
                        new OrderItemGroupColumn("x", SortDirection.Desc),
                    ],
                    pagination: new Pagination(take: 1L)
                ),
                Pair("x", new LiteralString("y"), "rows", new LiteralInteger(1))
            ),
            "x|2",
            "y|1"
        );
    }

    [Fact]
    public void ACombinedSubqueryIsPagedByParameters()
    {
        // A skip parameter bound to 0 skips nothing (distinctBy and
        // pagination).
        AssertRows(
            new PureQLProjection(
                Tables,
                Sorted(
                    SortDirection.Desc,
                    new Pagination(
                        skip: new ParamInteger("skip"),
                        take: new ParamInteger("take")
                    )
                ),
                new Dictionary<string, IParameter>
                {
                    ["skip"] = new TextParameter("integer", "0"),
                    ["take"] = new TextParameter("integer", "2"),
                }
            ),
            "3",
            "2"
        );
    }

    [Fact]
    public void ACombinedSubqueryOfStringsIsSortedByCodePoint()
    {
        // U+FFFD sorts before U+1F600 by code point, though not by UTF-16
        // code unit.
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "u",
                        Combined(
                            "union",
                            Literal(new LiteralString("\U0001F600")),
                            Literal(new LiteralString("\uFFFD")),
                            Literal(new LiteralString("z"))
                        )
                    ),
                ],
                from: new FromSubquery("u"),
                select: [new SelectItemProjection("x", new FieldString("u", "x"))],
                orderBy: [new OrderItemProjectionColumn("x")]
            ),
            "z",
            "\uFFFD",
            "\U0001F600"
        );
    }

    [Fact]
    public void ColumnsComeInTheOrderOfTheFirstInputOfANestedFirstInput()
    {
        PureQLProjection projection = new(
            Tables,
            MainQuery(
                "union",
                Combined(
                    "unionAll",
                    Pair("b", new LiteralString("x"), "a", new LiteralInteger(1)),
                    Pair("a", new LiteralDecimal(2.5m), "b", new LiteralString("y"))
                ),
                Pair("a", new LiteralInteger(1), "b", new LiteralString("x"))
            )
        );

        Assert.Equal(["b:string", "a:decimal"], ColumnTypes(projection));
        AssertUnordered(projection, "x|1", "y|2.5");
    }

    [Fact]
    public void AliasesThatDifferInCaseAreDifferentColumns()
    {
        // Names and sources: names compare exactly, code point by code
        // point.
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                MainQuery(
                    "union",
                    Literal(new LiteralInteger(1)),
                    Literal(new LiteralInteger(1), "X")
                )
            )
        );
    }

    [Fact]
    public void ATimeAndADatetimeColumnDoNotMeet()
    {
        AssertDoNotMeet(
            new LiteralTime(new TimeOnly(1, 0)),
            new LiteralDatetime(new DateTimeOffset(2024, 1, 1, 1, 0, 0, TimeSpan.Zero))
        );
    }

    [Fact]
    public void AUuidAndAStringColumnDoNotMeet()
    {
        AssertDoNotMeet(
            new LiteralUuid(new Guid("0000000a-0000-0000-0000-00000000000b")),
            new LiteralString("0000000a-0000-0000-0000-00000000000b")
        );
    }

    [Fact]
    public void ABooleanAndAStringColumnDoNotMeet()
    {
        AssertDoNotMeet(new LiteralBoolean(true), new LiteralString("True"));
    }

    [Fact]
    public void AnIntegerAndADateColumnDoNotMeet()
    {
        AssertDoNotMeet(
            new LiteralInteger(20240101),
            new LiteralDate(new DateOnly(2024, 1, 1))
        );
    }

    [Fact]
    public void ANullIntegerAndAStringColumnDoNotMeet()
    {
        AssertDoNotMeet(new LiteralIntegerNullable(), new LiteralString("a"));
    }

    [Fact]
    public void ANullStringAndANullIntegerColumnDoNotMeet()
    {
        AssertDoNotMeet(new LiteralStringNullable(), new LiteralIntegerNullable());
    }

    [Fact]
    public void ADecimalAndATimeColumnInANestedInputDoNotMeet()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                Tables,
                MainQuery(
                    "union",
                    Literal(new LiteralDecimal(1.5m)),
                    Combined(
                        "unionAll",
                        Literal(new LiteralTime(new TimeOnly(1, 0))),
                        Literal(new LiteralTime(new TimeOnly(2, 0)))
                    )
                )
            )
        );
    }

    // One row of the column x (or another alias) from t.one.
    private static Query Literal(ExprProjection value, string alias = "x")
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.one"),
            select: [new SelectItemProjection(alias, value)]
        );
    }

    // The column x of every row of t.spelled.
    private static Query Spelled(ExprProjection value)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.spelled"),
            select: [new SelectItemProjection("x", value)]
        );
    }

    // One row of two named columns from t.one.
    private static Query Pair(
        string first,
        ExprProjection a,
        string second,
        ExprProjection b
    )
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.one"),
            select: [new SelectItemProjection(first, a), new SelectItemProjection(second, b)]
        );
    }

    // One row (a, b) from t.one.
    private static Query Pair(ExprProjection a, ExprProjection b)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.one"),
            select: [new SelectItemProjection("a", a), new SelectItemProjection("b", b)]
        );
    }

    // One row of a value of every other type and the given time.
    private static Query Typed(TimeOnly time)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.one"),
            select:
            [
                new SelectItemProjection(
                    "date",
                    new LiteralDate(new DateOnly(2024, 2, 29))
                ),
                new SelectItemProjection("time", new LiteralTime(time)),
                new SelectItemProjection(
                    "datetime",
                    new LiteralDatetime(
                        new DateTimeOffset(2024, 2, 29, 23, 59, 58, TimeSpan.Zero)
                    )
                ),
                new SelectItemProjection(
                    "uuid",
                    new LiteralUuid(new Guid("0000000a-0000-0000-0000-00000000000b"))
                ),
                new SelectItemProjection("boolean", new LiteralBoolean(true)),
            ]
        );
    }

    // The column x of every row of t.nums.
    private static Query Column(ExprProjection value)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.nums"),
            select: [new SelectItemProjection("x", value)]
        );
    }

    // n, s of every row of t.nums.
    private static Query PairsOfNums()
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity("t.nums"),
            select:
            [
                new SelectItemProjection("n", new FieldIntegerNullable("t.nums", "n")),
                new SelectItemProjection("s", new FieldStringNullable("t.nums", "s")),
            ]
        );
    }

    // The distinct x: null and 1.
    private static Query NullOrOne()
    {
        return Combined(
            "union",
            Literal(new LiteralIntegerNullable()),
            Literal(new LiteralInteger(1))
        );
    }

    // The distinct x: 1, null, 2 and 9.
    private static Query NumsOrNine()
    {
        return Combined(
            "union",
            Column(new FieldIntegerNullable("t.nums", "n")),
            Literal(new LiteralInteger(9))
        );
    }

    // The ids 5 and 4: the first two in descending order.
    private static Query HighestIds()
    {
        return new PlainQueryWithPagination(
            from: new FromEntity("t.nums"),
            select: [new SelectItemProjection("x", new FieldInteger("t.nums", "id"))],
            orderBy: [new OrderItemProjectionColumn("x", SortDirection.Desc)],
            pagination: new Pagination(take: 2L)
        );
    }

    // For each n, the row of its highest id: (1, 2), (null, 5), (2, 4).
    private static Query LatestOfEachN()
    {
        return new PlainQueryWithoutPaginationWithOrderBy(
            from: new FromEntity("t.nums"),
            select:
            [
                new SelectItemProjection("n", new FieldIntegerNullable("t.nums", "n")),
                new SelectItemProjection("id", new FieldInteger("t.nums", "id")),
            ],
            orderBy: [new OrderItemProjectionColumn("id", SortDirection.Desc)],
            distinctBy: ["n"]
        );
    }

    // The ids of t.nums that the condition keeps, the condition reading
    // the subquery u.
    private static PureQLQuery IdsWhere(Query u, BooleanRow condition)
    {
        return new MainPlainQueryWithoutPagination(
            subqueries: [new SubqueryQuery("u", u)],
            from: new FromEntity("t.nums"),
            where: condition,
            select: [new SelectItemProjection("id", new FieldInteger("t.nums", "id"))],
            orderBy: [new OrderItemProjectionColumn("id")]
        );
    }

    // The column x of 1, 1, null, 2, null and 3, sorted and paged.
    private static PureQLQuery Sorted(SortDirection direction, Pagination pagination)
    {
        return new MainPlainQueryWithPagination(
            subqueries:
            [
                new SubqueryQuery(
                    "u",
                    Combined(
                        "unionAll",
                        Column(new FieldIntegerNullable("t.nums", "n")),
                        Literal(new LiteralInteger(3))
                    )
                ),
            ],
            from: new FromSubquery("u"),
            select: [new SelectItemProjection("x", new FieldIntegerNullable("u", "x"))],
            orderBy: [new OrderItemProjectionColumn("x", direction)],
            pagination: pagination
        );
    }

    // The rows of a combined string column x, the rows where it is null
    // and the rows where it equals the given value.
    private static PureQLQuery Counting(
        Query combined,
        StringNullableRow x,
        StringNullableRow value
    )
    {
        return new MainTotalQuery(
            subqueries: [new SubqueryQuery("u", combined)],
            from: new FromSubquery("u"),
            aggregate:
            [
                new SelectItemTotal("rows", new CountTotal()),
                new SelectItemTotal(
                    "nulls",
                    new CountTotal(new EqualStringRow(x, new LiteralStringNullable()))
                ),
                new SelectItemTotal("equal", new CountTotal(new EqualStringRow(x, value))),
            ]
        );
    }

    // The column x of a combined subquery u, read through the given field.
    private static PureQLProjection Reading(Query combined, ExprProjection field)
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

    private static void AssertDoNotMeet(ExprProjection a, ExprProjection b)
    {
        foreach (string operation in new[] { "unionAll", "union", "intersect", "except" })
        {
            _ = Assert.Throws<ArgumentException>(() =>
                new PureQLProjection(
                    Tables,
                    MainQuery(operation, Literal(a), Literal(b))
                )
            );
        }
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
        IReadOnlyList<string> columns =
        [
            .. projection.TableSchema.Columns.Select(column => column.Name.TextValue),
        ];

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
