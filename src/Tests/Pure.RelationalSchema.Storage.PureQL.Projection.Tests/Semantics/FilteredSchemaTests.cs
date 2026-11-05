using System.Collections;
using System.Linq.Expressions;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.RowExpressions;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// A filtered schema dataset holds, of each table given a condition, only the
// rows the condition is true for, wherever a query reads the table: a row
// the condition drops is a row the table does not have. The tables here are
// held in memory (see Staff for their rows).
public sealed class FilteredSchemaTests
{
    [Fact]
    public void TableInFromHoldsOnlyTheRowsItsConditionIsTrueFor()
    {
        PureQLProjection projection = new(
            [Staff.Filtered(Staff.Schema(), ("people", Staff.RedTeam))],
            Staff.Names()
        );

        Assert.Equal(["Ann", "Dee"], Staff.Lines(projection));
        Assert.Equal(QueryExecution.InMemory, projection.Execution);
        Assert.Null(projection.FallbackReason);
    }

    [Fact]
    public void ConditionReadsAColumnTheQueryDoesNotRead()
    {
        PureQLProjection projection = new(
            [Staff.Filtered(Staff.Schema(), ("people", Staff.RedTeam))],
            Staff.Names()
        );

        Assert.Equal(
            "[people].Where(people => (people[team] == \"red\"))"
                + ".Select(p => (p[name]))",
            TreeText.Of(
                ((MethodCallExpression)projection.Expression)
                    .Arguments[0]
            )
        );
    }

    [Fact]
    public void InnerJoinPairsOnlyTheRowsOfBothTablesTheirConditionsAreTrueFor()
    {
        PureQLProjection onlyPeople = new(
            [Staff.Filtered(Staff.Schema(), ("people", Staff.RedTeam))],
            Staff.PeopleWithOrders(JoinKind.Inner)
        );
        PureQLProjection both = new(
            [
                Staff.Filtered(
                    Staff.Schema(),
                    ("people", Staff.RedTeam),
                    ("orders", Staff.BigOrders)
                ),
            ],
            Staff.PeopleWithOrders(JoinKind.Inner)
        );

        Assert.Equal(["Ann|1", "Ann|5", "Dee|9"], Staff.Lines(onlyPeople).Order());
        Assert.Equal(["Ann|5", "Dee|9"], Staff.Lines(both).Order());
    }

    [Fact]
    public void LeftJoinKeepsARowWhoseMatchesTheConditionDropsWithNulls()
    {
        PureQLProjection projection = new(
            [Staff.Filtered(Staff.Schema(), ("orders", Staff.BigOrders))],
            Staff.PeopleWithOrders(JoinKind.Left)
        );

        Assert.Equal(
            ["Ann|5", "Bob|7", "Cy|", "Dee|9"],
            Staff.Lines(projection).Order()
        );
    }

    [Fact]
    public void LeftJoinPreservesOnlyTheRowsTheConditionIsTrueFor()
    {
        PureQLProjection projection = new(
            [Staff.Filtered(Staff.Schema(), ("people", Staff.RedTeam))],
            Staff.PeopleWithOrders(JoinKind.Left)
        );

        Assert.Equal(["Ann|1", "Ann|5", "Dee|9"], Staff.Lines(projection).Order());
    }

    [Fact]
    public void RightJoinGivesNullsForTheRowsTheConditionDrops()
    {
        PureQLProjection projection = new(
            [Staff.Filtered(Staff.Schema(), ("people", Staff.RedTeam))],
            Staff.PeopleWithOrders(JoinKind.Right)
        );

        Assert.Equal(
            ["Ann|1", "Ann|5", "Dee|9", "|2", "|3", "|7"],
            Staff.Lines(projection).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void FullJoinSeesOnlyTheRowsOfBothTablesTheirConditionsAreTrueFor()
    {
        PureQLProjection projection = new(
            [
                Staff.Filtered(
                    Staff.Schema(),
                    ("people", Staff.RedTeam),
                    ("orders", Staff.BigOrders)
                ),
            ],
            Staff.PeopleWithOrders(JoinKind.Full)
        );

        Assert.Equal(
            ["Ann|5", "Dee|9", "|7"],
            Staff.Lines(projection).Order(StringComparer.Ordinal)
        );
    }

    [Fact]
    public void EveryReadOfTheTableInAQueryIsFiltered()
    {
        PureQLProjection projection = new(
            [Staff.Filtered(Staff.Schema(), ("people", Staff.RedTeam))],
            Staff.Teammates()
        );

        Assert.Equal(
            ["Ann|Ann", "Ann|Dee", "Dee|Ann", "Dee|Dee"],
            Staff.Lines(projection).Order()
        );
    }

    [Fact]
    public void SubqueryReadInFromSeesOnlyTheRowsTheConditionIsTrueFor()
    {
        PureQLProjection projection = new(
            [Staff.Filtered(Staff.Schema(), ("people", Staff.RedTeam))],
            Staff.NamesThroughASubquery()
        );

        Assert.Equal(["Ann", "Dee"], Staff.Lines(projection));
    }

    [Fact]
    public void SubqueryReadByInHoldsOnlyTheRowsTheConditionIsTrueFor()
    {
        PureQLProjection projection = new(
            [Staff.Filtered(Staff.Schema(), ("people", Staff.RedTeam))],
            Staff.TotalsOfTheOrdersOfListedPeople()
        );

        Assert.Equal(["1", "5", "9"], Staff.Lines(projection).Order());
    }

    [Fact]
    public void TotalAggregatesFoldOnlyTheRowsTheConditionIsTrueFor()
    {
        PureQLProjection projection = new(
            [Staff.Filtered(Staff.Schema(), ("orders", Staff.BigOrders))],
            Staff.CountAndSumOfOrders()
        );

        Assert.Equal(["3|21"], Staff.Lines(projection));
    }

    [Fact]
    public void TotalAggregatesOverATableWithNoMatchingRowFoldNoRows()
    {
        PureQLProjection projection = new(
            [
                Staff.Filtered(
                    Staff.Schema(),
                    ("orders", new BooleanRow(new LiteralBoolean(false)))
                ),
            ],
            Staff.CountAndSumOfOrders()
        );

        Assert.Equal(["0|0"], Staff.Lines(projection));
    }

    [Fact]
    public void GroupsHoldOnlyTheRowsTheConditionIsTrueFor()
    {
        PureQLProjection projection = new(
            [Staff.Filtered(Staff.Schema(), ("orders", Staff.BigOrders))],
            Staff.OrdersPerPerson()
        );

        Assert.Equal(["1|1", "2|1", "4|1"], Staff.Lines(projection).Order());
    }

    [Fact]
    public void RowForWhichTheConditionIsNullIsNotARowOfTheTable()
    {
        PureQLProjection projection = new(
            [Staff.Filtered(Staff.Schema(), ("people", Staff.Flagged))],
            Staff.Names()
        );

        Assert.Equal(["Ann"], Staff.Lines(projection));
    }

    public static TheoryData<string> Conditions => [.. ConditionsByName.Keys];

    // Conditions over the people, each given the source its fields name.
    private static IReadOnlyDictionary<
        string,
        Func<string, BooleanRow>
    > ConditionsByName =>
        new Dictionary<string, Func<string, BooleanRow>>
        {
            ["team is red"] = source => Staff.TeamIs("red", source),
            ["team is not red"] = source => Staff.Not(Staff.TeamIs("red", source)),
            ["flag"] = Staff.FlagOf,
            ["not flag"] = source => Staff.Not(Staff.FlagOf(source)),
            ["true"] = _ => new BooleanRow(new LiteralBoolean(true)),
            ["false"] = _ => new BooleanRow(new LiteralBoolean(false)),
        };

    [Theory]
    [MemberData(nameof(Conditions))]
    public void ConditionKeepsTheRowsTheSameConditionKeepsAsAQuerysWhere(string name)
    {
        Func<string, BooleanRow> condition = ConditionsByName[name];

        Assert.Equal(
            Staff.Lines(
                new PureQLProjection([Staff.Schema()], Staff.Names(condition("p")))
            ),
            Staff.Lines(
                new PureQLProjection(
                    [Staff.Filtered(Staff.Schema(), ("people", condition("people")))],
                    Staff.Names()
                )
            )
        );
    }

    [Fact]
    public void ComparisonWithAnEmptyCellOfANullableFieldIsNotTrue()
    {
        PureQLProjection others = new(
            [
                Staff.Filtered(
                    Staff.Schema(),
                    ("people", Staff.Not(Staff.TeamIs("red", "people")))
                ),
            ],
            Staff.Names()
        );

        Assert.Equal(["Bob", "Cy"], Staff.Lines(others));
        Assert.Equal(
            ["Ann", "Dee"],
            Staff.Lines(
                new PureQLProjection(
                    [Staff.Filtered(Staff.Schema(), ("people", Staff.RedTeam))],
                    Staff.Names()
                )
            )
        );
    }

    [Fact]
    public void SourceOfAFieldOfTheConditionIsNotRead()
    {
        PureQLProjection projection = new(
            [
                Staff.Filtered(
                    Staff.Schema(),
                    ("people", Staff.TeamIs("red", "no such source"))
                ),
            ],
            Staff.Teammates()
        );

        Assert.Equal(
            ["Ann|Ann", "Ann|Dee", "Dee|Ann", "Dee|Dee"],
            Staff.Lines(projection).Order()
        );
    }

    [Fact]
    public void TableGivenSeveralConditionsHoldsTheRowsAllOfThemAreTrueFor()
    {
        PureQLProjection projection = new(
            [
                Staff.Filtered(
                    Staff.Schema(),
                    ("people", Staff.RedTeam),
                    ("people", Staff.Not(Staff.Flagged))
                ),
            ],
            Staff.Names()
        );

        Assert.Equal(["Dee"], Staff.Lines(projection));
    }

    [Fact]
    public void ConditionBelongsToTheTableWithItsHash()
    {
        IStoredSchemaDataSet schema = Staff.Schema();
        ITable people = Staff.Table(schema, "people");
        ITable same = new Table.Table(new String("people"), people.Columns, []);
        PureQLProjection projection = new(
            [
                new PureQLFilteredSchemaDataSet(
                    schema,
                    [KeyValuePair.Create(same, Staff.RedTeam)]
                ),
            ],
            Staff.Names()
        );

        Assert.NotSame(people, same);
        Assert.Equal(["Ann", "Dee"], Staff.Lines(projection));
    }

    [Fact]
    public void TableWithoutAConditionIsTheOriginsTable()
    {
        IStoredSchemaDataSet schema = Staff.Schema();
        PureQLFilteredSchemaDataSet filtered = Staff.Filtered(
            schema,
            ("people", Staff.RedTeam)
        );
        ITable orders = Staff.Table(schema, "orders");
        ITable people = Staff.Table(schema, "people");

        Assert.Same(schema[orders], filtered[orders]);
        Assert.NotSame(schema[people], filtered[people]);
        Assert.Equal(
            Staff.Lines(new PureQLProjection([schema], Staff.CountAndSumOfOrders())),
            Staff.Lines(new PureQLProjection([filtered], Staff.CountAndSumOfOrders()))
        );
    }

    [Fact]
    public void SchemaWithNoConditionsIsTheOrigin()
    {
        IStoredSchemaDataSet schema = Staff.Schema();
        PureQLFilteredSchemaDataSet filtered = new(schema, []);

        Assert.Same(schema.Schema, filtered.Schema);
        Assert.Equal(schema.Count, filtered.Count);
        Assert.Equal(
            schema.Keys.Select(table => table.Name.TextValue).Order(),
            filtered.Keys.Select(table => table.Name.TextValue).Order()
        );
        Assert.All(schema, table => Assert.Same(table.Value, filtered[table.Key]));
        Assert.Equal(
            Staff.Lines(
                new PureQLProjection([schema], Staff.PeopleWithOrders(JoinKind.Full))
            ),
            Staff.Lines(
                new PureQLProjection([filtered], Staff.PeopleWithOrders(JoinKind.Full))
            )
        );
    }

    [Fact]
    public void SchemaIsReadAsADictionaryOfItsTables()
    {
        IStoredSchemaDataSet schema = Staff.Schema();
        PureQLFilteredSchemaDataSet filtered = Staff.Filtered(
            schema,
            ("people", Staff.RedTeam)
        );
        ITable people = Staff.Table(schema, "people");
        ITable unknown = new Table.Table(new String("nobody"), [], []);

        Assert.True(filtered.ContainsKey(people));
        Assert.False(filtered.ContainsKey(unknown));
        Assert.True(filtered.TryGetValue(people, out IStoredTableDataSet? table));
        Assert.False(filtered.TryGetValue(unknown, out _));
        Assert.Same(people, table!.TableSchema);
        Assert.Equal(2, filtered.Values.Count());
        Assert.Equal(
            ["orders", "people"],
            Untyped(filtered)
                .OfType<KeyValuePair<ITable, IStoredTableDataSet>>()
                .Select(pair => pair.Key.Name.TextValue)
                .Order()
        );
    }

    [Fact]
    public void FilteredTableEnumeratedOnItsOwnHoldsOnlyTheRowsTheConditionIsTrueFor()
    {
        IStoredSchemaDataSet schema = Staff.Schema();
        IStoredTableDataSet people = Staff.Filtered(schema, ("people", Staff.RedTeam))[
            Staff.Table(schema, "people")
        ];

        Assert.Equal(["1|Ann|red|True", "4|Dee|red|False"], Staff.Lines(people));
        Assert.Equal(
            ["1|Ann|red|True", "4|Dee|red|False"],
            Staff.Lines(
                people.TableSchema,
                Untyped(people).OfType<IRow>()
            )
        );
        Assert.Equal(typeof(IRow), people.ElementType);
    }

    [Fact]
    public async Task FilteredTableEnumeratedAsynchronouslyHoldsTheSameRows()
    {
        IStoredSchemaDataSet schema = Staff.Schema();
        IStoredTableDataSet people = Staff.Filtered(schema, ("people", Staff.RedTeam))[
            Staff.Table(schema, "people")
        ];
        List<IRow> rows = [];

        await foreach (IRow row in people)
        {
            rows.Add(row);
        }

        Assert.Equal(
            ["1|Ann|red|True", "4|Dee|red|False"],
            Staff.Lines(people.TableSchema, rows)
        );
    }

    [Fact]
    public void LinqComposedOverAFilteredTableReadsOnlyTheRowsTheConditionIsTrueFor()
    {
        IStoredSchemaDataSet schema = Staff.Schema();
        IStoredTableDataSet people = Staff.Filtered(schema, ("people", Staff.RedTeam))[
            Staff.Table(schema, "people")
        ];

        Assert.Equal(2, people.Count());
        Assert.Equal(
            ["4|Dee|red|False"],
            Staff.Lines(people.TableSchema, Queryable.Skip(people, 1))
        );
    }

    [Fact]
    public void FilteredTableOfAFilteredSchemaHoldsTheRowsBothConditionsAreTrueFor()
    {
        IStoredSchemaDataSet schema = Staff.Schema();
        PureQLFilteredSchemaDataSet twice = Staff.Filtered(
            Staff.Filtered(schema, ("people", Staff.RedTeam)),
            ("people", Staff.Flagged)
        );

        Assert.Equal(
            ["Ann"],
            Staff.Lines(new PureQLProjection([twice], Staff.Names()))
        );
        Assert.Equal(
            ["1|Ann|red|True"],
            Staff.Lines(twice[Staff.Table(schema, "people")])
        );
    }

    [Fact]
    public void FieldDeclaredNonNullIsParsedAsItIsAndFailsOnAnEmptyCell()
    {
        PureQLProjection projection = new(
            [
                Staff.Filtered(
                    Staff.Schema(),
                    (
                        "people",
                        new BooleanRow(new FieldBoolean("people", "flag"))
                    )
                ),
            ],
            Staff.Names()
        );

        _ = Assert.Throws<FormatException>(() => projection.ToList());
    }

    [Fact]
    public void ConditionOverALongColumnTypedIntegerIsRead()
    {
        IStoredSchemaDataSet schema = new InlineTable(
            "t",
            "orders",
            ("total", new LongColumnType())
        ).With(["5"], ["3"]);
        PureQLFilteredSchemaDataSet filtered = Staff.Filtered(
            schema,
            ("orders", Staff.BigOrders)
        );

        Assert.Equal(["5"], Staff.Lines(filtered.Values.Single()));
    }

    // The items of a sequence read as the non-generic sequence it also is.
    private static IEnumerable<object> Untyped(IEnumerable sequence)
    {
        foreach (object item in sequence)
        {
            yield return item;
        }
    }
}
