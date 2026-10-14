using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.GroupExpressions;
using PureQL.CSharp.Model.GroupKeys;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.SelectItems;

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
                new PureQLQuery(
                    new MainPlainQuery(
                        new From(new FromEntity("t.nobody")),
                        [
                            new SelectItemProjection(
                                new SelectItemProjectionNonNullable(
                                    new SelectItemProjectionString(
                                        "name",
                                        new StringProjection(
                                            new FieldString("t.nobody", "name")
                                        )
                                    )
                                )
                            ),
                        ]
                    )
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
                new PureQLQuery(
                    new MainPlainQuery(
                        new From(new FromEntity("t.people", "p")),
                        [
                            new SelectItemProjection(
                                new SelectItemProjectionNonNullable(
                                    new SelectItemProjectionString(
                                        "name",
                                        new StringProjection(
                                            new FieldString("t.people", "name")
                                        )
                                    )
                                )
                            ),
                        ]
                    )
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
                new PureQLQuery(
                    new MainPlainQuery(
                        new From(new FromEntity("t.people")),
                        [
                            new SelectItemProjection(
                                new SelectItemProjectionNonNullable(
                                    new SelectItemProjectionString(
                                        "email",
                                        new StringProjection(
                                            new FieldString("t.people", "email")
                                        )
                                    )
                                )
                            ),
                        ]
                    )
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
                new PureQLQuery(
                    new MainPlainQuery(
                        new From(new FromEntity("t.people")),
                        [
                            new SelectItemProjection(
                                new SelectItemProjectionNonNullable(
                                    new SelectItemProjectionInteger(
                                        "name",
                                        new IntegerProjection(
                                            new FieldInteger("t.people", "name")
                                        )
                                    )
                                )
                            ),
                        ]
                    )
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
                new PureQLQuery(
                    new MainPlainQuery(
                        new From(new FromEntity("t.people")),
                        [
                            new SelectItemProjection(
                                new SelectItemProjectionNonNullable(
                                    new SelectItemProjectionString(
                                        "hash",
                                        new StringProjection(
                                            new FieldString("t.people", "hash")
                                        )
                                    )
                                )
                            ),
                        ]
                    )
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
                new PureQLQuery(
                    new MainPlainQuery(
                        new From(new FromEntity("t.people", "people")),
                        [
                            new SelectItemProjection(
                                new SelectItemProjectionNonNullable(
                                    new SelectItemProjectionString(
                                        "name",
                                        new StringProjection(
                                            new FieldString(source, "name")
                                        )
                                    )
                                )
                            ),
                        ],
                        subqueries: null,
                        joins:
                        [
                            new Join(
                                new JoinEntity(
                                    type,
                                    "t.people",
                                    new BooleanRow(
                                        new ComparisonRow(
                                            new EqualRow(
                                                new EqualUuidRow(
                                                    new UuidNullableRow(
                                                        new ExactUuidNullableRow(
                                                            new FieldUuidNullable(
                                                                "people",
                                                                "boss"
                                                            )
                                                        )
                                                    ),
                                                    new UuidNullableRow(
                                                        new UuidRow(
                                                            new FieldUuid("boss", "id")
                                                        )
                                                    )
                                                )
                                            )
                                        )
                                    ),
                                    "boss"
                                )
                            ),
                        ],
                        where: null,
                        orderBy: null,
                        pagination: null,
                        distinctBy: null
                    )
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
                new PureQLQuery(
                    new MainPlainQuery(
                        new From(new FromEntity("t.people", "a")),
                        [
                            new SelectItemProjection(
                                new SelectItemProjectionNonNullable(
                                    new SelectItemProjectionString(
                                        "name",
                                        new StringProjection(new FieldString("a", "name"))
                                    )
                                )
                            ),
                        ],
                        subqueries: null,
                        joins:
                        [
                            new Join(
                                new JoinEntity(
                                    JoinKind.Inner,
                                    "t.people",
                                    new BooleanRow(new FieldBoolean("c", "name")),
                                    "b"
                                )
                            ),
                            new Join(
                                new JoinEntity(
                                    JoinKind.Inner,
                                    "t.people",
                                    new BooleanRow(new LiteralBoolean(true)),
                                    "c"
                                )
                            ),
                        ],
                        where: null,
                        orderBy: null,
                        pagination: null,
                        distinctBy: null
                    )
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
                new PureQLQuery(
                    new MainPlainQuery(
                        new From(new FromEntity("t.people")),
                        [
                            new SelectItemProjection(
                                new SelectItemProjectionNonNullable(
                                    new SelectItemProjectionString(
                                        "name",
                                        new StringProjection(
                                            new FieldString("t.people", "name")
                                        )
                                    )
                                )
                            ),
                        ],
                        subqueries: null,
                        joins:
                        [
                            new Join(
                                new JoinEntity(
                                    JoinKind.Inner,
                                    "t.people",
                                    new BooleanRow(new LiteralBoolean(true))
                                )
                            ),
                        ],
                        where: null,
                        orderBy: null,
                        pagination: null,
                        distinctBy: null
                    )
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
                new PureQLQuery(
                    new MainPlainQuery(
                        new From(new FromEntity("t.people")),
                        [
                            new SelectItemProjection(
                                new SelectItemProjectionNonNullable(
                                    new SelectItemProjectionString(
                                        "name",
                                        new StringProjection(
                                            new FieldString("t.people", "name")
                                        )
                                    )
                                )
                            ),
                            new SelectItemProjection(
                                new SelectItemProjectionNonNullable(
                                    new SelectItemProjectionUuid(
                                        "name",
                                        new UuidProjection(new FieldUuid("t.people", "id"))
                                    )
                                )
                            ),
                        ]
                    )
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
                new PureQLQuery(
                    new MainPlainQuery(
                        new From(new FromEntity("t.people")),
                        [
                            new SelectItemProjection(
                                new SelectItemProjectionNonNullable(
                                    new SelectItemProjectionString(
                                        "name",
                                        new StringProjection(
                                            new FieldString("t.people", "name")
                                        )
                                    )
                                )
                            ),
                        ],
                        subqueries:
                        [
                            new Subquery(
                                "ids",
                                new Query(
                                    new PlainQuery(
                                        new From(new FromEntity("t.people")),
                                        [
                                            new SelectItemProjection(
                                                new SelectItemProjectionNonNullable(
                                                    new SelectItemProjectionUuid(
                                                        "id",
                                                        new UuidProjection(
                                                            new FieldUuid("t.people", "id")
                                                        )
                                                    )
                                                )
                                            ),
                                        ]
                                    )
                                )
                            ),
                            new Subquery(
                                "ids",
                                new Query(
                                    new PlainQuery(
                                        new From(new FromEntity("t.people")),
                                        [
                                            new SelectItemProjection(
                                                new SelectItemProjectionNonNullable(
                                                    new SelectItemProjectionUuid(
                                                        "id",
                                                        new UuidProjection(
                                                            new FieldUuid("t.people", "id")
                                                        )
                                                    )
                                                )
                                            ),
                                        ]
                                    )
                                )
                            ),
                        ],
                        joins: null,
                        where: null,
                        orderBy: null,
                        pagination: null,
                        distinctBy: null
                    )
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
                new PureQLQuery(
                    new MainPlainQuery(
                        new From(new FromSubquery("first")),
                        [
                            new SelectItemProjection(
                                new SelectItemProjectionNonNullable(
                                    new SelectItemProjectionUuid(
                                        "id",
                                        new UuidProjection(new FieldUuid("first", "id"))
                                    )
                                )
                            ),
                        ],
                        subqueries:
                        [
                            new Subquery(
                                "first",
                                new Query(
                                    new PlainQuery(
                                        new From(new FromSubquery("second")),
                                        [
                                            new SelectItemProjection(
                                                new SelectItemProjectionNonNullable(
                                                    new SelectItemProjectionUuid(
                                                        "id",
                                                        new UuidProjection(
                                                            new FieldUuid("second", "id")
                                                        )
                                                    )
                                                )
                                            ),
                                        ]
                                    )
                                )
                            ),
                            new Subquery(
                                "second",
                                new Query(
                                    new PlainQuery(
                                        new From(new FromEntity("t.people")),
                                        [
                                            new SelectItemProjection(
                                                new SelectItemProjectionNonNullable(
                                                    new SelectItemProjectionUuid(
                                                        "id",
                                                        new UuidProjection(
                                                            new FieldUuid("t.people", "id")
                                                        )
                                                    )
                                                )
                                            ),
                                        ]
                                    )
                                )
                            ),
                        ],
                        joins: null,
                        where: null,
                        orderBy: null,
                        pagination: null,
                        distinctBy: null
                    )
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
                new PureQLQuery(
                    new MainPlainQuery(
                        new From(new FromSubquery("ids")),
                        [
                            new SelectItemProjection(
                                new SelectItemProjectionNullable(
                                    new SelectItemProjectionUuidNullable(
                                        "id",
                                        new ExactUuidNullableProjection(
                                            new FieldUuidNullable("ids", "id")
                                        )
                                    )
                                )
                            ),
                        ],
                        subqueries:
                        [
                            new Subquery(
                                "ids",
                                new Query(
                                    new PlainQuery(
                                        new From(new FromEntity("t.people")),
                                        [
                                            new SelectItemProjection(
                                                new SelectItemProjectionNonNullable(
                                                    new SelectItemProjectionUuid(
                                                        "id",
                                                        new UuidProjection(
                                                            new FieldUuid("t.people", "id")
                                                        )
                                                    )
                                                )
                                            ),
                                        ]
                                    )
                                )
                            ),
                        ],
                        joins: null,
                        where: null,
                        orderBy: null,
                        pagination: null,
                        distinctBy: null
                    )
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
                new PureQLQuery(
                    new MainPlainQuery(
                        new From(new FromEntity("t.people")),
                        [
                            new SelectItemProjection(
                                new SelectItemProjectionNonNullable(
                                    new SelectItemProjectionString(
                                        "name",
                                        new StringProjection(
                                            new FieldString("t.people", "name")
                                        )
                                    )
                                )
                            ),
                        ],
                        subqueries:
                        [
                            new Subquery(
                                "ids",
                                new Query(
                                    new PlainQuery(
                                        new From(new FromEntity("t.people")),
                                        [
                                            new SelectItemProjection(
                                                new SelectItemProjectionNonNullable(
                                                    new SelectItemProjectionUuid(
                                                        "id",
                                                        new UuidProjection(
                                                            new FieldUuid("t.people", "id")
                                                        )
                                                    )
                                                )
                                            ),
                                        ]
                                    )
                                )
                            ),
                        ],
                        joins: null,
                        where: new BooleanRow(
                            new ComparisonRow(
                                new InRow(
                                    new InUuidRow(
                                        new UuidNullableRow(
                                            new ExactUuidNullableRow(
                                                new FieldUuidNullable("t.people", "boss")
                                            )
                                        ),
                                        new ListUuid(
                                            new ListSubqueryColumnUuid(
                                                subquery,
                                                field,
                                                nullable
                                            )
                                        )
                                    )
                                )
                            )
                        ),
                        orderBy: null,
                        pagination: null,
                        distinctBy: null
                    )
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
                new PureQLQuery(
                    new MainGroupedQuery(
                        new From(new FromEntity("t.people")),
                        [
                            new GroupKey(
                                new GroupKeyNonNullable(
                                    new GroupKeyString(
                                        "name",
                                        new StringRow(new FieldString("t.people", "name"))
                                    )
                                )
                            ),
                        ],
                        [
                            nullable
                                ? new SelectItemGroup(
                                    new SelectItemGroupNullable(
                                        new SelectItemGroupStringNullable(
                                            "name",
                                            new ExactStringNullableGroup(
                                                new KeyStringNullable(key)
                                            )
                                        )
                                    )
                                )
                                : new SelectItemGroup(
                                    new SelectItemGroupNonNullable(
                                        new SelectItemGroupString(
                                            "name",
                                            new StringGroup(new KeyString(key))
                                        )
                                    )
                                ),
                        ]
                    )
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
                new PureQLQuery(
                    new MainGroupedQuery(
                        new From(new FromEntity("t.people")),
                        [
                            new GroupKey(
                                new GroupKeyNonNullable(
                                    new GroupKeyString(
                                        "key",
                                        new StringRow(new FieldString("t.people", "name"))
                                    )
                                )
                            ),
                            new GroupKey(
                                new GroupKeyNonNullable(
                                    new GroupKeyUuid(
                                        "key",
                                        new UuidRow(new FieldUuid("t.people", "id"))
                                    )
                                )
                            ),
                        ],
                        [
                            new SelectItemGroup(
                                new SelectItemGroupNonNullable(
                                    new SelectItemGroupString(
                                        "name",
                                        new StringGroup(new KeyString("key"))
                                    )
                                )
                            ),
                        ]
                    )
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
                new PureQLQuery(
                    new MainPlainQuery(
                        new From(new FromEntity("t.people")),
                        [
                            new SelectItemProjection(
                                new SelectItemProjectionNonNullable(
                                    new SelectItemProjectionString(
                                        "name",
                                        new StringProjection(
                                            new FieldString("t.people", "name")
                                        )
                                    )
                                )
                            ),
                        ],
                        subqueries: null,
                        joins: null,
                        where: null,
                        orderBy: null,
                        pagination: null,
                        distinctBy: ["id"]
                    )
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
                new PureQLQuery(
                    new MainPlainQuery(
                        new From(new FromEntity("t.people")),
                        [
                            new SelectItemProjection(
                                new SelectItemProjectionNonNullable(
                                    new SelectItemProjectionString(
                                        "name",
                                        new StringProjection(
                                            new FieldString("t.people", "name")
                                        )
                                    )
                                )
                            ),
                        ],
                        subqueries: null,
                        joins: null,
                        where: null,
                        orderBy: null,
                        pagination: new Pagination(skip, take),
                        distinctBy: null
                    )
                )
            )
        );
    }
}
