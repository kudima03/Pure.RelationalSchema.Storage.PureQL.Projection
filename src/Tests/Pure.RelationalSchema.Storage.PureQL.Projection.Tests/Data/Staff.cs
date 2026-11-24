using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.GroupExpressions;
using PureQL.CSharp.Model.Keys;
using PureQL.CSharp.Model.Lists;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.TotalExpressions;
using String = Pure.Primitives.String.String;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;

// The schema the filtered schema tests read, the conditions they filter its
// tables with and the queries they run over it.
//
//   t.people: id  name  team  flag        t.orders: id  person  total
//             1   Ann   red   True                  10  1       5
//             2   Bob   blue  False                 11  2       7
//             3   Cy                                12  4       9
//             4   Dee   red   False                 13  1       1
//                                                   14  9       3
//                                                   15  3       2
//
// Cy has no team and no flag (empty cells, NULL), order 14 is of nobody, and
// the only order of Cy is a small one.
internal static class Staff
{
    private static readonly InlineTable PeopleTable = new(
        "t",
        "people",
        ("id", new LongColumnType()),
        ("name", new StringColumnType()),
        ("team", new StringColumnType()),
        ("flag", new BoolColumnType())
    );

    private static readonly InlineTable OrdersTable = new(
        "t",
        "orders",
        ("id", new LongColumnType()),
        ("person", new LongColumnType()),
        ("total", new LongColumnType())
    );

    // The people of the red team: Ann and Dee. A person without a team is
    // not of the red team.
    public static BooleanRow RedTeam => TeamIs("red", "people");

    // The orders above 4: 10, 11 and 12.
    public static BooleanRow BigOrders =>
        new(
            new GreaterThanDecimalRow(
                new FieldInteger("orders", "total"),
                new LiteralInteger(4)
            )
        );

    // The flag as it is: true for Ann, false for Bob and Dee, null for Cy.
    public static BooleanRow Flagged => FlagOf("people");

    public static BooleanRow FlagOf(string source)
    {
        return new CoalesceBooleanRow([new FieldBooleanNullable(source, "flag")]);
    }

    public static BooleanRow TeamIs(string team, string source)
    {
        return new EqualStringRow(
            new FieldStringNullable(source, "team"),
            new LiteralString(team)
        );
    }

    public static BooleanRow Not(BooleanRow condition)
    {
        return new NotRow(condition);
    }

    // Both tables, held in memory, in one schema dataset.
    public static IStoredSchemaDataSet Schema()
    {
        return Schema(People(), Orders());
    }

    // The tables of several one-table datasets in one schema dataset.
    public static IStoredSchemaDataSet Schema(params IStoredSchemaDataSet[] parts)
    {
        return new StoredSchemaDataSet(
            new Schema.Schema(
                new String("t"),
                [.. parts.SelectMany(part => part.Keys)],
                []
            ),
            [.. parts.SelectMany(part => part.Values)]
        );
    }

    public static IStoredSchemaDataSet People()
    {
        return PeopleTable.With(
            ["1", "Ann", "red", "True"],
            ["2", "Bob", "blue", "False"],
            ["3", "Cy", "", ""],
            ["4", "Dee", "red", "False"]
        );
    }

    public static IStoredSchemaDataSet Orders()
    {
        return OrdersTable.With(
            ["10", "1", "5"],
            ["11", "2", "7"],
            ["12", "4", "9"],
            ["13", "1", "1"],
            ["14", "9", "3"],
            ["15", "3", "2"]
        );
    }

    public static ITable Table(IStoredSchemaDataSet schema, string name)
    {
        return schema.Keys.Single(table => table.Name.TextValue == name);
    }

    // The schema with its tables under the conditions given by table name.
    public static PureQLFilteredSchemaDataSet Filtered(
        IStoredSchemaDataSet schema,
        params (string Table, BooleanRow Condition)[] conditions
    )
    {
        return new PureQLFilteredSchemaDataSet(
            schema,
            conditions.Select(condition =>
                KeyValuePair.Create(Table(schema, condition.Table), condition.Condition)
            )
        );
    }

    // The rows of a table, each as its cell texts, in column order, joined
    // by '|'.
    public static IReadOnlyList<string> Lines(IStoredTableDataSet table)
    {
        return Lines(table.TableSchema, table);
    }

    public static IReadOnlyList<string> Lines(ITable table, IEnumerable<IRow> rows)
    {
        return
        [
            .. rows.Select(row =>
                string.Join(
                    '|',
                    table.Columns.Select(column =>
                        row.Cells.Single(cell =>
                            cell.Key.Name.TextValue == column.Name.TextValue
                        ).Value.Value.TextValue
                    )
                )
            ),
        ];
    }

    // from t.people p select p.name
    public static PureQLQuery Names()
    {
        return Names(null);
    }

    // from t.people p where <condition> select p.name
    public static PureQLQuery Names(BooleanRow? where)
    {
        return new MainPlainQueryWithoutPagination(
            from: new FromEntity("t.people", "p"),
            where: where,
            select: [Name("p")]
        );
    }

    // from t.people p <kind> join t.orders o on o.person = p.id
    // select p.name, o.total
    public static PureQLQuery PeopleWithOrders(JoinKind kind)
    {
        return new MainPlainQueryWithoutPagination(
            from: new FromEntity("t.people", "p"),
            joins:
            [
                new JoinEntity(
                    kind,
                    "t.orders",
                    Equal(Integer("o", "person"), Integer("p", "id")),
                    "o"
                ),
            ],
            select: [Name("p"), Total("o")]
        );
    }

    // from t.people p join t.people q on q.team = p.team
    // select p.name, q.name as other
    public static PureQLQuery Teammates()
    {
        return new MainPlainQueryWithoutPagination(
            from: new FromEntity("t.people", "p"),
            joins:
            [
                new JoinEntity(
                    JoinKind.Inner,
                    "t.people",
                    new EqualStringRow(Team("q"), Team("p")),
                    "q"
                ),
            ],
            select: [Name("p"), Name("q", "other")]
        );
    }

    // staff: from t.people select name
    // from staff s select s.name
    public static PureQLQuery NamesThroughASubquery()
    {
        return new MainPlainQueryWithoutPagination(
            subqueries:
            [
                new SubqueryQuery(
                    "staff",
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromEntity("t.people"),
                        select: [Name("t.people")]
                    )
                ),
            ],
            from: new FromSubquery("staff", "s"),
            select: [Name("s")]
        );
    }

    // staff: from t.people select id
    // from t.orders o where o.person in staff.id select o.total
    public static PureQLQuery TotalsOfTheOrdersOfListedPeople()
    {
        return new MainPlainQueryWithoutPagination(
            subqueries:
            [
                new SubqueryQuery(
                    "staff",
                    new PlainQueryWithoutPaginationWithoutOrderBy(
                        from: new FromEntity("t.people"),
                        select:
                        [
                            new SelectItemProjection(
                                "id",
                                new FieldIntegerNullable("t.people", "id")
                            ),
                        ]
                    )
                ),
            ],
            from: new FromEntity("t.orders", "o"),
            where: new InDecimalRow(
                Integer("o", "person"),
                new ListSubqueryColumnIntegerNullable("staff", "id")
            ),
            select: [Total("o")]
        );
    }

    // from t.orders o aggregate count() as count, sum(o.total) as sum
    public static PureQLQuery CountAndSumOfOrders()
    {
        return new MainTotalQuery(
            from: new FromEntity("t.orders", "o"),
            aggregate:
            [
                new SelectItemTotal("count", new CountTotal()),
                new SelectItemTotal(
                    "sum",
                    new SumIntegerTotal(new FieldInteger("o", "total"))
                ),
            ]
        );
    }

    // from t.orders o group by o.person select person, count()
    public static PureQLQuery OrdersPerPerson()
    {
        return new MainGroupedQueryWithoutPagination(
            from: new FromEntity("t.orders", "o"),
            groupBy: [new GroupKey("person", new FieldInteger("o", "person"))],
            select:
            [
                new SelectItemGroup("person", new KeyInteger("person")),
                new SelectItemGroup("count", new CountGroup()),
            ]
        );
    }

    private static SelectItemProjection Name(string source, string alias = "name")
    {
        return new SelectItemProjection(alias, new FieldStringNullable(source, "name"));
    }

    private static SelectItemProjection Total(string source)
    {
        return new SelectItemProjection(
            "total",
            new FieldIntegerNullable(source, "total")
        );
    }

    private static StringNullableRow Team(string source)
    {
        return new FieldStringNullable(source, "team");
    }

    private static DecimalNullableRow Integer(string source, string field)
    {
        return new FieldIntegerNullable(source, field);
    }

    private static BooleanRow Equal(DecimalNullableRow left, DecimalNullableRow right)
    {
        return new EqualDecimalRow(left, right);
    }
}
