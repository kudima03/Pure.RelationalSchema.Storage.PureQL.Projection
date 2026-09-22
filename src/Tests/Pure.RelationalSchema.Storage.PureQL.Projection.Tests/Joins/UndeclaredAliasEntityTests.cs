using Pure.Primitives.String;
using Pure.Primitives.String.Operations;
using Pure.RelationalSchema.Samples.Columns;
using Pure.RelationalSchema.Samples.Schemas;
using Pure.RelationalSchema.Samples.Tables;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.Records;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Joins;

// The spec gives joinItem no alias: joined tables must be referenced by
// their full "schema.table" entity string, and only the root from supports
// an alias. A field reference whose entity matches neither the from
// entity/alias nor any join entity is unresolvable and must fail fast
// instead of silently degrading to bare-name resolution (issue #82). The
// passing tests pin the spec-legal from-alias references.
//
// The colliding-"id" shape needed here comes from the centralized
// Pure.RelationalSchema.Storage.Samples catalogue's
// SchemaDataSetWithAmbiguousIds (schema "schema_with_indexes"):
// table_with_indexes (id, tenant_id, name, created_at) references
// table_with_single_index (id, name) via tenant_id -> id - the same
// six-needs/four-specialties/one-unreferenced shape this repo's
// CollidingNameDatabase used to hand-build, just under package names.
[Trait("Clause", "Join")]
[Trait("Feature", "AliasResolution")]
public sealed class UndeclaredAliasEntityTests
{
    private static Join NeedsToSpecialtiesJoin()
    {
        return new Join(
            JoinType.Inner,
            new JoinedString(
                new DotString(),
                [new RelationalSchemaWithIndexes().Name, new TableWithSingleIndex().Name]
            ).TextValue,
            new BooleanArrayReturning(
                new EachEquality(
                    new EachUuidEquality(
                        new UuidArrayReturning(
                            new UuidField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithIndexes().Name,
                                        new TableWithIndexes().Name,
                                    ]
                                ).TextValue,
                                new TenantIdColumn().Name.TextValue
                            )
                        ),
                        new UuidArrayReturning(
                            new UuidField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithIndexes().Name,
                                        new TableWithSingleIndex().Name,
                                    ]
                                ).TextValue,
                                new IdColumn().Name.TextValue
                            )
                        )
                    )
                )
            )
        );
    }

    [Fact]
    public void JoinOnConditionViaUndeclaredAliasFailsFast()
    {
        IStoredSchemaDataSet dataset = new SchemaDataSetWithAmbiguousIds();

        Query query = new Query(
            new FromExpression(
                new JoinedString(
                    new DotString(),
                    [new RelationalSchemaWithIndexes().Name, new TableWithIndexes().Name]
                ).TextValue,
                "need"
            ),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                new JoinedString(
                                    new DotString(),
                                    [
                                        new RelationalSchemaWithIndexes().Name,
                                        new TableWithIndexes().Name,
                                    ]
                                ).TextValue,
                                new IdColumn().Name.TextValue
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Inner,
                    new JoinedString(
                        new DotString(),
                        [
                            new RelationalSchemaWithIndexes().Name,
                            new TableWithSingleIndex().Name,
                        ]
                    ).TextValue,
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        new JoinedString(
                                            new DotString(),
                                            [
                                                new RelationalSchemaWithIndexes().Name,
                                                new TableWithIndexes().Name,
                                            ]
                                        ).TextValue,
                                        new TenantIdColumn().Name.TextValue
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField("sp", new IdColumn().Name.TextValue)
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
            new PureQLProjection([dataset], query)
        ));
    }

    [Fact]
    public void SelectOfCollidingColumnViaUndeclaredAliasFailsFast()
    {
        IStoredSchemaDataSet dataset = new SchemaDataSetWithAmbiguousIds();

        Query query = new Query(
            new FromExpression(
                new JoinedString(
                    new DotString(),
                    [new RelationalSchemaWithIndexes().Name, new TableWithIndexes().Name]
                ).TextValue,
                "need"
            ),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField("sp", new IdColumn().Name.TextValue)
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
            new PureQLProjection([dataset], query)
        ));
    }

    [Fact]
    public void FromAliasFieldReferenceResolvesWithoutJoins()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];
        IReadOnlyList<UserRecord> userRows = [.. new UserRecords()];

        Query query = new Query(
            new FromExpression(
                new JoinedString(
                    new DotString(),
                    [new RelationalSchemaWithForeignKeys().Name, new UsersTable().Name]
                ).TextValue,
                "u"
            ),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField("u", new UserNameColumn().Name.TextValue)
                        )
                    )
                ),
            ]
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(datasets, query)
        );

        string[] expected =
        [
            .. userRows.Select(user => user.UserName).OrderBy(name => name),
        ];

        Assert.Equal(
            expected,
            result.Column(new UserNameColumn().Name.TextValue)
                .OrderBy(name => name)
                .ToArray()
        );
    }

    [Fact]
    public void FromAliasReferenceToCollidingColumnResolvesToTheBaseTable()
    {
        IStoredSchemaDataSet dataset = new SchemaDataSetWithAmbiguousIds();
        IReadOnlyList<AmbiguousIdRecord> needRows = [.. new AmbiguousIdRecords()];

        Query query = new Query(
            new FromExpression(
                new JoinedString(
                    new DotString(),
                    [new RelationalSchemaWithIndexes().Name, new TableWithIndexes().Name]
                ).TextValue,
                "need"
            ),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField("need", new IdColumn().Name.TextValue)
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
            new PureQLProjection([dataset], query)
        );

        Guid[] expected = [.. needRows.Select(need => need.Id).OrderBy(id => id)];

        Guid[] actual =
        [
            .. result.Rows
                .Select(row => row.Uuid("ownId")!.Value)
                .OrderBy(id => id),
        ];

        Assert.Equal(expected, actual);
    }
}
