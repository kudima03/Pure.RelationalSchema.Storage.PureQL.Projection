namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// Ground-truth mirror of the sample dataset, using real .NET types.
// Tests compute expected results independently from these records, never by
// re-deriving from the translator under test.

internal sealed record UserRow(
    Guid UserId,
    string UserName,
    double UserAge,
    bool UserActive,
    DateOnly SignupDate,
    DateTime LastLogin,
    TimeOnly ShiftStart,
    // NULL-semantics fixture columns (issue #103): Score is NULL for two
    // users (Bob, Dan) to exercise three-valued WHERE/GROUP BY/aggregate/
    // DISTINCT/ORDER BY behavior; PrecisionValue and the Edge* triple hold
    // numeric-extreme and calendar-edge values for round-trip coverage.
    double? Score,
    double PrecisionValue,
    DateOnly EdgeDate,
    DateTime EdgeDateTime,
    TimeOnly EdgeTime
);

internal sealed record OrderRow(
    Guid OrderId,
    Guid OrderUserId,
    double OrderTotal,
    string OrderStatus,
    DateTime PlacedAt,
    DateOnly PlacedOn
);

internal sealed record ProductRow(
    Guid ProductId,
    string ProductName,
    double ProductPrice,
    bool ProductInStock
);

internal sealed record OrderItemRow(
    Guid ItemId,
    Guid ItemOrderId,
    Guid ItemProductId,
    double ItemQty
);

// Lives in a second schema ("audit") to exercise cross-schema joins.
internal sealed record LoginRow(Guid LoginId, Guid LoginUserId, DateTime LoginAt);
