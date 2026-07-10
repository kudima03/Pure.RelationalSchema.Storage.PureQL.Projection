using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Aggregates.String;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Aggregates;

// String aggregates are limited to min / max (there is no string sum/avg). The
// translator does not yet evaluate aggregate projections, so these spec-correct
// tests are disabled and document the intended SQL behaviour.
#pragma warning disable xUnit1004 // skipped: documents a known translator gap
[Trait("Clause", "Aggregate")]
[Trait("Feature", "StringAggregate")]
[Trait("Status", "KnownGap")]
public sealed class StringAggregateTests
{
    [Fact(Skip = "KnownGap: aggregate projections raise NotSupportedException. "
        + "Enable once aggregate select expressions are implemented.")]
    public void MinStatusPerUserProjectsGroupMinimum()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new StringReturning(
                            new StringAggregate(
                                new MinString(
                                    new StringArrayReturning(
                                        new StringField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Status
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "min_status"
                ),
            ],
            where: null,
            join: null,
            [new Field(new UuidField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string?[] expected =
        [
            .. db.OrderRows.GroupBy(order => order.OrderUserId)
                .Select(group => group.Min(order => order.OrderStatus))
                .OrderBy(value => value, StringComparer.Ordinal),
        ];

        string?[] actual =
        [
            .. result.Column("min_status").OrderBy(v => v, StringComparer.Ordinal),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }

    [Fact(Skip = "KnownGap: aggregate projections raise NotSupportedException. "
        + "Enable once aggregate select expressions are implemented.")]
    public void MaxStatusPerUserProjectsGroupMaximum()
    {
        SampleDatabase db = new SampleDatabase();

        Query query = new Query(
            new FromExpression(SampleDatabase.Orders.Entity),
            [
                new SelectExpression(
                    new SingleValueReturning(
                        new StringReturning(
                            new StringAggregate(
                                new MaxString(
                                    new StringArrayReturning(
                                        new StringField(
                                            SampleDatabase.Orders.Entity,
                                            SampleDatabase.Orders.Status
                                        )
                                    )
                                )
                            )
                        )
                    ),
                    "max_status"
                ),
            ],
            where: null,
            join: null,
            [new Field(new UuidField(SampleDatabase.Orders.Entity, SampleDatabase.Orders.UserId))],
            having: null,
            orderBy: null,
            pagination: null
        );

        ProjectionResult result = new ProjectionResult(
            new PureQLProjection(db.Datasets, query)
        );

        string?[] expected =
        [
            .. db.OrderRows.GroupBy(order => order.OrderUserId)
                .Select(group => group.Max(order => order.OrderStatus))
                .OrderBy(value => value, StringComparer.Ordinal),
        ];

        string?[] actual =
        [
            .. result.Column("max_status").OrderBy(v => v, StringComparer.Ordinal),
        ];

        Assert.Equal(expected.Length, result.Count);
        Assert.Equal(expected, actual);
    }
}
