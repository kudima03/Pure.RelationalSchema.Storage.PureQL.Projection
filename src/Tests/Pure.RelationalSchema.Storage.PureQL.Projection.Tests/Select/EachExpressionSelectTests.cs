using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
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
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new NumberArrayReturning(
                            new EachArithmetic(
                                new EachMultiply(
                                    [
                                        new NumberArrayReturning(
                                            new NumberField(
                                                SampleDatabase.Users.Entity,
                                                SampleDatabase.Users.Age
                                            )
                                        ),
                                        new NumberArrayReturning(
                                            new NumberField(
                                                SampleDatabase.Users.Entity,
                                                SampleDatabase.Users.PrecisionValue
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
            new PureQLProjection(db.Datasets, query).ToList()
        );
    }

    [Fact]
    public void BareEachSubtractInGroupBySelectFailsFast()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.Status
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
                                                SampleDatabase.Orders.Entity,
                                                SampleDatabase.Orders.Total
                                            )
                                        ),
                                        new NumberArrayReturning(
                                            new NumberField(
                                                SampleDatabase.Orders.Entity,
                                                SampleDatabase.Orders.Total
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
                        SampleDatabase.Orders.Entity,
                        SampleDatabase.Orders.Status
                    )
                ),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        _ = Assert.Throws<NotSupportedException>(() =>
            new PureQLProjection(db.Datasets, query).ToList()
        );
    }
}
