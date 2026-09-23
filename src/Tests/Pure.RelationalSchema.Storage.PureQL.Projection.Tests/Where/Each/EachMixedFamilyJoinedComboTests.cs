using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.Where.Each;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Where.Each;

// Issue #155: mixed-family each* combos whose operands are drawn from both
// sides of a join - a base-table column compared against/combined with a
// joined-table column inside an each-arithmetic, then fed into a comparison
// and/or further boolean composition. Every expectation is derived
// independently in LINQ over the ground-truth lists under SQL result-set
// semantics, including the LEFT JOIN null-propagation case (unmatched side
// -> null operand -> comparison false -> row excluded).
[Trait("Clause", "Where")]
[Trait("Feature", "EachMixedFamilyCombo")]
public sealed class EachMixedFamilyJoinedComboTests
{
    // eachGreaterThan(eachAdd(order.total, user.age), 120) - cross-entity
    // arithmetic feeding a comparison, bare at the top of the tree.
    [Fact]
    public void EachGreaterThanOfSummedOrderTotalAndUserAgeAcrossJoinFiltersRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query =
            new EachGreaterThanOfSummedOrderTotalAndUserAgeAcrossJoinQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(o =>
                {
                    UserRecord user = userRows.Single(u => u.UserId == o.OrderUserId);
                    return o.OrderTotal + user.UserAge > 120;
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

    // eachAnd(eachGreaterThan(eachSubtract(user.age, order.total), -100),
    //         status == "shipped") - cross-entity arithmetic AND a plain,
    // same-side (order) string equality.
    [Fact]
    public void EachAndOfCrossEntityArithmeticComparisonAndOwnSideStringEquality()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query =
            new EachAndOfCrossEntityArithmeticComparisonAndOwnSideStringEqualityQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(o =>
                {
                    UserRecord user = userRows.Single(u => u.UserId == o.OrderUserId);
                    return user.UserAge - o.OrderTotal > -100 && o.OrderStatus == "shipped";
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

    // eachOr(user.active == false,
    //        eachGreaterThan(eachDateDiffDays(order.placed_on, user.signup_date), 1500))
    [Fact]
    public void EachOrOfJoinedBooleanEqualityAndCrossEntityDateDiffComparison()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query =
            new EachOrOfJoinedBooleanEqualityAndCrossEntityDateDiffComparisonQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(o =>
                {
                    UserRecord user = userRows.Single(u => u.UserId == o.OrderUserId);
                    int gap = o.PlacedOn.DayNumber - user.SignupDate.DayNumber;
                    return !user.UserActive || gap > 1500;
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

    // 4-level tree over joined columns:
    //   eachAnd(
    //     eachOr(user.active == false, eachGreaterThan(eachAdd(order.total, user.age), 300)),
    //     eachNot(status == "cancelled")
    //   )
    [Fact]
    public void FourLevelTreeOverJoinedColumnsMixingCrossEntityArithmeticAndBooleanOps()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query =
            new FourLevelTreeOverJoinedColumnsMixingCrossEntityArithmeticAndBooleanOpsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(o =>
                {
                    UserRecord user = userRows.Single(u => u.UserId == o.OrderUserId);
                    bool orCondition =
                        !user.UserActive || o.OrderTotal + user.UserAge > 300;
                    return orCondition && o.OrderStatus != "cancelled";
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

    // FROM shop.users LEFT JOIN shop.orders: eachGreaterThan(eachAdd(user.age,
    // order.total), 120). Eve and Fay place no orders, so their joined Total
    // is NULL; the cross-entity add propagates the NULL and the comparison
    // evaluates false (SQL 3VL), excluding those rows rather than throwing or
    // treating the missing side as zero.
    [Fact]
    public void EachLeftJoinWithCrossEntityArithmeticExcludesUnmatchedRows()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new EachLeftJoinWithCrossEntityArithmeticQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows
                .GroupJoin(
                    orderRows,
                    user => user.UserId,
                    order => order.OrderUserId,
                    (user, orders) => (user, orders)
                )
                .SelectMany(t => t.orders.DefaultIfEmpty(), (t, order) => (t.user, order))
                .Where(t => t.user.UserAge + (t.order?.OrderTotal) > 120)
                .Select(t => t.user.UserName)
                .OrderBy(name => name),
        ];

        string?[] actual = [.. result.Column(new UserNameColumn().Name.TextValue).OrderBy(n => n)];

        Assert.NotEmpty(expected);
        // Eve and Fay place no orders; their NULL total must never
        // participate in a kept row, regardless of the threshold.
        Assert.DoesNotContain("Eve", expected);
        Assert.DoesNotContain("Fay", expected);
        Assert.Equal(expected, actual);
    }

    // eachAnd(user.active == true,
    //         eachGreaterThan(eachAdd(order.total, user.age), [100, junk]
    //         (broadcast)))  -- field, nested cross-entity arithmetic, and a
    // broadcast literal array, all combined over a join.
    [Fact]
    public void ThreeOperandShapesCombineOverJoinedColumnsInOnePredicate()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new ThreeOperandShapesOverJoinedColumnsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Guid[] expected =
        [
            .. orderRows
                .Where(o =>
                {
                    UserRecord user = userRows.Single(u => u.UserId == o.OrderUserId);
                    return user.UserActive && o.OrderTotal + user.UserAge > 100;
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
}
