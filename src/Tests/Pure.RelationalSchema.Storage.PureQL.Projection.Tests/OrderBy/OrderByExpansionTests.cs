using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Samples.Queries.OrderBy;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.OrderBy;

// Deeper ORDER BY coverage: mixed-direction multi-key sorts with 3+ keys,
// many-key (4-5) composites, stability at full-tie depth, and ordering by an
// aliased select column. Expected sequences are produced by the equivalent
// stable LINQ ordering over the ground-truth records.
[Trait("Clause", "OrderBy")]
[Trait("Feature", "OrderByExpansion")]
public sealed class OrderByExpansionTests
{
    [Fact]
    public void OrderByActiveAscAgeDescNameAscOrdersThreeMixedDirectionKeys()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new OrderByActiveAscAgeDescNameAscQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows.OrderBy(user => user.UserActive)
                .ThenByDescending(user => user.UserAge)
                .ThenBy(user => user.UserName)
                .Select(user => user.UserName),
        ];

        string?[] actual = [.. result.Column(new UserNameColumn().Name.TextValue)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OrderByFiveKeysAppliesFullCompositeOrdering()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new OrderByFiveKeysQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows.OrderBy(user => user.UserActive)
                .ThenBy(user => user.UserAge)
                .ThenBy(user => user.SignupDate)
                .ThenByDescending(user => user.LastLogin)
                .ThenBy(user => user.UserName)
                .Select(user => user.UserName),
        ];

        string?[] actual = [.. result.Column(new UserNameColumn().Name.TextValue)];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void OrderByFullTieOnEveryKeyPreservesOriginalRelativeOrder()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        // Ann and Fay share SignupDate, LastLogin and ShiftStart exactly, so
        // ordering by that triple leaves zero distinguishing keys between
        // them: a stable sort must keep Ann (declared first) ahead of Fay
        // (declared last) in the output.
        Query query = new OrderByFullTieOnEveryKeyQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows.OrderBy(user => user.SignupDate)
                .ThenBy(user => user.LastLogin)
                .ThenBy(user => user.ShiftStart)
                .Select(user => user.UserName),
        ];

        string?[] actual = [.. result.Column(new UserNameColumn().Name.TextValue)];

        Assert.Equal(expected, actual);
        Assert.Contains("Ann", expected);
        Assert.Contains("Fay", expected);
        Assert.True(
            Array.IndexOf(expected, "Ann") < Array.IndexOf(expected, "Fay"),
            "Ground truth must exercise a full tie with Ann before Fay."
        );
    }

    [Fact]
    public void OrderByAliasedSelectColumnStillOrdersByUnderlyingField()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<OrderRecord> orderRows = [.. new OrderRecords()];

        // The select expression renames order_total to "grandTotal", but the
        // ORDER BY item still refers to the underlying field (there is no
        // alias reference in the model) and must keep sorting by it.
        Query query = new OrderByAliasedSelectColumnQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        double?[] expected =
        [
            .. orderRows.OrderBy(order => order.OrderTotal)
                .Select(order => (double?)order.OrderTotal),
        ];

        double?[] actual = [.. result.Rows.Select(row => row.Double("grandTotal"))];

        Assert.Equal(expected, actual);
    }

    // Rows produced by an unmatched LEFT JOIN side carry an empty
    // (NULL-equivalent) cell for the joined column. OrderByApplicator now
    // implements an intentional NULLS LAST contract regardless of sort
    // direction (matching PostgreSQL/Oracle/SQL Server's default): every
    // order-by item keys first by "is this cell NULL" (always ascending),
    // then by the real value in the requested direction. This test pins
    // ascending; see OrderByDescendingWithUnmatchedLeftJoinRowsPlacesNullsLast
    // below for the descending companion.
    [Fact]
    public void OrderByAscendingWithUnmatchedLeftJoinRowsPlacesNullsLast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new OrderByAscendingOverLeftJoinQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        // Eve and Fay place no orders, so their joined Total is NULL.
        // NULLS LAST: matched rows sorted ascending first, then the
        // unmatched users trailing in their original relative order.
        string[] expected =
        [
            "Ann",
            "Cara",
            "Ann",
            "Dan",
            "Bob",
            "Cara",
            "Eve",
            "Fay",
        ];

        string?[] actual = [.. result.Column(new UserNameColumn().Name.TextValue)];

        Assert.Equal(expected, actual);
    }

    // Companion to the ascending test above: pins that NULLS LAST also
    // holds under a descending sort (previously the only direction where
    // the old default Nullable<T> comparer already happened to place
    // NULLs last), proving the "regardless of direction" half of the
    // contract, not just the ascending half.
    [Fact]
    public void OrderByDescendingWithUnmatchedLeftJoinRowsPlacesNullsLast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new OrderByDescendingOverLeftJoinQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        // Eve and Fay place no orders, so their joined Total is NULL.
        // NULLS LAST holds here too: matched rows sorted descending first,
        // then the unmatched users trailing in their original relative
        // order (Ann/Dan tie at 100.50; a stable sort keeps Ann, whose
        // order row precedes Dan's, ahead of Dan for that tie).
        string[] expected =
        [
            "Cara",
            "Bob",
            "Ann",
            "Dan",
            "Cara",
            "Ann",
            "Eve",
            "Fay",
        ];

        string?[] actual = [.. result.Column(new UserNameColumn().Name.TextValue)];

        Assert.Equal(expected, actual);
    }

    // In group mode, the pipeline applies ORDER BY after GROUP BY + HAVING +
    // projection (see RowsFromDatasets.Build), so an OrderByItem is
    // evaluated against the projected, post-group rows. An aggregate
    // result (e.g. SUM(total) AS totalSum) exists as a column on that
    // projected row, so ordering by its alias orders the emitted groups by
    // their aggregate value, matching SQL's ORDER BY-after-GROUP BY
    // semantics.
    [Fact]
    public void OrderByAggregateResultOrdersEmittedGroupsByItsValue()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new OrderByAggregateResultQuery().Value;

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        // Ascending by summed total per status: cancelled (75.25) <
        // pending (150.50) < shipped (600.50).
        string[] expected = ["cancelled", "pending", "shipped"];

        string?[] actual = [.. result.Column(new OrderStatusColumn().Name.TextValue)];

        Assert.Equal(expected, actual);
    }
}
