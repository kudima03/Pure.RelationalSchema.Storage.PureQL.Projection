using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.GroupBy;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// HAVING equality matrix (issue #141): SingleValueEquality over every
// comparable type (Number, Date, DateTime, Time, String), a boolean
// composite equality between two aggregate comparisons, and the Uuid
// fail-fast case. Orders are grouped by user for Number/Date/DateTime/
// String; Users are grouped by Active for Time. Expected surviving group
// keys are computed independently from the ground-truth record lists per
// SQL HAVING semantics.
[Trait("Clause", "Having")]
public sealed class HavingEqualityMatrixTests
{
    // ---- Number: count(Orders.Id) == constant ----

    [Fact]
    [Trait("Feature", "HavingNumberEquality")]
    public void HavingCountEqualConstantKeepsMatchingGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingCountEqualExistingValueQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Count() == 2)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < orderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingNumberEquality")]
    public void HavingCountEqualConstantKeepsNoGroupsWhenNoMatch()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingCountEqualAbsentValueQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.DoesNotContain(
            5,
            orderRows.GroupBy(order => order.OrderUserId).Select(group => group.Count())
        );
        Assert.Equal(0, result.Count);
    }

    // ---- Boolean composite: (count > k) == (aggregate > m) ----

    [Fact]
    [Trait("Feature", "HavingBooleanComposite")]
    public void HavingBooleanEqualityOfCountAndSumComparisonsKeepsMatchingTruth()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingBooleanEqualityOfCountAndSumComparisonsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    (group.Count() > 1) == (group.Sum(order => order.OrderTotal) > 150)
                )
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < orderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingBooleanComposite")]
    public void HavingBooleanEqualityOfCountAndMaxComparisonsKeepsMatchingTruth()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingBooleanEqualityOfCountAndMaxComparisonsQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    (group.Count() > 1)
                    == (group.Max(order => order.OrderTotal) >= 300)
                )
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count
                < orderRows.Select(order => order.OrderUserId).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    // ---- Date: max(Orders.PlacedOn) == constant ----

    [Fact]
    [Trait("Feature", "HavingDateEquality")]
    public void HavingMaxPlacedOnEqualConstantKeepsMatchingGroup()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly threshold = new DateOnly(2024, 6, 2);

        Query query = new HavingMaxPlacedOnEqualExistingValueQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Max(order => order.PlacedOn) == threshold)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value
            ),
        ];

        _ = Assert.Single(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingDateEquality")]
    public void HavingMaxPlacedOnEqualConstantKeepsNoGroupsWhenNoMatch()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly threshold = new DateOnly(2024, 1, 1);

        Query query = new HavingMaxPlacedOnEqualAbsentValueQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.DoesNotContain(
            threshold,
            orderRows
                .GroupBy(order => order.OrderUserId)
                .Select(group => group.Max(order => order.PlacedOn))
        );
        Assert.Equal(0, result.Count);
    }

    // ---- DateTime: max(Orders.PlacedAt) == constant ----

    [Fact]
    [Trait("Feature", "HavingDateTimeEquality")]
    public void HavingMaxPlacedAtEqualConstantKeepsMatchingGroup()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateTime threshold = new DateTime(2024, 6, 5, 14, 0, 0);

        Query query = new HavingMaxPlacedAtEqualExistingValueQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Max(order => order.PlacedAt) == threshold)
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value
            ),
        ];

        _ = Assert.Single(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingDateTimeEquality")]
    public void HavingMaxPlacedAtEqualConstantKeepsNoGroupsWhenNoMatch()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateTime threshold = new DateTime(2024, 1, 1, 0, 0, 0);

        Query query = new HavingMaxPlacedAtEqualAbsentValueQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.DoesNotContain(
            threshold,
            orderRows
                .GroupBy(order => order.OrderUserId)
                .Select(group => group.Max(order => order.PlacedAt))
        );
        Assert.Equal(0, result.Count);
    }

    // ---- Time: max(Users.ShiftStart) == constant, grouped by Active ----

    [Fact]
    [Trait("Feature", "HavingTimeEquality")]
    public void HavingMaxShiftStartEqualConstantKeepsMatchingGroup()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly threshold = new TimeOnly(11, 30, 0);

        Query query = new HavingMaxShiftStartEqualExistingValueQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<bool> expected =
        [
            .. userRows
                .GroupBy(user => user.UserActive)
                .Where(group => group.Max(user => user.ShiftStart) == threshold)
                .Select(group => group.Key),
        ];

        HashSet<bool> actual =
        [
            .. result.Rows.Select(row =>
                row.Bool(new UserActiveColumn().Name.TextValue)!.Value
            ),
        ];

        _ = Assert.Single(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingTimeEquality")]
    public void HavingMaxShiftStartEqualConstantKeepsNoGroupsWhenNoMatch()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly threshold = new TimeOnly(0, 0, 0);

        Query query = new HavingMaxShiftStartEqualAbsentValueQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.DoesNotContain(
            threshold,
            userRows
                .GroupBy(user => user.UserActive)
                .Select(group => group.Max(user => user.ShiftStart))
        );
        Assert.Equal(0, result.Count);
    }

    // ---- String: min(Orders.Status) == constant (ordinal) ----

    [Fact]
    [Trait("Feature", "HavingStringEquality")]
    public void HavingMinStatusEqualConstantKeepsMatchingGroup()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingMinStatusEqualExistingValueQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    string.Equals(
                        group.Select(order => order.OrderStatus).Min(StringComparer.Ordinal),
                        "cancelled",
                        StringComparison.Ordinal
                    )
                )
                .Select(group => group.Key),
        ];

        HashSet<Guid> actual =
        [
            .. result.Rows.Select(row =>
                row.Uuid(new OrderUserIdColumn().Name.TextValue)!.Value
            ),
        ];

        _ = Assert.Single(expected);
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingStringEquality")]
    public void HavingMinStatusEqualConstantKeepsNoGroupsWhenNoMatch()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingMinStatusEqualAbsentValueQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.DoesNotContain(
            "unknown",
            orderRows
                .GroupBy(order => order.OrderUserId)
                .Select(group =>
                    group.Select(order => order.OrderStatus).Min(StringComparer.Ordinal)
                )
        );
        Assert.Equal(0, result.Count);
    }

    // ---- Uuid: no aggregate arm exists on UuidReturning (only Parameter and
    // Scalar - see PureQL.CSharp.Model.Returnings.UuidReturning), so "an
    // aggregate over a uuid column" cannot even be constructed; there is no
    // MinUuid/MaxUuid in the model. The closest HAVING-shaped probe is a
    // UuidParameter operand, which AggregateEvaluator.BuildUuid rejects with
    // its parameter-not-supported fail-fast (matching every other
    // parameterised HAVING/WHERE operand - see Parameters/ParameterTests.cs).
    // This is intentional, spec-correct fail-fast behaviour, not a gap.

    [Fact]
    [Trait("Feature", "HavingUuidEquality")]
    public void HavingUuidParameterEqualityFailsFastWithoutBinding()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        Query query = new HavingUuidParameterEqualityQuery().Value;

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(datasets, query)
        );
    }
}
