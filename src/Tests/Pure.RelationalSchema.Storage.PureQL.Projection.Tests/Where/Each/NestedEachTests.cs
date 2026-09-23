using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Where.Each;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Deep nesting of the per-row (each*) boolean family (eachAnd/eachOr/eachNot
// composing eachEquality/eachComparison/eachArithmetic leaves), at increasing
// depth (2 through 5 levels), plus the other batch items from issue #98:
// mixed arithmetic+boolean nesting, each* over joined columns inside a
// nested tree, and negative/empty cases. Unlike the scalar family
// (NestedBooleanTests), each leaf here reads a real per-row field, so the
// keep/remove outcome varies row by row rather than being all-or-nothing;
// every test derives the expected per-row truth value inline and cross-checks
// against a LINQ-equivalent predicate over the ground-truth records.
[Trait("Clause", "Where")]
[Trait("Feature", "NestedEach")]
public sealed class NestedEachTests
{
    // 2-level: eachAnd(total > 100, status == "shipped")
    [Fact]
    public void TwoLevelEachAndOfComparisonAndEqualityFiltersByBothConditions()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new TwoLevelEachAndOfComparisonAndEqualityQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(o => o.OrderTotal > 100 && o.OrderStatus == "shipped"),
            result.Count
        );
    }

    // 2-level: eachOr(eachNot(status == "cancelled"), total < 0)
    [Fact]
    public void TwoLevelEachOrOfNotAndComparisonFiltersByEitherCondition()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new TwoLevelEachOrOfNotAndComparisonQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(o => o.OrderStatus != "cancelled" || o.OrderTotal < 0),
            result.Count
        );
    }

    // 3-level: eachAnd(eachOr(total >= 200, status == "pending"),
    //                   eachNot(status == "cancelled"))
    [Fact]
    public void ThreeLevelEachAndOfOrAndNotFiltersByCombinedCondition()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new ThreeLevelEachAndOfOrAndNotQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(o =>
                (o.OrderTotal >= 200 || o.OrderStatus == "pending")
                && o.OrderStatus != "cancelled"
            ),
            result.Count
        );
    }

    // 3-level, mixed arithmetic + boolean:
    //   eachAnd(eachComparison(eachAdd(total, 50) > 150),
    //           eachOr(status == "shipped", total < 60))
    [Fact]
    public void ThreeLevelPerRowArithmeticInsideComparisonInsideEachAndFilters()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query =
            new ThreeLevelPerRowArithmeticInsideComparisonInsideEachAndQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(o =>
                o.OrderTotal + 50 > 150
                && (o.OrderStatus == "shipped" || o.OrderTotal < 60)
            ),
            result.Count
        );
    }

    // 4-level: eachAnd(eachOr(eachNot(eachAnd(total > 90, status == "shipped")),
    //                          total >= 300),
    //                   eachNot(status == "cancelled"))
    [Fact]
    public void FourLevelEachAndOrNotTreeFiltersByCombinedCondition()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new FourLevelEachAndOrNotTreeQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(
            orderRows.Count(o =>
                (!(o.OrderTotal > 90 && o.OrderStatus == "shipped") || o.OrderTotal >= 300)
                && o.OrderStatus != "cancelled"
            ),
            result.Count
        );
    }

    // 5-level tree, AND-rooted:
    //   eachAnd(
    //     eachOr(eachNot(eachAnd(a, b)), c),
    //     eachOr(eachNot(d), eachAnd(e, f))
    //   )
    // where, per row:
    //   a = total > 100          b = status == "pending"
    //   c = total >= 300         d = status == "cancelled"
    //   e = total < 100          f = status == "shipped"
    //
    // Per-row derivation (order id / total / status):
    //   101 / 100.50 / shipped   : a=T b=F -> and=F -> not=T -> left=T.
    //                              d=F -> not=T -> right=T. root = T AND T = T.
    //   102 /  50.00 / pending   : a=F b=T -> and=F -> not=T -> left=T.
    //                              d=F -> not=T -> right=T. root = T.
    //   103 / 200.00 / shipped   : a=T b=F -> and=F -> not=T -> left=T.
    //                              d=F -> not=T -> right=T. root = T.
    //   104 /  75.25 / cancelled : a=F b=F -> and=F -> not=T -> left=T.
    //                              d=T -> not=F. e=T f=F -> and=F.
    //                              right = F OR F = F. root = T AND F = F.
    //   105 / 300.00 / shipped   : a=T b=F -> and=F -> not=T -> left=T.
    //                              d=F -> not=T -> right=T. root = T.
    //   106 / 100.50 / pending   : a=T b=T -> and=T -> not=F.
    //                              c=100.50>=300=F -> left = F OR F = F.
    //                              root = F AND (anything) = F.
    //
    // Kept: 101, 102, 103, 105. Removed: 104, 106.
    [Fact]
    public void FiveLevelAndRootedTreeMatchesRowByRowAgainstLinqPredicate()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new FiveLevelAndRootedTreeQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(o =>
                {
                    bool av = o.OrderTotal > 100;
                    bool bv = o.OrderStatus == "pending";
                    bool cv = o.OrderTotal >= 300;
                    bool dv = o.OrderStatus == "cancelled";
                    bool ev = o.OrderTotal < 100;
                    bool fv = o.OrderStatus == "shipped";
                    bool left = !(av && bv) || cv;
                    bool right = !dv || (ev && fv);
                    return left && right;
                })
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Guid[] actual =
        [
            .. result.Rows
                .Select(row => row.Uuid(new OrderIdColumn().Name.TextValue)!.Value)
                .OrderBy(id => id),
        ];

        Assert.Equal(4, expected.Length);
        Assert.Equal(expected, actual);
    }

    // Each* predicate over joined columns nested three levels deep:
    //   eachAnd(eachOr(user_age > 28, order_status == "pending"),
    //           eachNot(user_active == false))
    [Fact]
    public void EachTreeOverJoinedColumnsNestedInsideEachAndFiltersByBothSides()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new EachTreeOverJoinedColumnsNestedInsideEachAndQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(o =>
                {
                    UserRecord user = userRows.Single(u => u.UserId == o.OrderUserId);
                    return (user.UserAge > 28 || o.OrderStatus == "pending")
                        && user.UserActive;
                })
                .Select(o => o.OrderId)
                .OrderBy(id => id),
        ];

        Guid[] actual =
        [
            .. result.Rows
                .Select(row => row.Uuid(new OrderIdColumn().Name.TextValue)!.Value)
                .OrderBy(id => id),
        ];

        Assert.NotEmpty(expected);
        Assert.True(expected.Length < orderRows.Count);
        Assert.Equal(expected, actual);
    }

    // Negative/empty: a structurally 3-level tree that is unsatisfiable by
    // every row (no order total is ever outside +-100000), regardless of the
    // second AND operand.
    [Fact]
    public void EachTreeThatIsUnsatisfiableForEveryRowReturnsEmptyResult()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new UnsatisfiableEachTreeQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    // Negative/empty: a restrictive INNER JOIN that matches zero rows makes
    // every downstream row disappear, even when the each* WHERE tree (however
    // deeply nested) would otherwise evaluate true for every remaining row.
    [Fact]
    public void EachTreeOverRestrictiveJoinWithNoMatchesReturnsEmptyResult()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new EachTreeOverRestrictiveJoinQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }
}
