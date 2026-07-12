using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

#pragma warning disable xUnit1004 // skipped: reproduces a known translator bug

// Reproduces issue #78: a select item that references a joined-table column
// whose name does NOT collide with any base-table column must still see that
// column after the join — plainly projected, aggregated over the whole set,
// or used as a groupBy key. The joining tables share the PK name "id" (the
// shape of the reported schema), but the referenced columns themselves
// ("actual_hours", "estimate_status") are unique to the joined table.
//
// The inner-join tests transcribe the issue's repro steps directly. The
// left-join tests pin the one in-library path that produces the reported
// symptoms (KeyNotFoundException from projection / silently empty
// aggregates): unmatched outer-join rows pass through unmerged, without the
// joined table's columns, instead of carrying null cells for them.
[Trait("Clause", "Join")]
[Trait("Feature", "JoinedColumnProjection")]
public sealed class JoinedTableColumnProjectionTests
{
    private static Join NeedsToEstimatesJoin()
    {
        return new Join(
            JoinType.Inner,
            CollidingNameDatabase.Estimates.Entity,
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                CollidingNameDatabase.Needs.Entity,
                                CollidingNameDatabase.Needs.Id
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                CollidingNameDatabase.Estimates.Entity,
                                CollidingNameDatabase.Estimates.NeedId
                            )
                        )
                    )
                )
            )
        );
    }

    [Fact]
    public void SelectOfNonCollidingJoinedColumnReturnsItsValues()
    {
        CollidingNameDatabase db = new CollidingNameDatabase();

        Query query = new Query(
            new FromExpression(CollidingNameDatabase.Needs.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                CollidingNameDatabase.Estimates.Entity,
                                CollidingNameDatabase.Estimates.ActualHours
                            )
                        )
                    )
                ),
            ],
            where: null,
            [NeedsToEstimatesJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double[] expected =
        [
            .. NeedRowsJoinedToEstimates(db)
                .Select(pair => pair.Estimate.EstimateActualHours)
                .OrderBy(hours => hours),
        ];

        double?[] actual =
        [
            .. result
                .Rows.Select(row =>
                    row.Double(CollidingNameDatabase.Estimates.ActualHours)
                )
                .OrderBy(hours => hours),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual.Select(hours => hours ?? 0).ToArray());
    }

    [Fact]
    public void WholeSetSumOverNonCollidingJoinedColumnComputesTheSum()
    {
        CollidingNameDatabase db = new CollidingNameDatabase();

        Query query = new Query(
            new FromExpression(CollidingNameDatabase.Needs.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            CollidingNameDatabase
                                                .Estimates
                                                .Entity,
                                            CollidingNameDatabase
                                                .Estimates
                                                .ActualHours
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "actual_hours_sum"
                ),
            ],
            where: null,
            [NeedsToEstimatesJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        double expected = NeedRowsJoinedToEstimates(db)
            .Sum(pair => pair.Estimate.EstimateActualHours);

        Assert.Equal(1, result.Count);
        Assert.Equal(expected, result.Row(0).Double("actual_hours_sum"));
    }

    [Fact]
    public void GroupByNonCollidingJoinedStringColumnGroupsByItsValues()
    {
        CollidingNameDatabase db = new CollidingNameDatabase();

        Query query = new Query(
            new FromExpression(CollidingNameDatabase.Needs.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                CollidingNameDatabase.Estimates.Entity,
                                CollidingNameDatabase.Estimates.Status
                            )
                        )
                    )
                ),
            ],
            where: null,
            [NeedsToEstimatesJoin()],
            [
                new Field(
                    new StringField(
                        CollidingNameDatabase.Estimates.Entity,
                        CollidingNameDatabase.Estimates.Status
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. NeedRowsJoinedToEstimates(db)
                .Select(pair => pair.Estimate.EstimateStatus)
                .Distinct()
                .OrderBy(status => status),
        ];

        string?[] actual =
        [
            .. result
                .Column(CollidingNameDatabase.Estimates.Status)
                .OrderBy(status => status),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact(
        Skip = "Issue #78: unmatched outer-join rows lack the joined table's "
            + "columns entirely, so projecting one throws KeyNotFoundException "
            + "instead of yielding null cells."
    )]
    [Trait("Status", "KnownGap")]
    public void LeftJoinUnmatchedRowsExposeJoinedColumnsAsNullCells()
    {
        CollidingNameDatabase db = new CollidingNameDatabase();

        Query query = new Query(
            new FromExpression(CollidingNameDatabase.Specialties.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new NumberField(
                                CollidingNameDatabase.Needs.Entity,
                                CollidingNameDatabase.Needs.PlannedHours
                            )
                        )
                    )
                ),
            ],
            where: null,
            [SpecialtiesToNeedsLeftJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int matched = db.NeedRows.Count;
        int unmatched = db.SpecialtyRows.Count(specialty =>
            db.NeedRows.All(need => need.NeedSpecialtyId != specialty.SpecialtyId)
        );

        double[] expectedHours =
        [
            .. db.NeedRows
                .Select(need => need.NeedPlannedHours)
                .OrderBy(hours => hours),
        ];

        double?[] cells =
        [
            .. result.Rows.Select(row =>
                row.Double(CollidingNameDatabase.Needs.PlannedHours)
            ),
        ];

        double[] actualHours =
        [
            .. cells
                .Where(hours => hours is not null)
                .Select(hours => hours!.Value)
                .OrderBy(hours => hours),
        ];

        Assert.Equal(matched + unmatched, result.Count);
        Assert.Equal(expectedHours, actualHours);
        Assert.Equal(unmatched, cells.Count(hours => hours is null));
    }

    [Fact(
        Skip = "Issue #78: grouping by a joined column crashes with "
            + "KeyNotFoundException when a group's rows come from the "
            + "unmatched outer-join fallback."
    )]
    [Trait("Status", "KnownGap")]
    public void LeftJoinGroupByJoinedColumnPutsUnmatchedRowsInNullGroup()
    {
        CollidingNameDatabase db = new CollidingNameDatabase();

        Query query = new Query(
            new FromExpression(CollidingNameDatabase.Specialties.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                CollidingNameDatabase.Needs.Entity,
                                CollidingNameDatabase.Needs.SpecialtyId
                            )
                        )
                    )
                ),
            ],
            where: null,
            [SpecialtiesToNeedsLeftJoin()],
            [
                new Field(
                    new UuidField(
                        CollidingNameDatabase.Needs.Entity,
                        CollidingNameDatabase.Needs.SpecialtyId
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int expectedGroups =
            db.NeedRows.Select(need => need.NeedSpecialtyId).Distinct().Count()
            + 1;

        Assert.Equal(expectedGroups, result.Count);
    }

    private static Join SpecialtiesToNeedsLeftJoin()
    {
        return new Join(
            JoinType.Left,
            CollidingNameDatabase.Needs.Entity,
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                CollidingNameDatabase.Specialties.Entity,
                                CollidingNameDatabase.Specialties.Id
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                CollidingNameDatabase.Needs.Entity,
                                CollidingNameDatabase.Needs.SpecialtyId
                            )
                        )
                    )
                )
            )
        );
    }

    private static IEnumerable<(NeedRow Need, EstimateRow Estimate)>
        NeedRowsJoinedToEstimates(CollidingNameDatabase db)
    {
        return
            from need in db.NeedRows
            join estimate in db.EstimateRows
                on need.NeedId equals estimate.EstimateNeedId
            select (need, estimate);
    }
}
