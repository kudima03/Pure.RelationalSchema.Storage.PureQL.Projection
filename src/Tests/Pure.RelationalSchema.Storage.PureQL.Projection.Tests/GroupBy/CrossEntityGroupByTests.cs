using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.GroupBy;

// GROUP BY over a join: keys may come from both sides of the join
// (entity-qualified), and aggregates may fold columns of the other side.
// Uses the colliding-name dataset so entity qualification is load-bearing.
[Trait("Clause", "GroupBy")]
[Trait("Feature", "CrossEntityGroupBy")]
public sealed class CrossEntityGroupByTests
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
    public void GroupByKeysFromBothTablesYieldsDistinctCombinations()
    {
        CollidingNameDatabase db = new CollidingNameDatabase();

        Query query = new Query(
            new FromExpression(CollidingNameDatabase.Needs.Entity),
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
                    new UuidField(
                        CollidingNameDatabase.Needs.Entity,
                        CollidingNameDatabase.Needs.SpecialtyId
                    )
                ),
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

        (Guid, string)[] expected =
        [
            .. db.NeedRows
                .Join(
                    db.EstimateRows,
                    need => need.NeedId,
                    estimate => estimate.EstimateNeedId,
                    (need, estimate) =>
                        (need.NeedSpecialtyId, estimate.EstimateStatus)
                )
                .Distinct()
                .OrderBy(pair => pair.NeedSpecialtyId)
                .ThenBy(pair => pair.EstimateStatus),
        ];

        (Guid, string)[] actual =
        [
            .. result.Rows
                .Select(row =>
                    (
                        SpecialtyId: row
                            .Uuid(CollidingNameDatabase.Needs.SpecialtyId)!
                            .Value,
                        Status: row[CollidingNameDatabase.Estimates.Status]!
                    )
                )
                .OrderBy(pair => pair.SpecialtyId)
                .ThenBy(pair => pair.Status),
        ];

        Assert.Equal(expected, actual);
    }

    [Fact]
    public void GroupByJoinedKeyAggregatesBaseTableValues()
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
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            CollidingNameDatabase.Needs.Entity,
                                            CollidingNameDatabase.Needs.PlannedHours
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "plannedSum"
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

        Dictionary<string, double> expected = db.NeedRows
            .Join(
                db.EstimateRows,
                need => need.NeedId,
                estimate => estimate.EstimateNeedId,
                (need, estimate) =>
                    (estimate.EstimateStatus, need.NeedPlannedHours)
            )
            .GroupBy(pair => pair.EstimateStatus)
            .ToDictionary(
                group => group.Key,
                group => group.Sum(pair => pair.NeedPlannedHours)
            );

        Dictionary<string, double> actual = result.Rows.ToDictionary(
            row => row[CollidingNameDatabase.Estimates.Status]!,
            row => row.Double("plannedSum")!.Value
        );

        Assert.Equal(expected, actual);
    }
}
