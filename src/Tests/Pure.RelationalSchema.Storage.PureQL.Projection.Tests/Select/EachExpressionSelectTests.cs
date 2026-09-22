using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachArithmetics;
using PureQL.CSharp.Model.Fields;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Select;

// A bare each* (per-row array-returning) expression has no defined result
// when projected directly: it must be folded by an aggregate first (see
// AggregateOverPerRowArithmeticTests). Selecting it unwrapped is a known
// execution gap (CLAUDE.md: "computed select columns") and must fail fast
// with NotSupportedException, with or without groupBy, rather than crash on
// an internal OneOf type mismatch (issue #134).
[Trait("Clause", "Select")]
[Trait("Feature", "EachExpressionProjection")]
public sealed class EachExpressionSelectTests
{
    [Fact]
    public void BareEachMultiplyInSelectWithoutGroupByFailsFast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.users"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new EachArithmetic(
                                new EachMultiply(
                                    [
                                        new NumberArrayReturning(
                                            new NumberField(
                                                "schema_with_foreign_keys.users",
                                                "user_age"
                                            )
                                        ),
                                        new NumberArrayReturning(
                                            new NumberField(
                                                "schema_with_foreign_keys.users",
                                                "user_precision_value"
                                            )
                                        ),
                                    ]
                                )
                            )
                        )
                    ),
                    "product"
                ),
            ]
        );

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(datasets, query).ToList()
        );
    }

    [Fact]
    public void BareEachSubtractInGroupBySelectFailsFast()
    {
        IEnumerable<IStoredSchemaDataSet> datasets =
            [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()];

        Query query = new Query(
            new FromExpression("schema_with_foreign_keys.orders"),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                "schema_with_foreign_keys.orders",
                                "order_status"
                            )
                        )
                    )
                ),
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new EachArithmetic(
                                new EachSubtract(
                                    [
                                        new NumberArrayReturning(
                                            new NumberField(
                                                "schema_with_foreign_keys.orders",
                                                "order_total"
                                            )
                                        ),
                                        new NumberArrayReturning(
                                            new NumberField(
                                                "schema_with_foreign_keys.orders",
                                                "order_total"
                                            )
                                        ),
                                    ]
                                )
                            )
                        )
                    ),
                    "diff"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new StringField(
                        "schema_with_foreign_keys.orders",
                        "order_status"
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(datasets, query).ToList()
        );
    }
}
