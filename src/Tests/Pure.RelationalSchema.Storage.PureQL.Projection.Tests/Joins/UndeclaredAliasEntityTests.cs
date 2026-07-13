using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

#pragma warning disable xUnit1004 // skipped: reproduces a known translator bug

// The spec gives joinItem no alias: joined tables must be referenced by
// their full "schema.table" entity string, and only the root from supports
// an alias. A field reference whose entity matches neither the from
// entity/alias nor any join entity is unresolvable and must fail fast
// instead of silently degrading to bare-name resolution (issue #82). The
// passing tests pin the spec-legal from-alias references.
[Trait("Clause", "Join")]
[Trait("Feature", "AliasResolution")]
public sealed class UndeclaredAliasEntityTests
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
        Skip = "Issue #82: a join-on field reference via an undeclared join "
            + "alias silently binds to the base table's same-named column, "
            + "so the equi-join key never matches and the join returns an "
            + "empty result instead of failing fast."
    )]
    [Trait("Status", "KnownGap")]
    public void JoinOnConditionViaUndeclaredAliasFailsFast()
    {
        CollidingNameDatabase db = new CollidingNameDatabase();

        Query query = new Query(
            new FromExpression(CollidingNameDatabase.Needs.Entity, "need"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                CollidingNameDatabase.Needs.Entity,
                                CollidingNameDatabase.Needs.Id
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
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
                                        "sp",
                                        CollidingNameDatabase.Specialties.Id
                                    )
                                )
                            )
                        )
                    )
                ),
            ],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        _ = Assert.Throws<NotSupportedException>(() => new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        ));
    }

    [Fact(
        Skip = "Issue #82: selecting a colliding column via an undeclared "
            + "join alias silently returns the base table's values instead "
            + "of failing fast."
    )]
    [Trait("Status", "KnownGap")]
    public void SelectOfCollidingColumnViaUndeclaredAliasFailsFast()
    {
        CollidingNameDatabase db = new CollidingNameDatabase();

        Query query = new Query(
            new FromExpression(CollidingNameDatabase.Needs.Entity, "need"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField("sp", CollidingNameDatabase.Specialties.Id)
                        )
                    ),
                    "specId"
                ),
            ],
            where: null,
            [NeedsToSpecialtiesJoin()],
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        _ = Assert.Throws<NotSupportedException>(() => new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        ));
    }

    [Fact]
    public void FromAliasFieldReferenceResolvesWithoutJoins()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity, "u"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField("u", SampleDatabase.Users.Name)
                        )
                    )
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string[] expected =
        [
            .. db.UserRows.Select(user => user.UserName).OrderBy(name => name),
        ];

        Assert.Equal(
            expected,
            result.Column(SampleDatabase.Users.Name).OrderBy(name => name).ToArray()
        );
    }

    [Fact]
    public void FromAliasReferenceToCollidingColumnResolvesToTheBaseTable()
    {
        CollidingNameDatabase db = new CollidingNameDatabase();

        Query query = new Query(
            new FromExpression(CollidingNameDatabase.Needs.Entity, "need"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField("need", CollidingNameDatabase.Needs.Id)
                        )
                    ),
                    "ownId"
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

        Guid[] expected =
        [
            .. db.NeedRows.Select(need => need.NeedId).OrderBy(id => id),
        ];

        Guid[] actual =
        [
            .. result.Rows
                .Select(row => row.Uuid("ownId")!.Value)
                .OrderBy(id => id),
        ];

        Assert.Equal(expected, actual);
    }
}
