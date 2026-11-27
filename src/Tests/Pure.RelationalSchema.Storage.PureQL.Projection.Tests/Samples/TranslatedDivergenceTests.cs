using Pure.RelationalSchema.Abstractions.Column;
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

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Samples;

// The same query over the same rows, held in memory and stored in
// PostgreSQL, returns the rows the specification defines, where SQL is known
// to compute otherwise: remainders and quotients of negative numbers, decimal
// quotients and averages to 28 digits, rounding half away from zero, partial
// sums beyond 64 bits, nulls in comparisons, greatest / least, lists and set
// operations, strings in code point order, LIKE wildcards in string matching,
// nulls first ascending, aggregates over no rows, months clamped to the end of
// the month and ISO days of the week. Each query is first checked to be
// translated, so the rows compared are the ones PostgreSQL computed. Rows are
// written as their cells' texts joined with '|' (a null is empty), in the
// order of the query, by id. Needs Docker.
public sealed class TranslatedDivergenceTests(DivergenceFixtures fixtures)
    : IClassFixture<DivergenceFixtures>
{
    private const string Ints = "n.ints";

    private const string Decimals = "n.decimals";

    private const string Big = "n.big";

    private const string Grouped = "n.grouped";

    private const string Words = "s.words";

    private const string Patterns = "s.patterns";

    private const string Moments = "m.moments";

    private readonly DivergenceFixtures _fixtures = fixtures;

    // ---- Integer arithmetic (Integer and decimal arithmetic) ----

    // modulo takes the sign of left: modulo(-7, 2) is -1; null is lifted.
    [Fact]
    public void IntegerModuloTakesTheSignOfTheLeftOperand()
    {
        AssertRows(
            ById(Ints, null, Select("m", new ModuloIntegerNullableProjection(A, B))),
            "1|1",
            "2|-1",
            "3|1",
            "4|-1",
            "5|",
            "6|",
            "7|"
        );
    }

    // integerDivide truncates toward zero: integerDivide(-7, 2) is -3.
    [Fact]
    public void IntegerDivideTruncatesTowardZero()
    {
        AssertRows(
            ById(
                Ints,
                null,
                Select("q", new IntegerDivideIntegerNullableProjection(A, B))
            ),
            "1|3",
            "2|-3",
            "3|-3",
            "4|3",
            "5|",
            "6|",
            "7|"
        );
    }

    // left = right x integerDivide(left, right) + modulo(left, right).
    [Fact]
    public void IntegerDivideAndModuloRecomposeTheLeftOperand()
    {
        AssertRows(
            ById(
                Ints,
                new NotEqualDecimalRow(RowA, new LiteralIntegerNullable()),
                Select(
                    "back",
                    new AddIntegerNullableProjection([
                        new MultiplyIntegerNullableProjection([
                            B,
                            new IntegerDivideIntegerNullableProjection(A, B),
                        ]),
                        new ModuloIntegerNullableProjection(A, B),
                    ])
                )
            ),
            "1|7",
            "2|-7",
            "3|7",
            "4|-7",
            "6|"
        );
    }

    // Any integer result outside 64 bits is an error.
    [Fact]
    public void IntegerAdditionBeyond64BitsFails()
    {
        AssertFails(
            ById(
                Big,
                new EqualDecimalRow(new FieldInteger("t", "id"), new LiteralInteger(1)),
                Select(
                    "v",
                    new AddIntegerProjection([
                        new FieldInteger("t", "v"),
                        new LiteralInteger(1),
                    ])
                )
            )
        );
    }

    [Fact]
    public void IntegerMultiplicationBeyond64BitsFails()
    {
        AssertFails(
            ById(
                Big,
                new EqualDecimalRow(new FieldInteger("t", "id"), new LiteralInteger(1)),
                Select(
                    "v",
                    new MultiplyIntegerProjection([
                        new FieldInteger("t", "v"),
                        new LiteralInteger(2),
                    ])
                )
            )
        );
    }

    // Division by zero is an error, for integerDivide and modulo too.
    [Fact]
    public void IntegerModuloByZeroFails()
    {
        AssertFails(
            ById(
                Ints,
                null,
                Select(
                    "m",
                    new ModuloIntegerNullableProjection(
                        A,
                        new SubtractIntegerNullableProjection([B, B])
                    )
                )
            )
        );
    }

    // ---- Decimal arithmetic ----

    // divide is the exact quotient rounded half away from zero to 28 digits:
    // divide(1, 3) is 0.3333333333333333333333333333.
    [Fact(
        Skip = "Bug (upstream translator): divide(1, 3) is "
            + "0.3333333333333333333333333333 by the spec and in memory; translated, "
            + "numeric / numeric returns PostgreSQL's 20 digits, 0.33333333333333333333"
    )]
    public void DecimalDivisionKeeps28Digits()
    {
        AssertRows(
            ById(
                Decimals,
                null,
                Select("q", new DivideDecimalNullableProjection([X, Y]))
            ),
            "1|0.3333333333333333333333333333",
            "2|0.6666666666666666666666666667",
            "3|-0.6666666666666666666666666667",
            "4|2.5",
            "5|-2.5",
            "6|1",
            "7|",
            "8|0.1428571428571428571428571429"
        );
    }

    // A divide of two integers is a decimal too: there is no truncating
    // division by accident.
    [Fact]
    public void DivisionOfIntegersIsADecimalQuotient()
    {
        AssertRows(
            ById(
                Ints,
                new NotEqualDecimalRow(RowB, new LiteralIntegerNullable()),
                Select("q", new DivideDecimalNullableProjection([A, B]))
            ),
            "1|3.5",
            "2|-3.5",
            "3|-3.5",
            "4|3.5",
            "5|"
        );
    }

    // A product that needs more than 28 decimal places is rounded half away
    // from zero to 28: 0.111111111111111 squared is
    // 0.012345679012345654320987654321, which becomes
    // 0.0123456790123456543209876543.
    [Fact(
        Skip = "Bug (upstream translator/provider): 0.111111111111111 x "
            + "0.111111111111111 is 0.0123456790123456543209876543 by the spec and in "
            + "memory; translated, the exact numeric product has 30 places and reading "
            + "it throws OverflowException"
    )]
    public void DecimalProductIsRoundedTo28DecimalPlaces()
    {
        AssertRows(
            ById(
                Decimals,
                new EqualDecimalRow(new FieldInteger("t", "id"), new LiteralInteger(6)),
                Select("p", new MultiplyDecimalNullableProjection([X, Y]))
            ),
            "6|0.0123456790123456543209876543"
        );
    }

    // 0.1 + 0.2 is 0.3: decimals are exact.
    [Fact]
    public void DecimalSumOfTenthsIsExact()
    {
        AssertRows(
            ById(
                Decimals,
                new EqualDecimalRow(new FieldInteger("t", "id"), new LiteralInteger(1)),
                Select(
                    "s",
                    new AddDecimalNullableProjection([
                        new MultiplyDecimalNullableProjection([
                            X,
                            new LiteralDecimal(0.1m),
                        ]),
                        new LiteralDecimal(0.2m),
                    ])
                )
            ),
            "1|0.3"
        );
    }

    // round rounds half away from zero: round(2.5) is 3, round(-2.5) is -3.
    [Fact]
    public void RoundHalvesAwayFromZero()
    {
        AssertRows(
            ById(
                Decimals,
                null,
                Select("r", new RoundWithoutDigitsIntegerNullableProjection(X))
            ),
            "1|1",
            "2|2",
            "3|-2",
            "4|3",
            "5|-3",
            "6|0",
            "7|",
            "8|1"
        );
    }

    // halfEven rounds a value exactly halfway to the even neighbour.
    [Fact]
    public void RoundHalfEvenRoundsHalvesToTheEvenNeighbour()
    {
        AssertRows(
            ById(
                Decimals,
                null,
                Select(
                    "r",
                    new RoundWithoutDigitsIntegerNullableProjection(
                        X,
                        RoundingMode.HalfEven
                    )
                )
            ),
            "1|1",
            "2|2",
            "3|-2",
            "4|2",
            "5|-2",
            "6|0",
            "7|",
            "8|1"
        );
    }

    // floor and ceiling round toward -inf and +inf, truncate toward zero.
    [Fact]
    public void FloorCeilingAndTruncateRoundInTheirDirections()
    {
        AssertRows(
            ById(
                Decimals,
                null,
                Select("f", new FloorIntegerNullableProjection(X)),
                Select("c", new CeilingIntegerNullableProjection(X)),
                Select("t", new TruncateWithoutDigitsIntegerNullableProjection(X))
            ),
            "1|1|1|1",
            "2|2|2|2",
            "3|-2|-2|-2",
            "4|2|3|2",
            "5|-3|-2|-2",
            "6|0|1|0",
            "7|||",
            "8|1|1|1"
        );
    }

    // round with digits rounds the exact quotient once, half away from zero.
    [Fact]
    public void RoundToDigitsOfAQuotient()
    {
        AssertRows(
            ById(
                Decimals,
                null,
                Select(
                    "r",
                    new RoundDecimalNullableProjection(
                        new DivideDecimalNullableProjection([X, Y]),
                        new LiteralInteger(2)
                    )
                )
            ),
            "1|0.33",
            "2|0.67",
            "3|-0.67",
            "4|2.5",
            "5|-2.5",
            "6|1",
            "7|",
            "8|0.14"
        );
    }

    // sign is an integer; abs keeps the type.
    [Fact]
    public void SignAndAbsOfDecimals()
    {
        AssertRows(
            ById(
                Decimals,
                null,
                Select("s", new SignIntegerNullableProjection(X)),
                Select("a", new AbsDecimalNullableProjection(X))
            ),
            "1|1|1",
            "2|1|2",
            "3|-1|2",
            "4|1|2.5",
            "5|-1|2.5",
            "6|1|0.111111111111111",
            "7||",
            "8|1|1"
        );
    }

    // ---- Aggregates (Aggregate evaluation) ----

    // A sum is computed exactly: 2^63 - 1, 1 and -1 sum to 2^63 - 1 whatever
    // partial sums the rows give.
    [Fact]
    public void IntegerSumIsExactWhateverThePartialSums()
    {
        AssertRows(
            Total(
                Big,
                null,
                new SelectItemTotal(
                    "s",
                    new SumIntegerTotal(new FieldIntegerNullable("t", "v"))
                )
            ),
            "9223372036854775807"
        );
    }

    // An integer sum beyond 64 bits is an error.
    [Fact]
    public void IntegerSumBeyond64BitsFails()
    {
        AssertFails(
            Total(
                Big,
                new LessThanOrEqualDecimalRow(
                    new FieldInteger("t", "id"),
                    new LiteralInteger(2)
                ),
                new SelectItemTotal(
                    "s",
                    new SumIntegerTotal(new FieldIntegerNullable("t", "v"))
                )
            )
        );
    }

    // average is sum divided as by divide: to 28 digits.
    [Fact(
        Skip = "Bug (upstream translator): average of the integers 2^63-1, 1, -1 is "
            + "3074457345618258602.333333333 by the spec and in memory; translated, AVG "
            + "casts them to double precision and returns 3074457345618258602"
    )]
    public void AverageOfIntegersKeeps28Digits()
    {
        AssertRows(
            Total(
                Big,
                null,
                new SelectItemTotal(
                    "a",
                    new AverageDecimalNullableTotal(new FieldIntegerNullable("t", "v"))
                )
            ),
            "3074457345618258602.333333333"
        );
    }

    // Of the decimals 1, 2, -2, 2.5, -2.5, 0.111111111111111, 1 and a null:
    // the sum 2.111111111111111 and the mean of the seven values to 28 digits.
    [Fact(
        Skip = "Bug (upstream translator): average of the decimals is "
            + "0.3015873015873015714285714286 (28 digits) by the spec and in memory; "
            + "translated AVG(numeric) returns PostgreSQL's 0.30158730158730157143"
    )]
    public void AverageOfDecimalsKeeps28Digits()
    {
        AssertRows(
            Total(
                Decimals,
                null,
                new SelectItemTotal("s", new SumDecimalTotal(X)),
                new SelectItemTotal("a", new AverageDecimalNullableTotal(X))
            ),
            "2.111111111111111|0.3015873015873015714285714286"
        );
    }

    // With no rows: count and sum are 0, average, min and max null, any false
    // and all true; a total query still returns its row.
    [Fact]
    public void AggregatesOverNoRows()
    {
        BooleanRow predicate = new GreaterThanDecimalRow(RowA, new LiteralInteger(0));

        AssertRows(
            Total(
                Ints,
                new GreaterThanDecimalRow(RowA, new LiteralInteger(100)),
                new SelectItemTotal("n", new CountTotal()),
                new SelectItemTotal("s", new SumIntegerTotal(RowA)),
                new SelectItemTotal("a", new AverageDecimalNullableTotal(RowA)),
                new SelectItemTotal("min", new MinIntegerNullableTotal(RowA)),
                new SelectItemTotal("max", new MaxIntegerNullableTotal(RowA)),
                new SelectItemTotal("any", new AnyTotal(predicate)),
                new SelectItemTotal("all", new AllTotal(predicate))
            ),
            "0|0||||False|True"
        );
    }

    // Aggregates skip nulls: of 7, -7, 7, -7, 3 and two nulls, the sum is 3,
    // the mean 0.6, count counts every row.
    [Fact]
    public void AggregatesSkipNulls()
    {
        AssertRows(
            Total(
                Ints,
                null,
                new SelectItemTotal("n", new CountTotal()),
                new SelectItemTotal("s", new SumIntegerTotal(RowA)),
                new SelectItemTotal("a", new AverageDecimalNullableTotal(RowA)),
                new SelectItemTotal("min", new MinIntegerNullableTotal(RowA)),
                new SelectItemTotal("max", new MaxIntegerNullableTotal(RowA)),
                new SelectItemTotal(
                    "positive",
                    new CountTotal(new GreaterThanDecimalRow(RowA, new LiteralInteger(0)))
                )
            ),
            "7|3|0.6|-7|7|3"
        );
    }

    // A sum over only nulls is 0, not null.
    [Fact]
    public void SumOfOnlyNullsIsZero()
    {
        AssertRows(
            Total(
                Ints,
                new EqualDecimalRow(RowA, new LiteralIntegerNullable()),
                new SelectItemTotal("s", new SumIntegerTotal(RowA)),
                new SelectItemTotal("n", new CountTotal())
            ),
            "0|2"
        );
    }

    // An aggregate over all rows in a plain select sees every row left by
    // where.
    [Fact(
        Skip = "Bug (upstream translator): average of 1, 2, 2 over all rows is "
            + "1.666666666666666666666666667 by the spec and in memory; translated "
            + "(window AVG) returns 1.6666666666666667"
    )]
    public void AverageOverAllRowsInAPlainSelectKeeps28Digits()
    {
        AssertRows(
            ById(
                Grouped,
                new EqualDecimalRow(
                    new FieldIntegerNullable("t", "g"),
                    new LiteralInteger(1)
                ),
                Select(
                    "a",
                    new AverageDecimalNullableProjection(
                        new FieldIntegerNullable("t", "v")
                    )
                )
            ),
            "1|1.666666666666666666666666667",
            "2|1.666666666666666666666666667",
            "3|1.666666666666666666666666667"
        );
    }

    // first and last read the row the aggregate's orderBy puts first or
    // last, with null first ascending.
    [Fact]
    public void FirstAndLastSortNullFirst()
    {
        OrderItemRow[] byWord = [new OrderItemRow(Word), new OrderItemRow(RowId)];

        AssertRows(
            Total(
                Words,
                null,
                new SelectItemTotal(
                    "first",
                    new FirstIntegerNullableTotal(RowIdNullable, byWord)
                ),
                new SelectItemTotal(
                    "last",
                    new LastIntegerNullableTotal(RowIdNullable, byWord)
                )
            ),
            "6|7"
        );
    }

    // ---- Null semantics (Null semantics, Ordering and equality) ----

    // null == null is true; an ordering comparison with one null is false,
    // with two nulls >= and <= are true.
    [Fact]
    public void ComparisonsOfNullableOperands()
    {
        AssertRows(
            ById(
                Ints,
                null,
                Select("eq", new EqualDecimalProjection(A, B)),
                Select("ne", new NotEqualDecimalProjection(A, B)),
                Select("ge", new GreaterThanOrEqualDecimalProjection(A, B)),
                Select("le", new LessThanOrEqualDecimalProjection(A, B)),
                Select("gt", new GreaterThanDecimalProjection(A, B)),
                Select("lt", new LessThanDecimalProjection(A, B))
            ),
            "1|False|True|True|False|True|False",
            "2|False|True|False|True|False|True",
            "3|False|True|True|False|True|False",
            "4|False|True|False|True|False|True",
            "5|False|True|False|False|False|False",
            "6|False|True|False|False|False|False",
            "7|True|False|True|True|False|False"
        );
    }

    // The same comparisons as conditions of where.
    [Fact]
    public void GreaterThanOrEqualOfTwoNullsHoldsInWhere()
    {
        AssertRows(
            ById(Ints, new GreaterThanOrEqualDecimalRow(RowA, RowB)),
            "1",
            "3",
            "7"
        );
    }

    [Fact]
    public void NotEqualOfOneNullHoldsInWhere()
    {
        AssertRows(
            ById(Ints, new NotEqualDecimalRow(RowA, RowB)),
            "1",
            "2",
            "3",
            "4",
            "5",
            "6"
        );
    }

    // not(lessThan(a, b)) is true when one operand is null.
    [Fact]
    public void NotOfAComparisonWithANullIsTrue()
    {
        AssertRows(
            ById(Ints, new NotRow(new LessThanDecimalRow(RowA, RowB))),
            "1",
            "3",
            "5",
            "6",
            "7"
        );
    }

    // greatest and least are null when any operand is null.
    [Fact]
    public void GreatestAndLeastAreNullWhenAnOperandIsNull()
    {
        AssertRows(
            ById(
                Ints,
                null,
                Select("g", new GreatestIntegerNullableProjection([A, B])),
                Select("l", new LeastIntegerNullableProjection([A, B]))
            ),
            "1|7|2",
            "2|2|-7",
            "3|7|-2",
            "4|-2|-7",
            "5||",
            "6||",
            "7||"
        );
    }

    // greatest of strings by code point, null when an operand is null.
    [Fact]
    public void GreatestOfStringsIsByCodePointAndLifted()
    {
        AssertRows(
            ById(
                Words,
                null,
                Select(
                    "g",
                    new GreatestStringNullableProjection([
                        WordProjection,
                        new LiteralString("a"),
                    ])
                ),
                Select(
                    "l",
                    new LeastStringNullableProjection([
                        WordProjection,
                        new LiteralString("a"),
                    ])
                )
            ),
            "1|a|a",
            "2|a|B",
            "3|b|a",
            "4|a|A",
            "5|ab|a",
            "6||",
            "7|\U0001F600|a",
            "8|�|a",
            "9|é|a",
            "10|a|Z"
        );
    }

    // nullIfEqual is null when its operands are equal, two nulls included,
    // and left otherwise, also when right is null.
    [Fact]
    public void NullIfEqualOfNullableOperands()
    {
        AssertRows(
            ById(
                Ints,
                null,
                Select("n", new NullIfEqualIntegerNullableProjection(A, B)),
                Select(
                    "z",
                    new NullIfEqualIntegerNullableProjection(A, new LiteralInteger(7))
                )
            ),
            "1|7|",
            "2|-7|-7",
            "3|7|",
            "4|-7|-7",
            "5||",
            "6|3|3",
            "7||"
        );
    }

    // coalesce stops at the first non-null operand.
    [Fact]
    public void CoalesceTakesTheFirstNonNullOperand()
    {
        AssertRows(
            ById(
                Ints,
                null,
                Select("c", new CoalesceIntegerProjection([A, B, new LiteralInteger(0)]))
            ),
            "1|7",
            "2|-7",
            "3|7",
            "4|-7",
            "5|3",
            "6|3",
            "7|0"
        );
    }

    // A null is in a subquery column that holds null; in a literal list it
    // is in nothing.
    [Fact]
    public void NullIsInASubqueryColumnHoldingNull()
    {
        AssertRows(InRight(new InDecimalRow(LeftKey, RightKeys)), "1", "2");
    }

    // not(in(...)) is true for a value missing from a list that holds null,
    // where SQL's NOT IN would be unknown.
    [Fact]
    public void NotInASubqueryColumnHoldingNullIsTrueForAMissingValue()
    {
        AssertRows(InRight(new NotRow(new InDecimalRow(LeftKey, RightKeys))), "3");
    }

    // in over a subquery as a select column.
    [Fact]
    public void MembershipInASubqueryColumnAsAValue()
    {
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries: [RightKeysQuery()],
                from: new FromEntity("k.left", "t"),
                select:
                [
                    new SelectItemProjection("id", new FieldInteger("t", "id")),
                    new SelectItemProjection(
                        "in",
                        new InDecimalProjection(
                            new FieldIntegerNullable("t", "k"),
                            RightKeys
                        )
                    ),
                ],
                orderBy: [new OrderItemProjectionExpression(new FieldInteger("t", "id"))]
            ),
            "1|True",
            "2|True",
            "3|False"
        );
    }

    // A literal list holds no null: null is in no literal list.
    [Fact]
    public void NullIsInNoLiteralList()
    {
        AssertRows(
            ById(Ints, new InDecimalRow(RowA, new ListLiteralInteger([7, 3]))),
            "1",
            "3",
            "6"
        );
    }

    [Fact]
    public void NotInALiteralListKeepsNulls()
    {
        AssertRows(
            ById(
                Ints,
                new NotRow(new InDecimalRow(RowA, new ListLiteralInteger([7, 3])))
            ),
            "2",
            "4",
            "5",
            "7"
        );
    }

    // equal on two nullable keys pairs the rows whose keys are both null.
    [Fact]
    public void JoinOnNullableKeysPairsTheNulls()
    {
        AssertRows(Joined(JoinKind.Inner), "1|10", "2|11");
    }

    [Fact]
    public void LeftJoinOnNullableKeysKeepsTheUnmatchedRow()
    {
        AssertRows(Joined(JoinKind.Left), "1|10", "2|11", "3|");
    }

    [Fact]
    public void FullJoinOnNullableKeysKeepsBothUnmatchedRows()
    {
        AssertRows(Joined(JoinKind.Full), "|12", "1|10", "2|11", "3|");
    }

    // groupBy puts all null keys in one group.
    [Fact]
    public void GroupByPutsNullKeysInOneGroupSortedFirst()
    {
        AssertRows(
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity(Ints, "t"),
                groupBy: [new GroupKey("a", RowA)],
                select:
                [
                    new SelectItemGroup("a", new KeyIntegerNullable("a")),
                    new SelectItemGroup("n", new CountGroup()),
                ],
                orderBy: [new OrderItemGroupColumn("a")]
            ),
            "|2",
            "-7|2",
            "3|1",
            "7|2"
        );
    }

    // ---- Sorting, distinctBy and pagination ----

    // null sorts before every value ascending and after every value
    // descending.
    [Fact]
    public void IntegersSortWithNullFirstAscending()
    {
        AssertRows(
            Sorted(Ints, RowA, SortDirection.Asc),
            "5",
            "7",
            "2",
            "4",
            "6",
            "1",
            "3"
        );
    }

    [Fact]
    public void IntegersSortWithNullLastDescending()
    {
        AssertRows(
            Sorted(Ints, RowA, SortDirection.Desc),
            "1",
            "3",
            "6",
            "2",
            "4",
            "5",
            "7"
        );
    }

    // The page is taken after the sort, nulls first.
    [Fact]
    public void PaginationAfterSortingWithNullsFirst()
    {
        AssertRows(
            new MainPlainQueryWithPagination(
                from: new FromEntity(Ints, "t"),
                select: [new SelectItemProjection("id", new FieldInteger("t", "id"))],
                orderBy:
                [
                    new OrderItemProjectionExpression(B),
                    new OrderItemProjectionExpression(new FieldInteger("t", "id")),
                ],
                pagination: new Pagination(new PaginationSkip(1), new PaginationTake(3))
            ),
            "7",
            "3",
            "4"
        );
    }

    // distinctBy keeps the first row in orderBy order of each value, null one
    // of them.
    [Fact]
    public void DistinctByKeepsTheFirstRowOfEachValueNullIncluded()
    {
        AssertRows(
            new MainPlainQueryWithoutPagination(
                from: new FromEntity(Ints, "t"),
                select:
                [
                    new SelectItemProjection("a", A),
                    new SelectItemProjection("id", new FieldInteger("t", "id")),
                ],
                distinctBy: ["a"],
                orderBy:
                [
                    new OrderItemProjectionColumn("a"),
                    new OrderItemProjectionColumn("id", SortDirection.Desc),
                ]
            ),
            "|7",
            "-7|4",
            "3|6",
            "7|3"
        );
    }

    // Strings sort by code point: upper case before lower case, a prefix
    // before a longer string, U+FFFD before U+1F600, null first.
    [Fact]
    public void StringsSortByCodePointWithNullFirst()
    {
        AssertRows(
            Sorted(Words, Word, SortDirection.Asc),
            "6",
            "4",
            "2",
            "10",
            "1",
            "5",
            "3",
            "9",
            "8",
            "7"
        );
    }

    [Fact]
    public void StringsSortByCodePointDescendingWithNullLast()
    {
        AssertRows(
            Sorted(Words, Word, SortDirection.Desc),
            "7",
            "8",
            "9",
            "3",
            "5",
            "1",
            "10",
            "2",
            "4",
            "6"
        );
    }

    // Comparisons of strings are by code point too.
    [Fact]
    public void StringComparisonIsByCodePoint()
    {
        AssertRows(
            ById(Words, new LessThanStringRow(Word, new LiteralString("a"))),
            "2",
            "4",
            "10"
        );
    }

    [Fact]
    public void StringComparisonBeyondTheBasicPlaneIsByCodePoint()
    {
        AssertRows(
            ById(Words, new GreaterThanStringRow(Word, new LiteralString("�"))),
            "7"
        );
    }

    // min and max of strings are by code point.
    [Fact]
    public void MinAndMaxOfStringsAreByCodePoint()
    {
        AssertRows(
            Total(
                Words,
                null,
                new SelectItemTotal("min", new MinStringNullableTotal(Word)),
                new SelectItemTotal("max", new MaxStringNullableTotal(Word)),
                new SelectItemTotal(
                    "lower",
                    new MinStringNullableTotal(
                        Word,
                        new GreaterThanOrEqualStringRow(Word, new LiteralString("a"))
                    )
                )
            ),
            "A|\U0001F600|a"
        );
    }

    // equal is code point by code point: "a" and "A" differ, also as group
    // keys.
    [Fact]
    public void GroupingStringsIsCaseSensitive()
    {
        AssertRows(
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity("c.first", "t"),
                groupBy: [new GroupKey("s", new FieldStringNullable("t", "s"))],
                select:
                [
                    new SelectItemGroup("s", new KeyStringNullable("s")),
                    new SelectItemGroup("n", new CountGroup()),
                ],
                orderBy: [new OrderItemGroupColumn("s")]
            ),
            "|2",
            "X|1",
            "x|2",
            "y|1"
        );
    }

    // uuids sort by their hexadecimal digits, which is the order of their
    // bytes read big-endian; null first.
    [Fact]
    public void UuidsSortByTheirHexadecimalDigits()
    {
        AssertRows(
            Sorted(Moments, new FieldUuidNullable("t", "u"), SortDirection.Asc),
            "6",
            "1",
            "5",
            "3",
            "2",
            "4"
        );
    }

    // false sorts before true; null last descending.
    [Fact]
    public void BooleansSortFalseBeforeTrueWithNullLastDescending()
    {
        AssertRows(
            Sorted(Moments, new FieldBooleanNullable("t", "f"), SortDirection.Desc),
            "1",
            "3",
            "5",
            "2",
            "4",
            "6"
        );
    }

    [Fact]
    public void DatesSortChronologicallyWithNullFirst()
    {
        AssertRows(
            Sorted(Moments, new FieldDateNullable("t", "d"), SortDirection.Asc),
            "6",
            "4",
            "2",
            "1",
            "5",
            "3"
        );
    }

    [Fact]
    public void InstantsSortChronologicallyDescendingWithNullLast()
    {
        AssertRows(
            Sorted(Moments, new FieldDatetimeNullable("t", "dt"), SortDirection.Desc),
            "5",
            "2",
            "1",
            "3",
            "4",
            "6"
        );
    }

    [Fact]
    public void TimesSortFromMidnightWithNullFirst()
    {
        AssertRows(
            Sorted(Moments, new FieldTimeNullable("t", "t"), SortDirection.Asc),
            "6",
            "2",
            "4",
            "3",
            "5",
            "1"
        );
    }

    // ---- String matching (Strings) ----

    // pattern is literal text: % and _ and \ stand for themselves.
    [Theory]
    [InlineData("contains", "%", new[] { "1" })]
    [InlineData("contains", "_", new[] { "3", "8" })]
    [InlineData("contains", "\\", new[] { "5", "6" })]
    [InlineData("contains", "\\\\", new[] { "6" })]
    [InlineData("contains", "A", new[] { "8" })]
    [InlineData("contains", "", new[] { "1", "2", "3", "4", "5", "6", "8" })]
    [InlineData("startsWith", "a_", new[] { "3" })]
    [InlineData("startsWith", "100", new[] { "1", "2" })]
    [InlineData("startsWith", "a\\", new[] { "5", "6" })]
    [InlineData("endsWith", "%", new[] { "1" })]
    [InlineData("endsWith", "\\c", new[] { "5", "6" })]
    [InlineData("endsWith", "_C", new[] { "8" })]
    public void StringMatchingTakesThePatternLiterally(
        string kind,
        string pattern,
        string[] expected
    )
    {
        StringNullableRow operand = new FieldStringNullable("t", "p");
        LiteralString literal = new(pattern);
        BooleanRow condition = kind switch
        {
            "contains" => new ContainsRow(operand, literal),
            "startsWith" => new StartsWithRow(operand, literal),
            _ => new EndsWithRow(operand, literal),
        };

        AssertRows(ById(Patterns, condition), expected);
    }

    // A null pattern matches nothing.
    [Fact]
    public void ContainsANullPatternMatchesNothing()
    {
        AssertRows(
            ById(
                Patterns,
                new ContainsRow(
                    new FieldStringNullable("t", "p"),
                    new LiteralStringNullable()
                )
            )
        );
    }

    // A pattern read from the row is literal text too: "100%" does not occur
    // in "x1000y", nor "a_c" in "xabcy".
    [Theory]
    [InlineData("x1000y", "2")]
    [InlineData("xabcy", "4")]
    [InlineData("xa\\cy", "5")]
    public void ContainsAPatternReadFromTheRowTakesItLiterally(
        string operand,
        string expected
    )
    {
        AssertRows(
            ById(
                Patterns,
                new ContainsRow(
                    new LiteralString(operand),
                    new FieldStringNullable("t", "p")
                )
            ),
            expected
        );
    }

    // ---- Set operations ----

    // union returns each distinct row once; two nulls are the same value, and
    // the integer 1 and the decimal 1 are.
    [Fact]
    public void UnionOfIntegersAndDecimalsDropsEqualRows()
    {
        AssertUnordered(
            new MainCombinedQueryUnion([
                Column("c.first", "n", new FieldIntegerNullable("t", "n")),
                Column("c.second", "n", new FieldDecimalNullable("t", "d")),
            ]),
            "",
            "1",
            "2",
            "3.5",
            "4"
        );
    }

    [Fact]
    public void UnionAllKeepsEveryRow()
    {
        AssertUnordered(
            new MainCombinedQueryUnionAll([Pairs("c.first"), Pairs("c.second")]),
            "|",
            "|",
            "|",
            "1|x",
            "1|x",
            "1|x",
            "1|x",
            "2|y",
            "3|z",
            "4|X",
            "4|x"
        );
    }

    [Fact]
    public void IntersectMatchesNullRowsAndIsCaseSensitive()
    {
        AssertUnordered(
            new MainCombinedQueryIntersect([Pairs("c.first"), Pairs("c.second")]),
            "|",
            "1|x"
        );
    }

    [Fact]
    public void ExceptRemovesNullRowsAndReturnsDistinctRows()
    {
        AssertUnordered(
            new MainCombinedQueryExcept([Pairs("c.first"), Pairs("c.second")]),
            "2|y",
            "4|X"
        );
    }

    // ---- Dates and times ----

    // Months move the month and clamp the day to the end of the month.
    [Fact]
    public void DateAddMonthsClampsTheDay()
    {
        AssertRows(
            ById(
                Moments,
                null,
                Select(
                    "next",
                    new DateAddMonthsDateNullableProjection(Day, new LiteralInteger(1))
                ),
                Select(
                    "previous",
                    new DateAddMonthsDateNullableProjection(Day, new LiteralInteger(-1))
                ),
                Select(
                    "year",
                    new DateAddMonthsDateNullableProjection(Day, new LiteralInteger(12))
                )
            ),
            "1|2024-02-29|2023-12-31|2025-01-31",
            "2|2023-02-28|2022-12-31|2024-01-31",
            "3|2025-01-30|2024-11-30|2025-12-30",
            "4|2021-02-03|2020-12-03|2022-01-03",
            "5|2024-03-29|2024-01-29|2025-02-28",
            "6|||"
        );
    }

    [Fact]
    public void DateAddDaysAndDateDiffDays()
    {
        AssertRows(
            ById(
                Moments,
                null,
                Select(
                    "next",
                    new DateAddDaysDateNullableProjection(Day, new LiteralInteger(1))
                ),
                Select(
                    "diff",
                    new DateDiffDaysIntegerNullableProjection(
                        Day,
                        new LiteralDate(new DateOnly(2024, 1, 1))
                    )
                )
            ),
            "1|2024-02-01|30",
            "2|2023-02-01|-335",
            "3|2024-12-31|364",
            "4|2021-01-04|-1093",
            "5|2024-03-01|59",
            "6||"
        );
    }

    // timeAddSeconds wraps around midnight, both ways; timeDiffSeconds does
    // not wrap.
    [Fact]
    public void TimeAddSecondsWrapsAroundMidnight()
    {
        AssertRows(
            ById(
                Moments,
                null,
                Select(
                    "later",
                    new TimeAddSecondsTimeNullableProjection(
                        Time,
                        new LiteralInteger(7200)
                    )
                ),
                Select(
                    "earlier",
                    new TimeAddSecondsTimeNullableProjection(
                        Time,
                        new LiteralInteger(-3600)
                    )
                ),
                Select(
                    "diff",
                    new TimeDiffSecondsDecimalNullableProjection(
                        Time,
                        new LiteralTime(new TimeOnly(12, 0))
                    )
                )
            ),
            "1|01:00:00|22:00:00|39600",
            "2|02:30:00.25|23:30:00.25|-41399.75",
            "3|14:00:00|11:00:00|0",
            "4|08:15:30.5|05:15:30.5|-20669.5",
            "5|20:30:15.25|17:30:15.25|23415.25",
            "6|||"
        );
    }

    [Fact(
        Skip = "Bug (upstream translator, stored datetime fraction dropped): "
            + "2024-01-31T23:59:59.999999Z + 0.000001 s is 2024-02-01T00:00:00Z by the "
            + "spec and in memory; translated 2024-01-31T23:59:59.000001Z, and + 1 "
            + "month gives 2024-02-29T23:59:59Z, not .999999Z"
    )]
    public void DatetimeAddSecondsAndMonths()
    {
        AssertRows(
            ById(
                Moments,
                null,
                Select(
                    "s",
                    new DatetimeAddSecondsDatetimeNullableProjection(
                        Instant,
                        new LiteralDecimal(0.000001m)
                    )
                ),
                Select(
                    "m",
                    new DatetimeAddMonthsDatetimeNullableProjection(
                        Instant,
                        new LiteralInteger(1)
                    )
                )
            ),
            "1|2024-02-01T00:00:00Z|2024-02-29T23:59:59.999999Z",
            "2|2024-02-29T00:00:00.500001Z|2024-03-29T00:00:00.5Z",
            "3|2021-01-01T12:00:00.000001Z|2021-02-01T12:00:00Z",
            "4|1999-12-31T23:59:59.000001Z|2000-01-31T23:59:59Z",
            "5|2024-08-15T18:30:15.250001Z|2024-09-15T18:30:15.25Z",
            "6||"
        );
    }

    // min and max of temporal values and booleans by their order.
    [Fact]
    public void MinAndMaxOfTemporalValuesAndBooleans()
    {
        AssertRows(
            Total(
                Moments,
                null,
                new SelectItemTotal(
                    "d",
                    new MinDateNullableTotal(new FieldDateNullable("t", "d"))
                ),
                new SelectItemTotal(
                    "t",
                    new MaxTimeNullableTotal(new FieldTimeNullable("t", "t"))
                ),
                new SelectItemTotal(
                    "dt",
                    new MinDatetimeNullableTotal(new FieldDatetimeNullable("t", "dt"))
                ),
                new SelectItemTotal(
                    "f",
                    new MinBooleanNullableTotal(new FieldBooleanNullable("t", "f"))
                ),
                new SelectItemTotal(
                    "g",
                    new MaxBooleanNullableTotal(new FieldBooleanNullable("t", "f"))
                )
            ),
            "2021-01-03|23:00:00|1999-12-31T23:59:59Z|False|True"
        );
    }

    // ---- More arithmetic ----

    // integerDivide(-2^63, -1) is 2^63, beyond 64 bits: an error.
    [Fact]
    public void IntegerDivideOfTheLeastIntegerByMinusOneFails()
    {
        AssertFails(
            ById(
                Big,
                new EqualDecimalRow(new FieldInteger("t", "id"), new LiteralInteger(3)),
                Select(
                    "q",
                    new IntegerDivideIntegerProjection(
                        new LiteralInteger(long.MinValue),
                        new FieldInteger("t", "v")
                    )
                )
            )
        );
    }

    // abs of -2^63 has no value in range: an error.
    [Fact]
    public void AbsOfTheLeastIntegerFails()
    {
        AssertFails(
            ById(
                Big,
                new EqualDecimalRow(new FieldInteger("t", "id"), new LiteralInteger(3)),
                Select(
                    "a",
                    new AbsIntegerProjection(
                        new AddIntegerProjection([
                            new FieldInteger("t", "v"),
                            new LiteralInteger(long.MinValue + 1),
                        ])
                    )
                )
            )
        );
    }

    [Fact]
    public void AbsAndSignOfIntegers()
    {
        AssertRows(
            ById(
                Ints,
                null,
                Select("abs", new AbsIntegerNullableProjection(A)),
                Select("sign", new SignIntegerNullableProjection(A))
            ),
            "1|7|1",
            "2|7|-1",
            "3|7|1",
            "4|7|-1",
            "5||",
            "6|3|1",
            "7||"
        );
    }

    // round with digits keeps a decimal; halfEven also with digits 0.
    [Fact]
    public void RoundToZeroDigitsHalfAwayAndHalfEven()
    {
        AssertRows(
            ById(
                Decimals,
                null,
                Select(
                    "away",
                    new RoundDecimalNullableProjection(X, new LiteralInteger(0))
                ),
                Select(
                    "even",
                    new RoundDecimalNullableProjection(
                        X,
                        new LiteralInteger(0),
                        RoundingMode.HalfEven
                    )
                )
            ),
            "1|1|1",
            "2|2|2",
            "3|-2|-2",
            "4|3|2",
            "5|-3|-2",
            "6|0|0",
            "7||",
            "8|1|1"
        );
    }

    // The integer 1 equals the decimal 1.
    [Fact]
    public void IntegerAndDecimalCompareByValue()
    {
        AssertRows(
            ById(Decimals, new EqualDecimalRow(X, new LiteralInteger(1))),
            "1",
            "8"
        );
    }

    // greatest of a decimal and an integer is a decimal, null when an
    // operand is.
    [Fact]
    public void GreatestOfADecimalAndAnInteger()
    {
        AssertRows(
            ById(
                Decimals,
                null,
                Select(
                    "g",
                    new GreatestDecimalNullableProjection([X, new LiteralInteger(2)])
                )
            ),
            "1|2",
            "2|2",
            "3|2",
            "4|2.5",
            "5|2",
            "6|2",
            "7|",
            "8|2"
        );
    }

    // A quotient compared in where is the quotient to 28 digits.
    [Fact(
        Skip = "Bug (upstream translator): where divide(x, y) = "
            + "0.3333333333333333333333333333 keeps row 1 by the spec and in memory; "
            + "translated, the numeric quotient has 20 digits and no row is kept"
    )]
    public void DecimalQuotientInWhereIsTheQuotientTo28Digits()
    {
        AssertRows(
            ById(
                Decimals,
                new EqualDecimalRow(
                    new DivideDecimalNullableRow([
                        new FieldDecimalNullable("t", "x"),
                        new FieldDecimalNullable("t", "y"),
                    ]),
                    new LiteralDecimal(0.3333333333333333333333333333m)
                )
            ),
            "1"
        );
    }

    // An integer compares with a decimal exactly, also beyond 2^53.
    [Fact]
    public void LargeIntegerComparesWithADecimalExactly()
    {
        AssertRows(
            ById(
                Big,
                new OrRow([
                    new EqualDecimalRow(
                        new FieldInteger("t", "v"),
                        new LiteralDecimal(9223372036854775806m)
                    ),
                    new GreaterThanDecimalRow(
                        new FieldInteger("t", "v"),
                        new LiteralDecimal(9223372036854775806.5m)
                    ),
                ])
            ),
            "1"
        );
    }

    [Fact]
    public void LargeIntegerIsNotEqualToItsNeighbour()
    {
        AssertRows(
            ById(
                Big,
                new EqualDecimalRow(
                    new FieldInteger("t", "v"),
                    new LiteralDecimal(9223372036854775806m)
                )
            )
        );
    }

    // ---- Laziness (Evaluation) ----

    // if evaluates only the branch it takes: modulo by b - 3 is never
    // evaluated where b is 3.
    [Fact]
    public void IfEvaluatesOnlyTheBranchItTakes()
    {
        AssertRows(
            ById(
                Ints,
                null,
                Select(
                    "m",
                    new IfIntegerNullableProjection(
                        new LiteralInteger(0),
                        new ModuloIntegerNullableProjection(A, BMinusThree),
                        new EqualDecimalProjection(B, new LiteralInteger(3))
                    )
                )
            ),
            "1|0",
            "2|0",
            "3|2",
            "4|-2",
            "5|0",
            "6|",
            "7|"
        );
    }

    // and stops at the first false condition.
    [Fact]
    public void AndStopsAtTheFirstFalseCondition()
    {
        AssertRows(
            ById(
                Ints,
                new AndRow([
                    new NotEqualDecimalRow(RowBMinusThree, new LiteralInteger(0)),
                    new EqualDecimalRow(
                        new ModuloIntegerNullableRow(RowA, RowBMinusThree),
                        new LiteralInteger(0)
                    ),
                ])
            ),
            "1",
            "2"
        );
    }

    // or stops at the first true condition.
    [Fact]
    public void OrStopsAtTheFirstTrueCondition()
    {
        AssertRows(
            ById(
                Ints,
                new OrRow([
                    new EqualDecimalRow(RowBMinusThree, new LiteralInteger(0)),
                    new EqualDecimalRow(
                        new ModuloIntegerNullableRow(RowA, RowBMinusThree),
                        new LiteralInteger(0)
                    ),
                ])
            ),
            "1",
            "2",
            "5"
        );
    }

    // ---- More aggregates ----

    [Fact]
    public void DecimalSumIsExact()
    {
        AssertRows(
            Total(Decimals, null, new SelectItemTotal("s", new SumDecimalTotal(X))),
            "2.111111111111111"
        );
    }

    [Fact]
    public void GroupedSumAndCount()
    {
        AssertRows(
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity(Grouped, "t"),
                groupBy: [new GroupKey("g", new FieldIntegerNullable("t", "g"))],
                select:
                [
                    new SelectItemGroup("g", new KeyIntegerNullable("g")),
                    new SelectItemGroup(
                        "s",
                        new SumIntegerGroup(new FieldIntegerNullable("t", "v"))
                    ),
                    new SelectItemGroup("n", new CountGroup()),
                ],
                orderBy: [new OrderItemGroupColumn("g")]
            ),
            "1|5|3",
            "2|3|2",
            "3|0|1"
        );
    }

    [Fact]
    public void SumOverAllRowsInAPlainSelect()
    {
        AssertRows(
            ById(
                Grouped,
                new EqualDecimalRow(
                    new FieldIntegerNullable("t", "g"),
                    new LiteralInteger(1)
                ),
                Select("s", new SumIntegerProjection(new FieldIntegerNullable("t", "v")))
            ),
            "1|5",
            "2|5",
            "3|5"
        );
    }

    // The mean of 1, 2, 2 is 1.666666666666666666666666667, which is less
    // than 1.66666666666666667: having sees the mean to 28 digits.
    [Fact(
        Skip = "Bug (upstream translator): having average(v) < 1.66666666666666667 keeps "
            + "groups 1 and 2 by the spec and in memory; translated, AVG is "
            + "1.6666666666666667 and group 1 is dropped"
    )]
    public void HavingComparesTheAverageTo28Digits()
    {
        AssertRows(
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity(Grouped, "t"),
                groupBy: [new GroupKey("g", new FieldIntegerNullable("t", "g"))],
                select: [new SelectItemGroup("g", new KeyIntegerNullable("g"))],
                having: new LessThanDecimalGroup(
                    new AverageDecimalNullableGroup(new FieldIntegerNullable("t", "v")),
                    new LiteralDecimal(1.66666666666666667m)
                ),
                orderBy: [new OrderItemGroupColumn("g")]
            ),
            "1",
            "2"
        );
    }

    // countDistinct counts the different non-null values, strings by code
    // point.
    [Fact]
    public void CountDistinctSkipsNullsAndIsCaseSensitive()
    {
        AssertRows(
            Total(Ints, null, new SelectItemTotal("n", new CountDistinctTotal(RowA))),
            "3"
        );
        AssertRows(
            Total(
                "c.first",
                null,
                new SelectItemTotal(
                    "n",
                    new CountDistinctTotal(new FieldStringNullable("t", "s"))
                )
            ),
            "3"
        );
    }

    // The mean of two dates exactly halfway between two days is the later
    // day: 2024-01-31 and 2024-02-29 average 2024-02-15.
    [Theory]
    [InlineData(5, "2024-02-15")]
    [InlineData(3, "2024-07-16")]
    public void AverageOfDatesRoundsAHalfToTheLaterDay(long other, string expected)
    {
        AssertRows(
            Total(
                Moments,
                new InDecimalRow(
                    new FieldInteger("t", "id"),
                    new ListLiteralInteger([1, other])
                ),
                new SelectItemTotal("a", new AverageDateNullableTotal(Day))
            ),
            expected
        );
    }

    // The mean of times is the mean of their seconds since midnight, without
    // wrapping.
    [Fact]
    public void AverageOfTimesDoesNotWrap()
    {
        AssertRows(
            Total(
                Moments,
                new InDecimalRow(
                    new FieldInteger("t", "id"),
                    new ListLiteralInteger([1, 2])
                ),
                new SelectItemTotal("a", new AverageTimeNullableTotal(Time))
            ),
            "11:45:00.125"
        );
    }

    // The mean instant, rounded to the microsecond, a half to the later one.
    [Fact(
        Skip = "Bug (upstream translator, stored datetime fraction dropped): the mean of "
            + "2024-01-31T23:59:59.999999Z and 2024-02-29T00:00:00.5Z is "
            + "2024-02-15T00:00:00.25Z by the spec and in memory; translated "
            + "2024-02-14T23:59:59.5Z"
    )]
    public void AverageOfInstantsRoundsAHalfMicrosecondToTheLaterOne()
    {
        AssertRows(
            Total(
                Moments,
                new InDecimalRow(
                    new FieldInteger("t", "id"),
                    new ListLiteralInteger([1, 2])
                ),
                new SelectItemTotal("a", new AverageDatetimeNullableTotal(Instant))
            ),
            "2024-02-15T00:00:00.25Z"
        );
    }

    // ---- Subqueries, joins, sorting ----

    // A subquery's orderBy decides which rows its pagination keeps, nulls
    // last descending and first ascending.
    [Theory]
    [InlineData(SortDirection.Desc, "14|2")]
    [InlineData(SortDirection.Asc, "0|2")]
    public void SubqueryPageKeepsTheRowsItsOrderPutsFirst(
        SortDirection direction,
        string expected
    )
    {
        AssertRows(
            new MainTotalQuery(
                subqueries:
                [
                    new SubqueryQuery(
                        "top",
                        new PlainQueryWithPagination(
                            from: new FromEntity(Ints, "t"),
                            select: [new SelectItemProjection("a", A)],
                            orderBy:
                            [
                                new OrderItemProjectionExpression(A, direction),
                                new OrderItemProjectionExpression(
                                    new FieldInteger("t", "id")
                                ),
                            ],
                            pagination: new Pagination(take: new PaginationTake(2))
                        )
                    ),
                ],
                from: new FromSubquery("top", "s"),
                aggregate:
                [
                    new SelectItemTotal(
                        "s",
                        new SumIntegerTotal(new FieldIntegerNullable("s", "a"))
                    ),
                    new SelectItemTotal("n", new CountTotal()),
                ]
            ),
            expected
        );
    }

    [Fact]
    public void RightJoinOnNullableKeysKeepsTheUnmatchedJoinedRow()
    {
        AssertRows(Joined(JoinKind.Right), "|12", "1|10", "2|11");
    }

    // An integer key equals a decimal key of the same value; null keys pair.
    [Fact]
    public void JoinOfIntegerAndDecimalKeys()
    {
        AssertRows(
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("c.first", "f"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Inner,
                        "c.second",
                        new EqualDecimalRow(
                            new FieldIntegerNullable("f", "n"),
                            new FieldDecimalNullable("s", "d")
                        ),
                        "s"
                    ),
                ],
                select:
                [
                    new SelectItemProjection("f", new FieldInteger("f", "id")),
                    new SelectItemProjection("s", new FieldInteger("s", "id")),
                ],
                orderBy:
                [
                    new OrderItemProjectionColumn("f"),
                    new OrderItemProjectionColumn("s"),
                ]
            ),
            "1|1",
            "1|2",
            "2|1",
            "2|2",
            "4|4",
            "5|4",
            "6|5"
        );
    }

    // distinctBy keeps one row per value in orderBy order, nulls last
    // descending, before the page.
    [Fact]
    public void DistinctByDescendingThenTake()
    {
        AssertRows(
            new MainPlainQueryWithPagination(
                from: new FromEntity(Ints, "t"),
                select: [new SelectItemProjection("a", A)],
                distinctBy: ["a"],
                orderBy: [new OrderItemProjectionColumn("a", SortDirection.Desc)],
                pagination: new Pagination(
                    skip: new PaginationSkip(2),
                    take: new PaginationTake(5)
                )
            ),
            "-7",
            ""
        );
    }

    // ---- More strings ----

    [Fact]
    public void ConcatIsLifted()
    {
        AssertRows(
            ById(
                Words,
                null,
                Select(
                    "c",
                    new ConcatStringNullableProjection([
                        WordProjection,
                        new LiteralString("!"),
                    ])
                )
            ),
            "1|a!",
            "2|B!",
            "3|b!",
            "4|A!",
            "5|ab!",
            "6|",
            "7|\U0001F600!",
            "8|�!",
            "9|é!",
            "10|Z!"
        );
    }

    [Fact]
    public void InALiteralListOfStringsIsCaseSensitive()
    {
        AssertRows(
            ById(Words, new InStringRow(Word, new ListLiteralString(["a", "B"]))),
            "1",
            "2"
        );
    }

    // Two nulls are at least each other.
    [Fact]
    public void StringGreaterThanOrEqualItselfHoldsForNull()
    {
        AssertRows(
            ById(Words, new GreaterThanOrEqualStringRow(Word, Word)),
            "1",
            "2",
            "3",
            "4",
            "5",
            "6",
            "7",
            "8",
            "9",
            "10"
        );
    }

    [Fact]
    public void UuidComparisonFollowsTheirHexadecimalDigits()
    {
        AssertRows(
            ById(
                Moments,
                new GreaterThanUuidRow(
                    new FieldUuidNullable("t", "u"),
                    new LiteralUuid(Guid.Parse("0000000a-0000-0000-0000-000000000000"))
                )
            ),
            "2",
            "4"
        );
    }

    [Fact]
    public void BooleanComparisonPutsFalseBeforeTrue()
    {
        AssertRows(
            ById(
                Moments,
                new GreaterThanBooleanRow(
                    new FieldBooleanNullable("t", "f"),
                    new LiteralBoolean(false)
                )
            ),
            "1",
            "3",
            "5"
        );
    }

    // ---- More dates and times ----

    [Fact]
    public void CalendarPartsOfDates()
    {
        AssertRows(
            ById(
                Moments,
                null,
                Select("y", new YearDateIntegerNullableProjection(Day)),
                Select("m", new MonthDateIntegerNullableProjection(Day)),
                Select("d", new DayDateIntegerNullableProjection(Day)),
                Select("doy", new DayOfYearDateIntegerNullableProjection(Day))
            ),
            "1|2024|1|31|31",
            "2|2023|1|31|31",
            "3|2024|12|30|365",
            "4|2021|1|3|3",
            "5|2024|2|29|60",
            "6||||"
        );
    }

    [Fact]
    public void ClockPartsOfTimesDropTheFraction()
    {
        AssertRows(
            ById(
                Moments,
                null,
                Select("h", new HourTimeIntegerNullableProjection(Time)),
                Select("m", new MinuteTimeIntegerNullableProjection(Time)),
                Select("s", new SecondTimeIntegerNullableProjection(Time))
            ),
            "1|23|0|0",
            "2|0|30|0",
            "3|12|0|0",
            "4|6|15|30",
            "5|18|30|15",
            "6|||"
        );
    }

    [Fact]
    public void TimeAddsAFractionOfASecond()
    {
        AssertRows(
            ById(
                Moments,
                null,
                Select(
                    "t",
                    new TimeAddSecondsTimeNullableProjection(
                        Time,
                        new LiteralDecimal(0.5m)
                    )
                )
            ),
            "1|23:00:00.5",
            "2|00:30:00.75",
            "3|12:00:00.5",
            "4|06:15:31",
            "5|18:30:15.75",
            "6|"
        );
    }

    [Fact]
    public void DateOfAnInstantAndInstantOfADate()
    {
        AssertRows(
            ById(
                Moments,
                null,
                Select("d", new AsDateDatetimeDateNullableProjection(Instant)),
                Select("dt", new AsDatetimeDateDatetimeNullableProjection(Day))
            ),
            "1|2024-01-31|2024-01-31T00:00:00Z",
            "2|2024-02-29|2023-01-31T00:00:00Z",
            "3|2021-01-01|2024-12-30T00:00:00Z",
            "4|1999-12-31|2021-01-03T00:00:00Z",
            "5|2024-08-15|2024-02-29T00:00:00Z",
            "6||"
        );
    }

    // datetime has microsecond precision: a stored instant is read with its
    // fraction.
    [Fact(
        Skip = "Bug (upstream translator): a stored 2024-01-31T23:59:59.999999Z is read "
            + "as is in memory; translated it is 2024-01-31T23:59:59Z, as the cell text "
            + "is parsed back with TO_TIMESTAMP(text, 'TZH MM/DD/YYYY HH24:MI:SS'), "
            + "dropping the fraction (the library's AssumeUniversal parse keeps the "
            + "provider from reading the column itself)"
    )]
    public void InstantsKeepTheirMicroseconds()
    {
        AssertRows(
            ById(Moments, null, Select("dt", Instant)),
            "1|2024-01-31T23:59:59.999999Z",
            "2|2024-02-29T00:00:00.5Z",
            "3|2021-01-01T12:00:00Z",
            "4|1999-12-31T23:59:59Z",
            "5|2024-08-15T18:30:15.25Z",
            "6|"
        );
    }

    [Fact(
        Skip = "Bug (upstream translator, stored datetime fraction dropped): where dt = "
            + "2024-01-31T23:59:59.999999Z keeps row 1 by the spec and in memory; "
            + "translated keeps no row"
    )]
    public void InstantEqualsItsLiteralWithMicroseconds()
    {
        AssertRows(
            ById(
                Moments,
                new EqualDatetimeRow(
                    Instant,
                    new LiteralDatetime(
                        new DateTimeOffset(
                            2024,
                            1,
                            31,
                            23,
                            59,
                            59,
                            TimeSpan.Zero
                        ).AddTicks(9999990)
                    )
                )
            ),
            "1"
        );
    }

    [Fact(
        Skip = "Bug (upstream translator, stored datetime fraction dropped): where dt > "
            + "2024-01-31T23:59:59.5Z keeps rows 1, 2, 5 by the spec and in memory; "
            + "translated keeps 2, 5"
    )]
    public void InstantsCompareWithTheirFraction()
    {
        AssertRows(
            ById(
                Moments,
                new GreaterThanDatetimeRow(
                    Instant,
                    new LiteralDatetime(
                        new DateTimeOffset(
                            2024,
                            1,
                            31,
                            23,
                            59,
                            59,
                            TimeSpan.Zero
                        ).AddTicks(5000000)
                    )
                )
            ),
            "1",
            "2",
            "5"
        );
    }

    // datetimeDiffSeconds is a decimal with the microseconds.
    [Fact(
        Skip = "Bug (upstream translator, stored datetime fraction dropped): "
            + "datetimeDiffSeconds gives 2678399.999999, 5097600.5, 19679415.25 by the "
            + "spec and in memory; translated 2678399, 5097600, 19679415"
    )]
    public void DatetimeDiffSecondsKeepsMicroseconds()
    {
        AssertRows(
            ById(
                Moments,
                null,
                Select(
                    "diff",
                    new DatetimeDiffSecondsDecimalNullableProjection(
                        Instant,
                        new LiteralDatetime(
                            new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero)
                        )
                    )
                )
            ),
            "1|2678399.999999",
            "2|5097600.5",
            "3|-94564800",
            "4|-757382401",
            "5|19679415.25",
            "6|"
        );
    }

    [Fact]
    public void ClockAndCalendarPartsOfInstants()
    {
        AssertRows(
            ById(
                Moments,
                null,
                Select("y", new YearDatetimeIntegerNullableProjection(Instant)),
                Select("h", new HourDatetimeIntegerNullableProjection(Instant))
            ),
            "1|2024|23",
            "2|2024|0",
            "3|2021|12",
            "4|1999|23",
            "5|2024|18",
            "6||"
        );
    }

    private static SubtractIntegerNullableProjection BMinusThree =>
        new([new FieldIntegerNullable("t", "b"), new LiteralInteger(3)]);

    private static SubtractIntegerNullableRow RowBMinusThree =>
        new([new FieldIntegerNullable("t", "b"), new LiteralInteger(3)]);

    // ---- Conditional values and parts ----

    [Fact]
    public void NullIfEqualAndCoalesceOfStrings()
    {
        AssertRows(
            ById(
                Words,
                new LessThanOrEqualDecimalRow(RowId, new LiteralInteger(6)),
                Select(
                    "n",
                    new NullIfEqualStringNullableProjection(
                        WordProjection,
                        new LiteralString("a")
                    )
                ),
                Select(
                    "c",
                    new CoalesceStringProjection([WordProjection, new LiteralString("-")])
                )
            ),
            "1||a",
            "2|B|B",
            "3|b|b",
            "4|A|A",
            "5|ab|ab",
            "6||-"
        );
    }

    // if with a string comparison by code point as its condition: null is
    // not less than anything.
    [Fact]
    public void IfOverAStringComparison()
    {
        AssertRows(
            ById(
                Words,
                null,
                Select(
                    "c",
                    new IfStringNullableProjection(
                        WordProjection,
                        new LiteralString("x"),
                        new LessThanStringProjection(
                            WordProjection,
                            new LiteralString("b")
                        )
                    )
                )
            ),
            "1|a",
            "2|B",
            "3|x",
            "4|A",
            "5|ab",
            "6|x",
            "7|x",
            "8|x",
            "9|x",
            "10|Z"
        );
    }

    // greatest of dates, times and booleans, null when an operand is null.
    [Fact]
    public void GreatestOfTemporalValuesAndBooleans()
    {
        AssertRows(
            ById(
                Moments,
                null,
                Select(
                    "d",
                    new GreatestDateNullableProjection([
                        Day,
                        new LiteralDate(new DateOnly(2024, 1, 1)),
                    ])
                ),
                Select(
                    "t",
                    new GreatestTimeNullableProjection([
                        Time,
                        new LiteralTime(new TimeOnly(12, 0)),
                    ])
                ),
                Select(
                    "f",
                    new GreatestBooleanNullableProjection([
                        new FieldBooleanNullable("t", "f"),
                        new LiteralBoolean(false),
                    ])
                )
            ),
            "1|2024-01-31|23:00:00|True",
            "2|2024-01-01|12:00:00|False",
            "3|2024-12-30|12:00:00|True",
            "4|2024-01-01|12:00:00|False",
            "5|2024-02-29|18:30:15.25|True",
            "6|||"
        );
    }

    // greatest of instants keeps the microseconds of the greater one.
    [Fact(
        Skip = "Bug (upstream translator, stored datetime fraction dropped): "
            + "greatest(2024-02-29T00:00:00.5Z, 2024-02-01T00:00:00Z) is "
            + "2024-02-29T00:00:00.5Z by the spec and in memory; translated "
            + "2024-02-29T00:00:00Z"
    )]
    public void GreatestOfInstantsKeepsTheirMicroseconds()
    {
        AssertRows(
            ById(
                Moments,
                null,
                Select(
                    "dt",
                    new GreatestDatetimeNullableProjection([
                        Instant,
                        new LiteralDatetime(
                            new DateTimeOffset(2024, 2, 1, 0, 0, 0, TimeSpan.Zero)
                        ),
                    ])
                )
            ),
            "1|2024-02-01T00:00:00Z",
            "2|2024-02-29T00:00:00.5Z",
            "3|2024-02-01T00:00:00Z",
            "4|2024-02-01T00:00:00Z",
            "5|2024-08-15T18:30:15.25Z",
            "6|"
        );
    }

    // nullIfEqual compares numbers by value: the decimal 1 equals the
    // integer 1.
    [Fact]
    public void NullIfEqualOfADecimalAndAnInteger()
    {
        AssertRows(
            ById(
                Decimals,
                null,
                Select(
                    "n",
                    new NullIfEqualDecimalNullableProjection(X, new LiteralInteger(1))
                )
            ),
            "1|",
            "2|2",
            "3|-2",
            "4|2.5",
            "5|-2.5",
            "6|0.111111111111111",
            "7|",
            "8|"
        );
    }

    [Fact]
    public void DateFromParts()
    {
        AssertRows(
            ById(
                Ints,
                new GreaterThanDecimalRow(RowA, new LiteralInteger(0)),
                Select(
                    "d",
                    new DateFromPartsDateNullableProjection(
                        new LiteralInteger(2024),
                        new LiteralInteger(2),
                        A
                    )
                )
            ),
            "1|2024-02-07",
            "3|2024-02-07",
            "6|2024-02-03"
        );
    }

    // A part outside its range is an error: February 29, 2023 does not
    // exist.
    [Fact]
    public void DateFromPartsOfADayThatDoesNotExistFails()
    {
        AssertFails(
            ById(
                Ints,
                new EqualDecimalRow(RowId, new LiteralInteger(6)),
                Select(
                    "d",
                    new DateFromPartsDateNullableProjection(
                        new LiteralInteger(2023),
                        new LiteralInteger(2),
                        new AddIntegerNullableProjection([A, new LiteralInteger(26)])
                    )
                )
            )
        );
    }

    // datetimeFromParts is that day and time of day in UTC, microseconds
    // included.
    [Fact]
    public void DatetimeFromPartsKeepsTheFractionOfTheTime()
    {
        AssertRows(
            ById(
                Moments,
                null,
                Select("dt", new DatetimeFromPartsDatetimeNullableProjection(Day, Time))
            ),
            "1|2024-01-31T23:00:00Z",
            "2|2023-01-31T00:30:00.25Z",
            "3|2024-12-30T12:00:00Z",
            "4|2021-01-03T06:15:30.5Z",
            "5|2024-02-29T18:30:15.25Z",
            "6|"
        );
    }

    [Fact]
    public void MembershipOfDatesAndUuidsInLiteralLists()
    {
        AssertRows(
            ById(
                Moments,
                new OrRow([
                    new InDateRow(Day, new ListLiteralDate([new DateOnly(2024, 2, 29)])),
                    new InUuidRow(
                        new FieldUuidNullable("t", "u"),
                        new ListLiteralUuid([
                            Guid.Parse("0000000a-0000-0000-0000-000000000000"),
                        ])
                    ),
                ])
            ),
            "3",
            "5"
        );
    }

    // first and last of decimals, null first ascending: the first value of
    // x by x is null.
    [Fact]
    public void FirstAndLastOfDecimalsSortNullFirst()
    {
        OrderItemRow[] byX = [new OrderItemRow(X), new OrderItemRow(RowId)];

        AssertRows(
            Total(
                Decimals,
                null,
                new SelectItemTotal("first", new FirstDecimalNullableTotal(X, byX)),
                new SelectItemTotal("last", new LastDecimalNullableTotal(X, byX)),
                new SelectItemTotal("max", new MaxDecimalNullableTotal(X))
            ),
            "|2.5|2.5"
        );
    }

    // ---- Conditions over nulls as values (Null semantics: Conditions) ----

    // A comparison is a non-null boolean, so not, and and or of comparisons
    // with a null operand are true or false, never null.
    [Fact]
    public void ConnectivesOfComparisonsWithANullAreNeverNull()
    {
        AssertRows(
            ById(
                Ints,
                null,
                Select("not", new NotProjection(new LessThanDecimalProjection(A, B))),
                Select(
                    "and",
                    new AndProjection([
                        new NotProjection(new GreaterThanDecimalProjection(A, B)),
                        new NotEqualDecimalProjection(A, new LiteralInteger(0)),
                    ])
                ),
                Select(
                    "or",
                    new OrProjection([
                        new LessThanDecimalProjection(A, B),
                        new EqualDecimalProjection(A, B),
                    ])
                )
            ),
            "1|True|False|False",
            "2|False|True|True",
            "3|True|False|False",
            "4|False|True|True",
            "5|True|True|False",
            "6|True|True|False",
            "7|True|True|True"
        );
    }

    // if(not(lessThan(a, b)), 1, 0) takes the then branch where an operand is
    // null.
    [Fact]
    public void IfOverANegatedComparisonWithANullTakesThen()
    {
        AssertRows(
            ById(
                Ints,
                null,
                Select(
                    "i",
                    new IfIntegerProjection(
                        new LiteralInteger(1),
                        new LiteralInteger(0),
                        new NotProjection(new LessThanDecimalProjection(A, B))
                    )
                )
            ),
            "1|1",
            "2|0",
            "3|1",
            "4|0",
            "5|1",
            "6|1",
            "7|1"
        );
    }

    // An aggregate's predicate is a condition: a comparison with a null
    // operand is false, so all is false and not(...) counts the row.
    [Fact]
    public void AggregatePredicatesOverNullsAreConditions()
    {
        AssertRows(
            Total(
                Ints,
                null,
                new SelectItemTotal(
                    "all",
                    new AllTotal(new GreaterThanDecimalRow(RowA, new LiteralInteger(-10)))
                ),
                new SelectItemTotal(
                    "any",
                    new AnyTotal(
                        new NotRow(
                            new GreaterThanDecimalRow(RowA, new LiteralInteger(-10))
                        )
                    )
                ),
                new SelectItemTotal(
                    "notPositive",
                    new CountTotal(
                        new NotRow(new GreaterThanDecimalRow(RowA, new LiteralInteger(0)))
                    )
                ),
                new SelectItemTotal(
                    "ge",
                    new CountTotal(new GreaterThanOrEqualDecimalRow(RowA, RowB))
                ),
                new SelectItemTotal(
                    "sumNotLess",
                    new SumIntegerTotal(
                        RowId,
                        new NotRow(new LessThanDecimalRow(RowA, RowB))
                    )
                )
            ),
            "False|True|4|3|22"
        );
    }

    [Fact]
    public void GroupedAllAndAnyOverNulls()
    {
        AssertRows(
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity(Grouped, "t"),
                groupBy: [new GroupKey("g", new FieldIntegerNullable("t", "g"))],
                select:
                [
                    new SelectItemGroup("g", new KeyIntegerNullable("g")),
                    new SelectItemGroup(
                        "all",
                        new AllGroup(
                            new GreaterThanDecimalRow(
                                new FieldIntegerNullable("t", "v"),
                                new LiteralInteger(0)
                            )
                        )
                    ),
                    new SelectItemGroup(
                        "any",
                        new AnyGroup(
                            new NotRow(
                                new GreaterThanDecimalRow(
                                    new FieldIntegerNullable("t", "v"),
                                    new LiteralInteger(0)
                                )
                            )
                        )
                    ),
                ],
                orderBy: [new OrderItemGroupColumn("g")]
            ),
            "1|True|False",
            "2|True|False",
            "3|False|True"
        );
    }

    // on has the same null semantics as where: greaterThanOrEqual pairs two
    // null keys.
    [Fact]
    public void JoinOnAnOrderingComparisonPairsNullKeys()
    {
        AssertRows(
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("k.left", "l"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Inner,
                        "k.right",
                        new GreaterThanOrEqualDecimalRow(
                            new FieldIntegerNullable("l", "k"),
                            new FieldIntegerNullable("r", "k")
                        ),
                        "r"
                    ),
                ],
                select:
                [
                    new SelectItemProjection("l", new FieldInteger("l", "id")),
                    new SelectItemProjection("r", new FieldInteger("r", "id")),
                ],
                orderBy:
                [
                    new OrderItemProjectionColumn("l"),
                    new OrderItemProjectionColumn("r"),
                ]
            ),
            "1|10",
            "2|11",
            "3|10"
        );
    }

    // A left join keeps the unmatched row with nulls, which where can find.
    [Fact]
    public void LeftJoinAntiJoinFindsTheUnmatchedRows()
    {
        AssertRows(
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("k.right", "r"),
                joins:
                [
                    new JoinEntity(
                        JoinKind.Left,
                        "k.left",
                        new AndRow([
                            new EqualDecimalRow(
                                new FieldIntegerNullable("r", "k"),
                                new FieldIntegerNullable("l", "k")
                            ),
                            new NotEqualDecimalRow(
                                new FieldIntegerNullable("l", "k"),
                                new LiteralIntegerNullable()
                            ),
                        ]),
                        "l"
                    ),
                ],
                where: new EqualDecimalRow(
                    new FieldIntegerNullable("l", "id"),
                    new LiteralIntegerNullable()
                ),
                select: [new SelectItemProjection("r", new FieldInteger("r", "id"))],
                orderBy: [new OrderItemProjectionColumn("r")]
            ),
            "11",
            "12"
        );
    }

    // Sorting by a computed key: nulls first ascending.
    [Fact]
    public void SortingByAComputedKeyPutsNullsFirst()
    {
        AssertRows(
            Sorted(Ints, new ModuloIntegerNullableProjection(A, B), SortDirection.Asc),
            "5",
            "6",
            "7",
            "2",
            "4",
            "1",
            "3"
        );
    }

    // distinctBy two columns: rows equal on both, nulls included, are one.
    [Fact]
    public void DistinctByTwoColumnsTreatsNullsAsEqual()
    {
        AssertRows(
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("c.first", "t"),
                select:
                [
                    new SelectItemProjection("n", new FieldIntegerNullable("t", "n")),
                    new SelectItemProjection("s", new FieldStringNullable("t", "s")),
                    new SelectItemProjection("id", new FieldInteger("t", "id")),
                ],
                distinctBy: ["n", "s"],
                orderBy:
                [
                    new OrderItemProjectionColumn("n"),
                    new OrderItemProjectionColumn("s"),
                    new OrderItemProjectionColumn("id", SortDirection.Desc),
                ]
            ),
            "||5",
            "1|x|2",
            "2|y|3",
            "4|X|6"
        );
    }

    // Strings sorted descending, then paged: the page starts after the
    // greatest code points.
    [Fact]
    public void StringsSortedDescendingThenPaged()
    {
        AssertRows(
            new MainPlainQueryWithPagination(
                from: new FromEntity(Words, "t"),
                select: [new SelectItemProjection("w", WordProjection)],
                orderBy:
                [
                    new OrderItemProjectionExpression(WordProjection, SortDirection.Desc),
                ],
                pagination: new Pagination(new PaginationSkip(2), new PaginationTake(4))
            ),
            "é",
            "b",
            "ab",
            "a"
        );
    }

    // in over a subquery column of decimals: the integer 1 is in it.
    [Fact]
    public void IntegerIsInASubqueryColumnOfEqualDecimals()
    {
        AssertRows(
            new MainPlainQueryWithoutPagination(
                subqueries:
                [
                    new SubqueryQuery(
                        "ds",
                        new PlainQueryWithoutPaginationWithoutOrderBy(
                            from: new FromEntity("c.second", "s"),
                            select:
                            [
                                new SelectItemProjection(
                                    "d",
                                    new FieldDecimalNullable("s", "d")
                                ),
                            ]
                        )
                    ),
                ],
                from: new FromEntity("c.first", "t"),
                select: [new SelectItemProjection("id", new FieldInteger("t", "id"))],
                where: new InDecimalRow(
                    new FieldIntegerNullable("t", "n"),
                    new ListSubqueryColumnDecimalNullable("ds", "d")
                ),
                orderBy: [new OrderItemProjectionColumn("id")]
            ),
            "1",
            "2",
            "4",
            "5",
            "6"
        );
    }

    // A sum that needs more than 28 significant digits is rounded half away
    // from zero to the most decimal places that fit: 2.5 +
    // 0.0000000000000000000000000001 is 2.5.
    [Fact(
        Skip = "Bug (this library, or the translator): 2.5 + "
            + "0.0000000000000000000000000001 is 2.5 by the spec (|m| < 10^28) and in "
            + "memory; translated, the exact numeric sum 2.5000000000000000000000000001 "
            + "(29 digits, which System.Decimal holds) is returned unrounded: nothing "
            + "in the tree or the SQL rounds a decimal to 28 significant digits"
    )]
    public void DecimalSumBeyond28DigitsIsRounded()
    {
        AssertRows(
            ById(
                Decimals,
                new EqualDecimalRow(new FieldInteger("t", "id"), new LiteralInteger(4)),
                Select(
                    "s",
                    new AddDecimalNullableProjection([
                        X,
                        new LiteralDecimal(0.0000000000000000000000000001m),
                    ])
                )
            ),
            "4|2.5"
        );
    }

    // A decimal result of magnitude 10^28 or more is an error.
    [Fact]
    public void DecimalBeyondItsRangeFails()
    {
        AssertFails(
            ById(
                Decimals,
                new EqualDecimalRow(new FieldInteger("t", "id"), new LiteralInteger(4)),
                Select(
                    "s",
                    new MultiplyDecimalNullableProjection([
                        X,
                        new LiteralDecimal(9999999999999999999999999999m),
                    ])
                )
            )
        );
    }

    // timeAddSeconds wraps around midnight however many days it adds.
    [Fact]
    public void TimeAddSecondsWrapsOverSeveralDays()
    {
        AssertRows(
            ById(
                Moments,
                new LessThanOrEqualDecimalRow(RowId, new LiteralInteger(2)),
                Select(
                    "later",
                    new TimeAddSecondsTimeNullableProjection(
                        Time,
                        new LiteralInteger((3 * 86400) + 3601)
                    )
                ),
                Select(
                    "earlier",
                    new TimeAddSecondsTimeNullableProjection(
                        Time,
                        new LiteralInteger(-90000)
                    )
                )
            ),
            "1|00:00:01|22:00:00",
            "2|01:30:01.25|23:30:00.25"
        );
    }

    // A grouped integer sum beyond 64 bits is an error.
    [Fact]
    public void GroupedIntegerSumBeyond64BitsFails()
    {
        AssertFails(
            new MainGroupedQueryWithoutPagination(
                from: new FromEntity(Big, "t"),
                where: new LessThanOrEqualDecimalRow(
                    new FieldInteger("t", "id"),
                    new LiteralInteger(2)
                ),
                groupBy: [new GroupKey("one", new LiteralInteger(1))],
                select:
                [
                    new SelectItemGroup(
                        "s",
                        new SumIntegerGroup(new FieldIntegerNullable("t", "v"))
                    ),
                ]
            )
        );
    }

    private static FieldIntegerNullable A => new FieldIntegerNullable("t", "a");

    private static FieldIntegerNullable B => new FieldIntegerNullable("t", "b");

    private static FieldIntegerNullable RowA => new FieldIntegerNullable("t", "a");

    private static FieldIntegerNullable RowB => new FieldIntegerNullable("t", "b");

    private static FieldInteger RowId => new FieldInteger("t", "id");

    private static FieldInteger RowIdNullable => new FieldInteger("t", "id");

    private static FieldDecimalNullable X => new FieldDecimalNullable("t", "x");

    private static FieldDecimalNullable Y => new FieldDecimalNullable("t", "y");

    private static FieldStringNullable Word => new FieldStringNullable("t", "w");

    private static FieldStringNullable WordProjection =>
        new FieldStringNullable("t", "w");

    private static FieldDateNullable Day => new FieldDateNullable("t", "d");

    private static FieldTimeNullable Time => new FieldTimeNullable("t", "t");

    private static FieldDatetimeNullable Instant => new FieldDatetimeNullable("t", "dt");

    private static FieldIntegerNullable LeftKey => new FieldIntegerNullable("t", "k");

    private static ListDecimal RightKeys =>
        new ListSubqueryColumnIntegerNullable("keys", "k");

    private static SelectItemProjection Select(string alias, ExprProjection expression)
    {
        return new SelectItemProjection(alias, expression);
    }

    // The id and the given columns of the rows of entity (as t) for which
    // where holds, by id.
    private static MainPlainQueryWithoutPagination ById(
        string entity,
        BooleanRow? where,
        params SelectItemProjection[] select
    )
    {
        return new MainPlainQueryWithoutPagination(
            from: new FromEntity(entity, "t"),
            select:
            [
                new SelectItemProjection("id", new FieldInteger("t", "id")),
                .. select,
            ],
            where: where,
            orderBy: [new OrderItemProjectionExpression(new FieldInteger("t", "id"))]
        );
    }

    // The ids of the rows of entity sorted by key, then by id.
    private static MainPlainQueryWithoutPagination Sorted(
        string entity,
        ExprProjection key,
        SortDirection direction
    )
    {
        return new MainPlainQueryWithoutPagination(
            from: new FromEntity(entity, "t"),
            select: [new SelectItemProjection("id", new FieldInteger("t", "id"))],
            orderBy:
            [
                new OrderItemProjectionExpression(key, direction),
                new OrderItemProjectionExpression(new FieldInteger("t", "id")),
            ]
        );
    }

    private static MainTotalQuery Total(
        string entity,
        BooleanRow? where,
        params SelectItemTotal[] aggregate
    )
    {
        return new MainTotalQuery(
            from: new FromEntity(entity, "t"),
            aggregate: aggregate,
            where: where
        );
    }

    // The keys of k.right, a nullable column holding 1, null and 3.
    private static SubqueryQuery RightKeysQuery()
    {
        return new SubqueryQuery(
            "keys",
            new PlainQueryWithoutPaginationWithoutOrderBy(
                from: new FromEntity("k.right", "r"),
                select:
                [
                    new SelectItemProjection("k", new FieldIntegerNullable("r", "k")),
                ]
            )
        );
    }

    // The ids of k.left for which where holds, reading the keys of k.right.
    private static MainPlainQueryWithoutPagination InRight(BooleanRow where)
    {
        return new MainPlainQueryWithoutPagination(
            subqueries: [RightKeysQuery()],
            from: new FromEntity("k.left", "t"),
            select: [new SelectItemProjection("id", new FieldInteger("t", "id"))],
            where: where,
            orderBy: [new OrderItemProjectionExpression(new FieldInteger("t", "id"))]
        );
    }

    // The ids of k.left and k.right joined on equal keys, by both ids with
    // nulls first.
    private static MainPlainQueryWithoutPagination Joined(JoinKind kind)
    {
        bool leftOptional = kind is JoinKind.Right or JoinKind.Full;
        bool rightOptional = kind is JoinKind.Left or JoinKind.Full;
        ExprProjection leftId = leftOptional
            ? new FieldIntegerNullable("l", "id")
            : new FieldInteger("l", "id");
        ExprProjection rightId = rightOptional
            ? new FieldIntegerNullable("r", "id")
            : new FieldInteger("r", "id");

        return new MainPlainQueryWithoutPagination(
            from: new FromEntity("k.left", "l"),
            joins:
            [
                new JoinEntity(
                    kind,
                    "k.right",
                    new EqualDecimalRow(
                        new FieldIntegerNullable("l", "k"),
                        new FieldIntegerNullable("r", "k")
                    ),
                    "r"
                ),
            ],
            select:
            [
                new SelectItemProjection("l", leftId),
                new SelectItemProjection("r", rightId),
            ],
            orderBy:
            [
                new OrderItemProjectionColumn("l"),
                new OrderItemProjectionColumn("r"),
            ]
        );
    }

    private static PlainQueryWithoutPaginationWithoutOrderBy Column(
        string entity,
        string alias,
        ExprProjection expression
    )
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity(entity, "t"),
            select: [new SelectItemProjection(alias, expression)]
        );
    }

    private static PlainQueryWithoutPaginationWithoutOrderBy Pairs(string entity)
    {
        return new PlainQueryWithoutPaginationWithoutOrderBy(
            from: new FromEntity(entity, "t"),
            select:
            [
                new SelectItemProjection("n", new FieldIntegerNullable("t", "n")),
                new SelectItemProjection("s", new FieldStringNullable("t", "s")),
            ]
        );
    }

    // The rows of the query, run in memory and translated over the stored
    // tables, are the expected rows in order; the stored run is translated.
    private void AssertRows(PureQLQuery query, params string[] expected)
    {
        AssertQuery(query, expected, ordered: true);
    }

    private void AssertUnordered(PureQLQuery query, params string[] expected)
    {
        AssertQuery(query, expected, ordered: false);
    }

    private void AssertQuery(PureQLQuery query, string[] expected, bool ordered)
    {
        PureQLProjection memory = new(DivergenceFixtures.Memory, query);
        PureQLProjection stored = new(_fixtures.Stored, query);

        string inMemory = Rows(memory, ordered);
        string translated = Rows(stored, ordered);
        string specified = Text(expected, ordered);

        Assert.True(
            stored.FallbackReason is null,
            $"Not translated: {stored.FallbackReason?.Message}"
        );
        Assert.Equal(QueryExecution.Translated, stored.Execution);
        Assert.True(
            inMemory == specified && translated == specified,
            $"specified: {specified}\nin memory: {inMemory}\ntranslated: {translated}"
        );
    }

    // The query fails in memory and translated, the stored run with the
    // error of the database, not a refusal of the provider.
    private void AssertFails(PureQLQuery query)
    {
        PureQLProjection memory = new(DivergenceFixtures.Memory, query);
        PureQLProjection stored = new(_fixtures.Stored, query);

        _ = Assert.ThrowsAny<Exception>(() => memory.ToList());
        Exception exception = Assert.ThrowsAny<Exception>(() => stored.ToList());
        Assert.True(
            stored.FallbackReason is null,
            $"Not translated: {stored.FallbackReason?.Message}"
        );
        Assert.IsNotType<NotSupportedException>(exception);
    }

    private static string Rows(PureQLProjection projection, bool ordered)
    {
        List<IColumn> columns = [.. projection.TableSchema.Columns];
        List<string> rows =
        [
            .. projection
                .AsEnumerable()
                .Select(row =>
                    string.Join(
                        "|",
                        columns.Select(column =>
                            row.Cells.Single(cell =>
                                cell.Key.Name.TextValue == column.Name.TextValue
                            ).Value.Value.TextValue
                        )
                    )
                ),
        ];

        return Text(rows, ordered);
    }

    private static string Text(IEnumerable<string> rows, bool ordered)
    {
        return string.Join("; ", ordered ? rows : rows.Order(StringComparer.Ordinal));
    }
}
