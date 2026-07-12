using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

#pragma warning disable xUnit1004 // skipped: reproduces a known translator bug

// Reproduces issue #77: when the from table and a joined table both carry a
// column with the same name (here the conventional PK name "id"), any
// reference to that name on the joined side — in select, groupBy, or the
// join's own on clause — must resolve to the joined table's value, not be
// silently shadowed by the base table's same-named column.
[Trait("Clause", "Join")]
[Trait("Feature", "ColumnNameCollision")]
public sealed class JoinColumnNameCollisionTests
{
    private static Join NeedsToSpecialtiesJoin()
    {
        return new Join(
            JoinType.Inner,
            CollidingNameDatabase.Specialties.Entity,
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                CollidingNameDatabase.Needs.Entity,
                                CollidingNameDatabase.Needs.SpecialtyId
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                CollidingNameDatabase.Specialties.Entity,
                                CollidingNameDatabase.Specialties.Id
                            )
                        )
                    )
                )
            )
        );
    }

    [Fact(
        Skip = "Issue #77: the join's on clause resolves the joined table's "
            + "same-named column to the base table's value."
    )]
    public void JoinOnClauseWithSameNamedColumnMatchesJoinedTableValues()
    {
        CollidingNameDatabase db = new CollidingNameDatabase();

        Query query = new Query(
            new FromExpression(CollidingNameDatabase.Needs.Entity),
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
            [NeedsToSpecialtiesJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        int expected = NeedRowsJoinedToSpecialties(db).Count();

        Assert.Equal(expected, result.Count);
    }

    [Fact(
        Skip = "Issue #77: selecting the joined table's same-named column "
            + "returns the base table's value instead."
    )]
    public void SelectOfSameNamedColumnFromJoinedTableReturnsItsValues()
    {
        CollidingNameDatabase db = new CollidingNameDatabase();

        Query query = new Query(
            new FromExpression(CollidingNameDatabase.Needs.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                CollidingNameDatabase.Specialties.Entity,
                                CollidingNameDatabase.Specialties.Id
                            )
                        )
                    ),
                    "spec_pk"
                ),
            ],
            where: null,
            [NeedsToSpecialtiesJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. NeedRowsJoinedToSpecialties(db)
                .Select(pair => pair.Specialty.SpecialtyId.ToString())
                .OrderBy(id => id),
        ];

        string?[] actual = [.. result.Column("spec_pk").OrderBy(id => id)];

        Assert.Equal(expected, actual);
    }

    [Fact(
        Skip = "Issue #77: grouping by the joined table's same-named column "
            + "groups by the base table's value instead."
    )]
    public void GroupByOfSameNamedColumnFromJoinedTableGroupsByItsValues()
    {
        CollidingNameDatabase db = new CollidingNameDatabase();

        Query query = new Query(
            new FromExpression(CollidingNameDatabase.Needs.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                CollidingNameDatabase.Specialties.Entity,
                                CollidingNameDatabase.Specialties.Id
                            )
                        )
                    ),
                    "spec_pk"
                ),
                new SelectExpression(
                    new SingleValueReturning(
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            CollidingNameDatabase.Needs.Entity,
                                            CollidingNameDatabase
                                                .Needs
                                                .PlannedHours
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "total_hours"
                ),
            ],
            where: null,
            [NeedsToSpecialtiesJoin()],
            [
                new Field(
                    new UuidField(
                        CollidingNameDatabase.Specialties.Entity,
                        CollidingNameDatabase.Specialties.Id
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

        (string? SpecialtyId, double TotalHours)[] expected =
        [
            .. NeedRowsJoinedToSpecialties(db)
                .GroupBy(pair => pair.Specialty.SpecialtyId)
                .Select(group =>
                    (
                        group.Key.ToString(),
                        group.Sum(pair => pair.Need.NeedPlannedHours)
                    )
                )
                .OrderBy(group => group.Item1),
        ];

        (string? SpecialtyId, double? TotalHours)[] actual =
        [
            .. result
                .Rows.Select(row => (row["spec_pk"], row.Double("total_hours")))
                .OrderBy(row => row.Item1),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(
            expected,
            actual.Select(row => (row.SpecialtyId, row.TotalHours ?? 0)).ToArray()
        );
    }

    private static IEnumerable<(NeedRow Need, SpecialtyRow Specialty)>
        NeedRowsJoinedToSpecialties(CollidingNameDatabase db)
    {
        return
            from need in db.NeedRows
            join specialty in db.SpecialtyRows
                on need.NeedSpecialtyId equals specialty.SpecialtyId
            select (need, specialty);
    }
}
