using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates.Numeric;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Comparisons;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Errors;

// Part of the #72 roadmap (issue #144): EntityReferenceValidator rejects a
// query that references an entity/field not resolvable from the FROM/JOIN
// scope. NegativePathTests already pins the entity-lookup failure against the
// supplied datasets (a from/join entity that is syntactically consistent but
// absent from the datasets) and the column-lookup failure on the resolved
// table (a bad field name on a known entity). This suite instead pins
// EntityReferenceValidator's own contract: a field reference whose *entity*
// string matches none of the from entity, the from alias, or a join entity is
// rejected with NotSupportedException before any row is ever read, for every
// clause that can carry a field reference. It also exercises the aggregate
// argument recursion (Entities(NumberReturning/DateReturning/...)) that walks
// into sum/max/min/average arguments, both for an unknown entity (validator
// throw) and an unknown field on a known entity (downstream evaluator throw).
[Trait("Clause", "Errors")]
[Trait("Feature", "EntityReferenceValidation")]
public sealed class EntityReferenceValidationTests
{
    private const string UnknownEntity = "shop.nonexistent_entity";

    // ===== SELECT =====

    // Exercises Referenced(query)'s SelectExpressions branch: the select
    // field's entity ("shop.nonexistent_entity") matches neither the from
    // entity (shop.users) nor any join entity (there is none here), so the
    // FirstOrDefault(unknown) check fires and Validate throws before the
    // table lookup or any row is read.
    [Fact]
    public void SelectUnknownEntityFailsFast()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(UnknownEntity, "whatever")
                        )
                    )
                ),
            ]
        );

        NotSupportedException exception = Assert.Throws<NotSupportedException>(
            () => new PureQLProjection(db.Datasets, query)
        );

        Assert.Contains(UnknownEntity, exception.Message, System.StringComparison.Ordinal);
    }

    // ===== WHERE =====

    // Exercises Referenced(query)'s Where branch through the per-row
    // (BooleanArrayReturning) each* family: Entities(EachEquality) ->
    // Entities(EachStringEquality) recurses into the left operand's field,
    // whose entity is unresolvable.
    [Fact]
    public void WhereEachFieldUnknownEntityFailsFast()
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
            ],
            new BooleanArrayReturning(
                new EachEquality(
                    new EachStringEquality(
                        new StringArrayReturning(
                            new StringField(UnknownEntity, "whatever")
                        ),
                        new StringReturning(new StringScalar("shipped"))
                    )
                )
            ),
            join: null,
            groupBy: null,
            having: null,
            orderBy: null,
            pagination: null
        );

        NotSupportedException exception = Assert.Throws<NotSupportedException>(
            () => new PureQLProjection(db.Datasets, query)
        );

        Assert.Contains(UnknownEntity, exception.Message, System.StringComparison.Ordinal);
    }

    // ===== JOIN on =====

    // Exercises Referenced(query)'s Join branch: Entities(join.On) recurses
    // into the ON condition's fields. shop.products is a real table in the
    // supplied datasets, but it is neither the from entity (shop.users) nor
    // the join entity (shop.orders) of *this* query, so referencing it from
    // ON is unresolvable - joins have no per-join alias, so an ON condition
    // may only reference the two entities actually named by FROM/JOIN.
    [Fact]
    public void JoinOnEntityNeitherBaseNorJoinedFailsFast()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Id
                            )
                        )
                    )
                ),
            ],
            where: null,
            [
                new Join(
                    JoinType.Inner,
                    SampleDatabase.Orders.Entity,
                    new BooleanArrayReturning(
                        new EachEquality(
                            new EachUuidEquality(
                                new UuidArrayReturning(
                                    new UuidField(
                                        SampleDatabase.Users.Entity,
                                        SampleDatabase.Users.Id
                                    )
                                ),
                                new UuidArrayReturning(
                                    new UuidField(
                                        SampleDatabase.Products.Entity,
                                        SampleDatabase.Products.Id
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

        NotSupportedException exception = Assert.Throws<NotSupportedException>(
            () => new PureQLProjection(db.Datasets, query)
        );

        Assert.Contains(
            SampleDatabase.Products.Entity,
            exception.Message,
            System.StringComparison.Ordinal
        );
    }

    // ===== GROUP BY =====

    // Exercises Referenced(query)'s GroupBy branch: Entities(Field) extracts
    // the group key field's entity directly, which is unresolvable here.
    [Fact]
    public void GroupByUnknownEntityFailsFast()
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
                    ),
                    "status"
                ),
            ],
            where: null,
            join: null,
            [
                new Field(new StringField(UnknownEntity, "whatever")),
            ],
            having: null,
            orderBy: null,
            pagination: null
        );

        NotSupportedException exception = Assert.Throws<NotSupportedException>(
            () => new PureQLProjection(db.Datasets, query)
        );

        Assert.Contains(UnknownEntity, exception.Message, System.StringComparison.Ordinal);
    }

    // ===== HAVING aggregate argument =====

    // Exercises the uncovered aggregate-argument recursion: Entities(Having)
    // -> Entities(BooleanReturning) -> Entities(Comparison) ->
    // Entities(NumberReturning) -> the NumberAggregate arm ->
    // Entities(sum.Argument), which walks into the sum's NumberField and
    // finds an unresolvable entity - all before any group is ever folded.
    [Fact]
    public void HavingAggregateArgumentUnknownEntityFailsFast()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.UserId
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new UuidField(
                        SampleDatabase.Orders.Entity,
                        SampleDatabase.Orders.UserId
                    )
                ),
            ],
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThan,
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(UnknownEntity, "whatever")
                                    )
                                )
                            )
                        ),
                        new NumberReturning(new NumberScalar(0))
                    )
                )
            ),
            orderBy: null,
            pagination: null
        );

        NotSupportedException exception = Assert.Throws<NotSupportedException>(
            () => new PureQLProjection(db.Datasets, query)
        );

        Assert.Contains(UnknownEntity, exception.Message, System.StringComparison.Ordinal);
    }

    // ===== ORDER BY =====

    // Exercises Referenced(query)'s OrderBy branch: Entities(item.Field)
    // extracts the order-by field's entity, which is unresolvable here.
    // Complements OrderByFieldResolutionErrorTests, which pins the separate
    // alias-vs-source-column naming rule for a *resolvable* entity.
    [Fact]
    public void OrderByUnknownEntityFailsFast()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Users.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new StringArrayReturning(
                            new StringField(
                                SampleDatabase.Users.Entity,
                                SampleDatabase.Users.Name
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            groupBy: null,
            having: null,
            [
                new OrderByItem(
                    new Field(new StringField(UnknownEntity, "whatever")),
                    SortDirection.Asc
                ),
            ],
            pagination: null
        );

        NotSupportedException exception = Assert.Throws<NotSupportedException>(
            () => new PureQLProjection(db.Datasets, query)
        );

        Assert.Contains(UnknownEntity, exception.Message, System.StringComparison.Ordinal);
    }

    // ===== Aggregate over an unknown field on a known entity =====

    // Unlike the cases above, shop.orders here *is* a known entity, so
    // EntityReferenceValidator's aggregate-argument recursion walks through
    // it successfully (the field-level branch the issue calls out) and lets
    // the query construct. The unresolvable part is the field name itself
    // ("not_a_column"), which only the group-evaluation path
    // (AggregateEvaluator -> WhereExpressionBuilder.BuildNumberSelector ->
    // CellValueExtractor.GetRequiredCell) can detect, once rows are actually
    // folded per group - so this still fails fast, just one layer later than
    // the entity-level cases, and with the column-lookup exception type
    // (KeyNotFoundException) instead of NotSupportedException.
    [Fact]
    public void HavingAggregateOverUnknownFieldOnKnownEntityFailsFast()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new ArrayReturning(
                        new UuidArrayReturning(
                            new UuidField(
                                SampleDatabase.Orders.Entity,
                                SampleDatabase.Orders.UserId
                            )
                        )
                    )
                ),
            ],
            where: null,
            join: null,
            [
                new Field(
                    new UuidField(
                        SampleDatabase.Orders.Entity,
                        SampleDatabase.Orders.UserId
                    )
                ),
            ],
            new BooleanReturning(
                new Comparison(
                    new NumberComparison(
                        ComparisonOperator.GreaterThan,
                        new NumberReturning(
                            new NumberAggregate(
                                new SumNumber(
                                    new NumberArrayReturning(
                                        new NumberField(
                                            SampleDatabase.Orders.Entity,
                                            "not_a_column"
                                        )
                                    )
                                )
                            )
                        ),
                        new NumberReturning(new NumberScalar(0))
                    )
                )
            ),
            orderBy: null,
            pagination: null
        );

        KeyNotFoundException exception = Assert.Throws<KeyNotFoundException>(
            () => new ProjectionResult(new PureQLProjection(db.Datasets, query))
        );

        Assert.Contains("not_a_column", exception.Message, System.StringComparison.Ordinal);
    }
}
