namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// Ground-truth mirror of the colliding-name dataset, using real .NET types.
// Tests compute expected results independently from these records, never by
// re-deriving from the translator under test.

internal sealed record NeedRow(
    Guid NeedId,
    Guid NeedSpecialtyId,
    double NeedPlannedHours
);

internal sealed record SpecialtyRow(Guid SpecialtyId, string SpecialtyTitle);

internal sealed record EstimateRow(
    Guid EstimateId,
    Guid EstimateNeedId,
    string EstimateStatus,
    double EstimateActualHours
);
