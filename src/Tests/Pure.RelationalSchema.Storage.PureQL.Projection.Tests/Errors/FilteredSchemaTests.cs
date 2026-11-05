using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.Parameters;
using PureQL.CSharp.Model.RowExpressions;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Errors;

// A condition that cannot be applied to its table fails whatever reads the
// table, before a row is read, and never lets the table's rows through: a
// projection that reads the table is not built, and the table cannot be
// enumerated. A query that does not read the table is not affected.
public sealed class FilteredSchemaTests
{
    public static TheoryData<string> Inapplicable => [.. InapplicableByName.Keys];

    private static IReadOnlyDictionary<
        string,
        (BooleanRow Condition, string Message)
    > InapplicableByName =>
        new Dictionary<string, (BooleanRow Condition, string Message)>
        {
            ["a field that names no column"] = (
                new BooleanRow(new FieldBoolean("people", "retired")),
                "The condition of table 'people' reads field 'retired', which the "
                    + "table does not have."
            ),
            ["a field declared with another type"] = (
                new BooleanRow(new FieldBoolean("people", "team")),
                "Field 'team' of the condition of table 'people' is declared "
                    + "boolean but holds string."
            ),
            ["a field of a column of no PureQL type"] = (
                new BooleanRow(new FieldBoolean("people", "hash")),
                "Field 'hash' of the condition of table 'people' is declared "
                    + "boolean but holds a type PureQL does not have."
            ),
            ["a parameter"] = (
                new BooleanRow(new ParamBoolean("allowed")),
                "The condition of table 'people' reads parameter 'allowed', and a "
                    + "table's condition reads only the table's fields."
            ),
            ["a parameter below an operator"] = (
                Staff.Not(
                    new BooleanRow(
                        new LogicalRow(
                            new AndRow(
                                [
                                    Staff.RedTeam,
                                    new BooleanRow(new ParamBoolean("allowed")),
                                ]
                            )
                        )
                    )
                ),
                "The condition of table 'people' reads parameter 'allowed', and a "
                    + "table's condition reads only the table's fields."
            ),
            ["a list parameter"] = (
                TeamIn(new ListString(new ListParamString("teams"))),
                "The condition of table 'people' reads parameter 'teams', and a "
                    + "table's condition reads only the table's fields."
            ),
            ["a subquery column"] = (
                TeamIn(new ListString(new ListSubqueryColumnString("staff", "team"))),
                "The condition of table 'people' reads column 'team' of subquery "
                    + "'staff', and a table's condition reads only the table's fields."
            ),
        };

    [Theory]
    [MemberData(nameof(Inapplicable))]
    public void ProjectionReadingATableWithAnInapplicableConditionIsNotBuilt(string name)
    {
        (BooleanRow condition, string message) = InapplicableByName[name];
        RecordingProvider provider = new();
        IStoredSchemaDataSet schema = ProvidedTable.Schema(People(), provider);
        PureQLFilteredSchemaDataSet filtered = Staff.Filtered(
            schema,
            ("people", condition)
        );

        ArgumentException exception = Assert.Throws<ArgumentException>(() =>
            new PureQLProjection([filtered], Staff.Names())
        );

        Assert.Equal(message, exception.Message);
        Assert.Empty(provider.Queries);
        Assert.Equal(0, ((ProvidedTable)schema.Values.Single()).Reads);
    }

    [Theory]
    [MemberData(nameof(Inapplicable))]
    public void TableWithAnInapplicableConditionIsNotEnumerated(string name)
    {
        (BooleanRow condition, string message) = InapplicableByName[name];
        RecordingProvider provider = new();
        IStoredSchemaDataSet schema = ProvidedTable.Schema(People(), provider);
        IStoredTableDataSet people = Staff
            .Filtered(schema, ("people", condition))
            .Values.Single();

        Assert.Equal(
            message,
            Assert.Throws<ArgumentException>(() => people.ToList()).Message
        );
        Assert.Equal(
            message,
            Assert.Throws<ArgumentException>(() => people.Expression).Message
        );
        Assert.Equal(
            message,
            Assert
                .Throws<ArgumentException>(() =>
                    people.GetAsyncEnumerator().MoveNextAsync().AsTask().Wait()
                )
                .Message
        );
        Assert.Empty(provider.Queries);
        Assert.Equal(0, ((ProvidedTable)schema.Values.Single()).Reads);
    }

    [Theory]
    [MemberData(nameof(Inapplicable))]
    public void TableHeldInMemoryWithAnInapplicableConditionIsNotEnumerated(string name)
    {
        (BooleanRow condition, string message) = InapplicableByName[name];
        IStoredSchemaDataSet origin = People();
        CountedTable people = new(origin.Values.Single());
        PureQLFilteredSchemaDataSet filtered = Staff.Filtered(
            new StoredSchemaDataSet(origin.Schema, [people]),
            ("people", condition)
        );

        Assert.Equal(
            message,
            Assert
                .Throws<ArgumentException>(() =>
                    new PureQLProjection([filtered], Staff.Names())
                )
                .Message
        );
        Assert.Equal(
            message,
            Assert
                .Throws<ArgumentException>(() => filtered.Values.Single().ToList())
                .Message
        );
        Assert.Equal(0, people.Reads);
    }

    [Theory]
    [MemberData(nameof(Inapplicable))]
    public void QueryThatDoesNotReadTheTableIsNotAffected(string name)
    {
        PureQLFilteredSchemaDataSet filtered = Staff.Filtered(
            Staff.Schema(People(), Staff.Orders()),
            ("people", InapplicableByName[name].Condition),
            ("orders", Staff.BigOrders)
        );

        Assert.Equal(
            ["3|21"],
            Staff.Lines(new PureQLProjection([filtered], Staff.CountAndSumOfOrders()))
        );
    }

    [Fact]
    public void ConditionForATableTheSchemaDoesNotHaveFailsEveryReadOfTheSchema()
    {
        IStoredSchemaDataSet schema = Staff.Schema();
        PureQLFilteredSchemaDataSet filtered = new(
            schema,
            [
                KeyValuePair.Create(Staff.Table(schema, "people"), Staff.RedTeam),
                KeyValuePair.Create(
                    (ITable)
                        new Table.Table(
                            new String("people"),
                            Staff.Table(schema, "people").Columns.Skip(1),
                            []
                        ),
                    Staff.RedTeam
                ),
            ]
        );
        const string message =
            "A condition is given for table 'people', which schema 't' does not have.";

        Assert.Equal(
            message,
            Assert
                .Throws<ArgumentException>(() =>
                    new PureQLProjection([filtered], Staff.CountAndSumOfOrders())
                )
                .Message
        );
        Assert.Equal(
            message,
            Assert.Throws<ArgumentException>(() => filtered.ToList()).Message
        );
        Assert.Equal(
            message,
            Assert
                .Throws<ArgumentException>(() =>
                    filtered[Staff.Table(schema, "orders")]
                )
                .Message
        );
        Assert.Equal(
            message,
            Assert.Throws<ArgumentException>(() => filtered.Count).Message
        );
    }

    [Fact]
    public void BuildingAFilteredSchemaChecksNothing()
    {
        IStoredSchemaDataSet schema = Staff.Schema();

        PureQLFilteredSchemaDataSet filtered = new(
            schema,
            [
                KeyValuePair.Create(
                    (ITable)new Table.Table(new String("nobody"), [], []),
                    new BooleanRow(new ParamBoolean("allowed"))
                ),
            ]
        );

        Assert.Same(schema.Schema, filtered.Schema);
    }

    // The people, with a column of a type PureQL does not have.
    private static IStoredSchemaDataSet People()
    {
        return new InlineTable(
            "t",
            "people",
            ("id", new LongColumnType()),
            ("name", new StringColumnType()),
            ("team", new StringColumnType()),
            ("flag", new BoolColumnType()),
            ("hash", new DeterminedHashColumnType())
        ).With(["1", "Ann", "red", "True", "x"], ["2", "Bob", "blue", "False", "y"]);
    }

    private static BooleanRow TeamIn(ListString teams)
    {
        return new BooleanRow(
            new ComparisonRow(
                new InRow(
                    new InStringRow(
                        new StringNullableRow(
                            new ExactStringNullableRow(
                                new FieldStringNullable("people", "team")
                            )
                        ),
                        teams
                    )
                )
            )
        );
    }
}
