using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.GroupBy;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// HAVING comparison matrix (issue #141): all 4 ComparisonOperator values over
// Number, Date, DateTime, Time and String aggregates (plus Number
// aggregate-vs-aggregate), each type covering a threshold that keeps some,
// all or none of the groups across its 4 operators. Orders are grouped by
// user for Number/Date/DateTime/String; Users are grouped by Active for
// Time. Expected surviving group keys are computed independently from the
// ground-truth record lists per SQL HAVING semantics.
[Trait("Clause", "Having")]
public sealed class HavingConditionMatrixTests
{
    // ---- Number: sum(Orders.Total) <op> constant ----

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingSumGreaterThanConstantKeepsSomeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingSumGreaterThanConstantQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Sum(order => order.OrderTotal) > 150)
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
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingSumGreaterThanOrEqualConstantKeepsAllGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingSumGreaterThanOrEqualConstantQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows.Select(order => order.OrderUserId).Distinct().Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingSumLessThanConstantKeepsNoGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        Query query = new HavingSumLessThanConstantQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingSumLessThanOrEqualConstantKeepsSomeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingSumLessThanOrEqualConstantQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Sum(order => order.OrderTotal) <= 150.50)
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

    // ---- Number: max(Orders.Total) <op> min(Orders.Total) (aggregate-vs-aggregate) ----

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingMaxTotalGreaterThanMinTotalKeepsMultiOrderGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingMaxTotalGreaterThanMinTotalQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    group.Max(order => order.OrderTotal)
                    > group.Min(order => order.OrderTotal)
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
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingMaxTotalGreaterThanOrEqualMinTotalKeepsAllGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingMaxTotalGreaterThanOrEqualMinTotalQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows.Select(order => order.OrderUserId).Distinct().Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingMaxTotalLessThanMinTotalKeepsNoGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        Query query = new HavingMaxTotalLessThanMinTotalQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingNumberComparison")]
    public void HavingMaxTotalLessThanOrEqualMinTotalKeepsSingleOrderGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingMaxTotalLessThanOrEqualMinTotalQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    group.Max(order => order.OrderTotal)
                    <= group.Min(order => order.OrderTotal)
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

    // ---- Date: max(Orders.PlacedOn) <op> constant ----

    [Fact]
    [Trait("Feature", "HavingDateComparison")]
    public void HavingMaxPlacedOnGreaterThanConstantKeepsSomeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly threshold = new DateOnly(2024, 6, 3);

        Query query = new HavingMaxPlacedOnGreaterThanConstantQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Max(order => order.PlacedOn) > threshold)
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
    [Trait("Feature", "HavingDateComparison")]
    public void HavingMaxPlacedOnGreaterThanOrEqualConstantKeepsAllGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new HavingMaxPlacedOnGreaterThanOrEqualConstantQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows.Select(order => order.OrderUserId).Distinct().Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingDateComparison")]
    public void HavingMaxPlacedOnLessThanConstantKeepsNoGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new HavingMaxPlacedOnLessThanConstantQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingDateComparison")]
    public void HavingMaxPlacedOnLessThanOrEqualConstantKeepsSomeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateOnly threshold = new DateOnly(2024, 6, 3);

        Query query = new HavingMaxPlacedOnLessThanOrEqualConstantQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Max(order => order.PlacedOn) <= threshold)
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

    // ---- DateTime: max(Orders.PlacedAt) <op> constant ----

    [Fact]
    [Trait("Feature", "HavingDateTimeComparison")]
    public void HavingMaxPlacedAtGreaterThanConstantKeepsSomeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateTime threshold = new DateTime(2024, 6, 3, 12, 0, 0);

        Query query = new HavingMaxPlacedAtGreaterThanConstantQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Max(order => order.PlacedAt) > threshold)
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
    [Trait("Feature", "HavingDateTimeComparison")]
    public void HavingMaxPlacedAtGreaterThanOrEqualConstantKeepsAllGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        Query query = new HavingMaxPlacedAtGreaterThanOrEqualConstantQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows.Select(order => order.OrderUserId).Distinct().Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingDateTimeComparison")]
    public void HavingMaxPlacedAtLessThanConstantKeepsNoGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new HavingMaxPlacedAtLessThanConstantQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingDateTimeComparison")]
    public void HavingMaxPlacedAtLessThanOrEqualConstantKeepsSomeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        DateTime threshold = new DateTime(2024, 6, 3, 12, 0, 0);

        Query query = new HavingMaxPlacedAtLessThanOrEqualConstantQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group => group.Max(order => order.PlacedAt) <= threshold)
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

    // ---- Time: max(Users.ShiftStart) <op> constant, grouped by Active ----

    [Fact]
    [Trait("Feature", "HavingTimeComparison")]
    public void HavingMaxShiftStartGreaterThanConstantKeepsSomeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly threshold = new TimeOnly(10, 0, 0);

        Query query = new HavingMaxShiftStartGreaterThanConstantQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<bool> expected =
        [
            .. userRows
                .GroupBy(user => user.UserActive)
                .Where(group => group.Max(user => user.ShiftStart) > threshold)
                .Select(group => group.Key),
        ];

        HashSet<bool> actual =
        [
            .. result.Rows.Select(row =>
                row.Bool(new UserActiveColumn().Name.TextValue)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count < userRows.Select(user => user.UserActive).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    [Fact]
    [Trait("Feature", "HavingTimeComparison")]
    public void HavingMaxShiftStartGreaterThanOrEqualConstantKeepsAllGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new HavingMaxShiftStartGreaterThanOrEqualConstantQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = userRows.Select(user => user.UserActive).Distinct().Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingTimeComparison")]
    public void HavingMaxShiftStartLessThanConstantKeepsNoGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new HavingMaxShiftStartLessThanConstantQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingTimeComparison")]
    public void HavingMaxShiftStartLessThanOrEqualConstantKeepsSomeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];
        TimeOnly threshold = new TimeOnly(10, 0, 0);

        Query query = new HavingMaxShiftStartLessThanOrEqualConstantQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<bool> expected =
        [
            .. userRows
                .GroupBy(user => user.UserActive)
                .Where(group => group.Max(user => user.ShiftStart) <= threshold)
                .Select(group => group.Key),
        ];

        HashSet<bool> actual =
        [
            .. result.Rows.Select(row =>
                row.Bool(new UserActiveColumn().Name.TextValue)!.Value
            ),
        ];

        Assert.NotEmpty(expected);
        Assert.True(
            expected.Count < userRows.Select(user => user.UserActive).Distinct().Count()
        );
        Assert.Equal(expected, actual);
    }

    // ---- String: min(Orders.Status) <op> constant (ordinal) ----

    [Fact]
    [Trait("Feature", "HavingStringComparison")]
    public void HavingMinStatusGreaterThanConstantKeepsSomeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingMinStatusGreaterThanConstantQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    string.CompareOrdinal(
                        group.Select(order => order.OrderStatus).Min(StringComparer.Ordinal),
                        "pending"
                    ) > 0
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
    [Trait("Feature", "HavingStringComparison")]
    public void HavingMinStatusGreaterThanOrEqualConstantKeepsAllGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingMinStatusGreaterThanOrEqualConstantQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        int expected = orderRows.Select(order => order.OrderUserId).Distinct().Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingStringComparison")]
    public void HavingMinStatusLessThanConstantKeepsNoGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        Query query = new HavingMinStatusLessThanConstantQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        Assert.Equal(0, result.Count);
    }

    [Fact]
    [Trait("Feature", "HavingStringComparison")]
    public void HavingMinStatusLessThanOrEqualConstantKeepsSomeGroups()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];
        Query query = new HavingMinStatusLessThanOrEqualConstantQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        HashSet<Guid> expected =
        [
            .. orderRows
                .GroupBy(order => order.OrderUserId)
                .Where(group =>
                    string.CompareOrdinal(
                        group.Select(order => order.OrderStatus).Min(StringComparer.Ordinal),
                        "pending"
                    ) <= 0
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
}
