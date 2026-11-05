using System.Linq.Expressions;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.RowExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Tree;

// The tree of a query over a filtered schema dataset. Every scan of a
// filtered table is rooted on the constant holding the origin's table,
// under the Where of the table's condition and below the scan's Select: the
// condition is the first thing done to the table's rows wherever the query
// reads the table, the scan keeps its shape, and the tree holds nothing a
// provider does not translate.
public sealed class FilteredSchemaTreeTests
{
    private const string RedPeople =
        "[people].Where(people => (people[team] == \"red\"))";

    private const string BigOrders =
        "[orders].Where(orders => (Int64.Parse(orders[total]!, NumberStyles.Integer, "
        + "CultureInfo.InvariantCulture) > 4L))";

    public static TheoryData<string> Queries => [.. QueriesByName.Keys];

    private static IReadOnlyDictionary<string, PureQLQuery> QueriesByName =>
        new Dictionary<string, PureQLQuery>
        {
            ["from"] = Staff.Names(),
            ["where"] = Staff.Names(Staff.FlagOf("p")),
            ["inner join"] = Staff.PeopleWithOrders(JoinKind.Inner),
            ["left join"] = Staff.PeopleWithOrders(JoinKind.Left),
            ["right join"] = Staff.PeopleWithOrders(JoinKind.Right),
            ["full join"] = Staff.PeopleWithOrders(JoinKind.Full),
            ["self join"] = Staff.Teammates(),
            ["from subquery"] = Staff.NamesThroughASubquery(),
            ["in subquery"] = Staff.TotalsOfTheOrdersOfListedPeople(),
            ["total"] = Staff.CountAndSumOfOrders(),
            ["grouped"] = Staff.OrdersPerPerson(),
        };

    [Fact]
    public void WhereOfTheQueryStaysAboveTheScanOfAFilteredTable()
    {
        Assert.Equal(
            RedPeople
                + ".Select(p => (p[name], p[flag])).Where(p => (p.Item2 == true))"
                + ".Select(row => (row.Item1))",
            Query(Staff.Names(Staff.FlagOf("p")), ("people", Staff.RedTeam))
        );
    }

    [Fact]
    public void EveryScanOfAFilteredTableIsRootedOnItsCondition()
    {
        Assert.Equal(
            RedPeople
                + ".Select(p => (p[name], p[team])).Join("
                + RedPeople
                + ".Select(q => (q[name], q[team])), left => (left.Item2), "
                + "q => (q.Item2), (left, q) => (left, q))"
                + ".Select(row => (row.Item1.Item1, row.Item2.Item1))",
            Query(Staff.Teammates(), ("people", Staff.RedTeam))
        );
    }

    [Fact]
    public void ConditionsOfBothSidesOfAnOuterJoinAreBelowTheJoin()
    {
        Assert.Equal(
            RedPeople
                + ".Select(p => (p[id], p[name])).GroupJoin("
                + BigOrders
                + ".Select(o => (o[person], o[total])), left => (left.Item1), "
                + "o => (o.Item1), (left, matches) => (left, matches))"
                + ".SelectMany(match => match.Item2.DefaultIfEmpty(), "
                + "(match, o) => (match.Item1, o))"
                + ".Select(row => (row.Item1.Item2, row.Item2.Item2))",
            Query(
                Staff.PeopleWithOrders(JoinKind.Left),
                ("people", Staff.RedTeam),
                ("orders", Staff.BigOrders)
            )
        );
    }

    [Fact]
    public void SubqueryReadByInScansTheFilteredTable()
    {
        Assert.Equal(
            "[orders].Select(o => (o[person], o[total])).Where(o => "
                + RedPeople
                + ".Select(t.people => (t.people[id])).Select(staff => staff.Item1)"
                + ".Contains(o.Item1)).Select(row => (row.Item2))",
            Query(Staff.TotalsOfTheOrdersOfListedPeople(), ("people", Staff.RedTeam))
        );
    }

    [Fact]
    public void AggregatesOverAllRowsFoldTheScanOfTheFilteredTable()
    {
        Assert.Equal(
            "[one row].Select(one => ((long?)"
                + BigOrders
                + ".Select(o => (o[total]!)).LongCount(), "
                + BigOrders
                + ".Select(o => (o[total]!)).Sum(row => row.Item1)))",
            Query(Staff.CountAndSumOfOrders(), ("orders", Staff.BigOrders))
        );
    }

    [Fact]
    public void GroupsAreOfTheScanOfTheFilteredTable()
    {
        Assert.Equal(
            BigOrders
                + ".Select(o => (o[person]!)).GroupBy(row => (row.Item1))"
                + ".Select(group => (group.Key.Item1, (long?)group.LongCount()))",
            Query(Staff.OrdersPerPerson(), ("orders", Staff.BigOrders))
        );
    }

    [Fact]
    public void SeveralConditionsOfATableAreOneWhere()
    {
        Assert.Equal(
            "[people].Where(people => ((people[team] == \"red\") && "
                + "(people[flag] != true))).Select(p => (p[name]))",
            Query(
                Staff.Names(),
                ("people", Staff.RedTeam),
                ("people", Staff.Not(Staff.Flagged))
            )
        );
    }

    [Fact]
    public void ConditionThatIsAlwaysTrueLeavesTheScanOfTheOriginsTable()
    {
        Assert.Equal(
            "[people].Select(p => (p[name]))",
            Query(Staff.Names(), ("people", new BooleanRow(new LiteralBoolean(true))))
        );
    }

    [Fact]
    public void ConditionThatIsAlwaysFalseStaysOnTheScan()
    {
        Assert.Equal(
            "[people].Where(people => false).Select(p => (p[name]))",
            Query(Staff.Names(), ("people", new BooleanRow(new LiteralBoolean(false))))
        );
    }

    [Fact]
    public void FieldOfTheConditionIsTheCellReadAQueryFieldIs()
    {
        Assert.Equal(
            "[people].Where(people => ((String.IsNullOrEmpty("
                + "people.Cells[team].Value.TextValue) ? null : "
                + "people.Cells[team].Value.TextValue) == \"red\"))"
                + ".Select(p => ((String.IsNullOrEmpty(p.Cells[name].Value.TextValue) "
                + "? null : p.Cells[name].Value.TextValue)))",
            TreeText.Full(Tree(Staff.Names(), ("people", Staff.RedTeam)))
        );
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public void TreeOverFilteredTablesHoldsOnlyTranslatableNodes(string name)
    {
        PureQLProjection projection = new(
            [
                Staff.Filtered(
                    Staff.Schema(),
                    ("people", Staff.RedTeam),
                    ("people", Staff.Not(Staff.Flagged)),
                    ("orders", Staff.BigOrders)
                ),
            ],
            QueriesByName[name]
        );

        Assert.Empty(Untranslatable.In(projection.Expression));
    }

    [Theory]
    [MemberData(nameof(Queries))]
    public void EveryRootOfTheTreeIsAnOriginsTable(string name)
    {
        IStoredSchemaDataSet schema = Staff.Schema();
        PureQLProjection projection = new(
            [
                Staff.Filtered(
                    schema,
                    ("people", Staff.RedTeam),
                    ("orders", Staff.BigOrders)
                ),
            ],
            QueriesByName[name]
        );

        Assert.All(
            Roots.In(projection.Expression),
            table => Assert.Contains(table, schema.Values)
        );
        Assert.NotEmpty(Roots.In(projection.Expression));
    }

    private static string Query(
        PureQLQuery query,
        params (string Table, BooleanRow Condition)[] conditions
    )
    {
        return TreeText.Of(Tree(query, conditions));
    }

    // The query below the result rows.
    private static Expression Tree(
        PureQLQuery query,
        params (string Table, BooleanRow Condition)[] conditions
    )
    {
        return (
            (MethodCallExpression)
                new PureQLProjection(
                    [Staff.Filtered(Staff.Schema(), conditions)],
                    query
                ).Expression
        ).Arguments[0];
    }

    // The stored tables a tree holds as constants.
    private sealed class Roots : ExpressionVisitor
    {
        private readonly List<IStoredTableDataSet> _tables = [];

        public static IReadOnlyList<IStoredTableDataSet> In(Expression tree)
        {
            Roots roots = new();
            _ = roots.Visit(tree);
            return roots._tables;
        }

        protected override Expression VisitConstant(ConstantExpression node)
        {
            if (node.Value is IStoredTableDataSet table)
            {
                _tables.Add(table);
            }

            return node;
        }
    }
}
