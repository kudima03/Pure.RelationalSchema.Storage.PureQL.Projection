using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.RowExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Errors;

// The checks the specification leaves to the interpreter: they need the
// catalog or names, and a query failing one is rejected when the projection
// is built, before any row is read.
public sealed class ValidationTests
{
    private static readonly IStoredSchemaDataSet People = new InlineTable(
        "t",
        "people",
        ("id", new UuidColumnType()),
        ("name", new StringColumnType()),
        ("boss", new UuidColumnType()),
        ("hash", new DeterminedHashColumnType())
    ).With(
        ["00000000-0000-0000-0000-000000000001", "Ann", "", "x"],
        [
            "00000000-0000-0000-0000-000000000002",
            "Bob",
            "00000000-0000-0000-0000-000000000001",
            "y",
        ]
    );

    [Fact]
    public void UnknownEntityIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [People],
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.nobody"),
                    select:
                    [
                        new SelectItemProjection(
                            "name",
                            new FieldString("t.nobody", "name")
                        ),
                    ]
                )
            )
        );
    }

    [Fact]
    public void AliasedSourceCannotBeReadByItsEntityName()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [People],
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.people", "p"),
                    select:
                    [
                        new SelectItemProjection(
                            "name",
                            new FieldString("t.people", "name")
                        ),
                    ]
                )
            )
        );
    }

    [Fact]
    public void UnknownFieldIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [People],
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.people"),
                    select:
                    [
                        new SelectItemProjection(
                            "email",
                            new FieldString("t.people", "email")
                        ),
                    ]
                )
            )
        );
    }

    [Fact]
    public void FieldDeclaredWithAnotherTypeThanItsColumnIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [People],
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.people"),
                    select:
                    [
                        new SelectItemProjection(
                            "name",
                            new FieldInteger("t.people", "name")
                        ),
                    ]
                )
            )
        );
    }

    [Fact]
    public void FieldOfAColumnTypePureQLLacksIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [People],
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.people"),
                    select:
                    [
                        new SelectItemProjection(
                            "hash",
                            new FieldString("t.people", "hash")
                        ),
                    ]
                )
            )
        );
    }

    [Theory]
    [InlineData(JoinKind.Left, "boss")]
    [InlineData(JoinKind.Right, "people")]
    [InlineData(JoinKind.Full, "people")]
    public void NonNullFieldOfAnOptionalJoinSideIsRejected(JoinKind type, string source)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [People],
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.people", "people"),
                    joins:
                    [
                        new JoinEntity(
                            type,
                            "t.people",
                            new EqualUuidRow(
                                new FieldUuidNullable("people", "boss"),
                                new FieldUuid("boss", "id")
                            ),
                            "boss"
                        ),
                    ],
                    select:
                    [
                        new SelectItemProjection("name", new FieldString(source, "name")),
                    ]
                )
            )
        );
    }

    [Fact]
    public void JoinConditionCannotReadALaterSource()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [People],
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.people", "a"),
                    joins:
                    [
                        new JoinEntity(
                            JoinKind.Inner,
                            "t.people",
                            new FieldBoolean("c", "name"),
                            "b"
                        ),
                        new JoinEntity(
                            JoinKind.Inner,
                            "t.people",
                            new LiteralBoolean(true),
                            "c"
                        ),
                    ],
                    select:
                    [
                        new SelectItemProjection("name", new FieldString("a", "name")),
                    ]
                )
            )
        );
    }

    [Fact]
    public void RepeatedSourceNameIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [People],
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.people"),
                    joins:
                    [
                        new JoinEntity(
                            JoinKind.Inner,
                            "t.people",
                            new LiteralBoolean(true)
                        ),
                    ],
                    select:
                    [
                        new SelectItemProjection(
                            "name",
                            new FieldString("t.people", "name")
                        ),
                    ]
                )
            )
        );
    }

    [Fact]
    public void RepeatedColumnAliasIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [People],
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.people"),
                    select:
                    [
                        new SelectItemProjection(
                            "name",
                            new FieldString("t.people", "name")
                        ),
                        new SelectItemProjection("name", new FieldUuid("t.people", "id")),
                    ]
                )
            )
        );
    }

    [Fact]
    public void RepeatedSubqueryNameIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [People],
                new MainPlainQueryWithoutPagination(
                    subqueries:
                    [
                        new SubqueryQuery(
                            "ids",
                            new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.people"),
                                select:
                                [
                                    new SelectItemProjection(
                                        "id",
                                        new FieldUuid("t.people", "id")
                                    ),
                                ]
                            )
                        ),
                        new SubqueryQuery(
                            "ids",
                            new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.people"),
                                select:
                                [
                                    new SelectItemProjection(
                                        "id",
                                        new FieldUuid("t.people", "id")
                                    ),
                                ]
                            )
                        ),
                    ],
                    from: new FromEntity("t.people"),
                    select:
                    [
                        new SelectItemProjection(
                            "name",
                            new FieldString("t.people", "name")
                        ),
                    ]
                )
            )
        );
    }

    [Fact]
    public void SubqueryCannotReadALaterSubquery()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [People],
                new MainPlainQueryWithoutPagination(
                    subqueries:
                    [
                        new SubqueryQuery(
                            "first",
                            new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromSubquery("second"),
                                select:
                                [
                                    new SelectItemProjection(
                                        "id",
                                        new FieldUuid("second", "id")
                                    ),
                                ]
                            )
                        ),
                        new SubqueryQuery(
                            "second",
                            new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.people"),
                                select:
                                [
                                    new SelectItemProjection(
                                        "id",
                                        new FieldUuid("t.people", "id")
                                    ),
                                ]
                            )
                        ),
                    ],
                    from: new FromSubquery("first"),
                    select: [new SelectItemProjection("id", new FieldUuid("first", "id"))]
                )
            )
        );
    }

    [Fact]
    public void SubqueryColumnReadWithAnotherNullabilityIsRejected()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [People],
                new MainPlainQueryWithoutPagination(
                    subqueries:
                    [
                        new SubqueryQuery(
                            "ids",
                            new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.people"),
                                select:
                                [
                                    new SelectItemProjection(
                                        "id",
                                        new FieldUuid("t.people", "id")
                                    ),
                                ]
                            )
                        ),
                    ],
                    from: new FromSubquery("ids"),
                    select:
                    [
                        new SelectItemProjection(
                            "id",
                            new FieldUuidNullable("ids", "id")
                        ),
                    ]
                )
            )
        );
    }

    [Theory]
    [InlineData("missing", "id", false)]
    [InlineData("ids", "missing", false)]
    [InlineData("ids", "id", true)]
    public void InOverAnUndeclaredOrMistypedSubqueryColumnIsRejected(
        string subquery,
        string field,
        bool nullable
    )
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [People],
                new MainPlainQueryWithoutPagination(
                    subqueries:
                    [
                        new SubqueryQuery(
                            "ids",
                            new PlainQueryWithoutPaginationWithoutOrderBy(
                                from: new FromEntity("t.people"),
                                select:
                                [
                                    new SelectItemProjection(
                                        "id",
                                        new FieldUuid("t.people", "id")
                                    ),
                                ]
                            )
                        ),
                    ],
                    from: new FromEntity("t.people"),
                    where: new InUuidRow(
                        new FieldUuidNullable("t.people", "boss"),
                        nullable
                            ? new ListSubqueryColumnUuidNullable(subquery, field)
                            : new ListSubqueryColumnUuid(subquery, field)
                    ),
                    select:
                    [
                        new SelectItemProjection(
                            "name",
                            new FieldString("t.people", "name")
                        ),
                    ]
                )
            )
        );
    }

    [Theory]
    [InlineData("other", false)]
    [InlineData("name", true)]
    public void GroupKeyReferenceMustMatchADeclaredKey(string key, bool nullable)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [People],
                new MainGroupedQueryWithoutPagination(
                    from: new FromEntity("t.people"),
                    groupBy: [new GroupKey("name", new FieldString("t.people", "name"))],
                    select:
                    [
                        nullable
                            ? new SelectItemGroup("name", new KeyStringNullable(key))
                            : new SelectItemGroup("name", new KeyString(key)),
                    ]
                )
            )
        );
    }

    [Fact]
    public void GroupKeyAliasesAreUnique()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [People],
                new MainGroupedQueryWithoutPagination(
                    from: new FromEntity("t.people"),
                    groupBy:
                    [
                        new GroupKey("key", new FieldString("t.people", "name")),
                        new GroupKey("key", new FieldUuid("t.people", "id")),
                    ],
                    select: [new SelectItemGroup("name", new KeyString("key"))]
                )
            )
        );
    }

    [Fact]
    public void DistinctByListsOnlySelectAliases()
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [People],
                new MainPlainQueryWithoutPagination(
                    from: new FromEntity("t.people"),
                    select:
                    [
                        new SelectItemProjection(
                            "name",
                            new FieldString("t.people", "name")
                        ),
                    ],
                    distinctBy: ["id"]
                )
            )
        );
    }

    [Theory]
    [InlineData(-1, 1)]
    [InlineData(0, 0)]
    public void PaginationOutsideItsRangeIsRejected(long skip, long take)
    {
        _ = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection(
                [People],
                new MainPlainQueryWithPagination(
                    from: new FromEntity("t.people"),
                    select:
                    [
                        new SelectItemProjection(
                            "name",
                            new FieldString("t.people", "name")
                        ),
                    ],
                    orderBy: [new OrderItemProjectionColumn("name")],
                    pagination: new Pagination(skip: skip, take: take)
                )
            )
        );
    }
}
