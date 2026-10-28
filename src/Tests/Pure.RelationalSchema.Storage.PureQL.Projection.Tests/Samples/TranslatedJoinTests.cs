using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using Pure.RelationalSchema.Storage.Samples.SchemaDataSets;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.RowExpressions;
using PureQL.CSharp.Model.SelectItems;
using PureQL.CSharp.Model.TotalExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Samples;

// A join on fields the query declares non-null, run over the fixtures stored
// in PostgreSQL through the provider of their tables, is one SQL statement
// whose join PostgreSQL hashes or merges: the keys are read without a null
// check, so the SQL compares them with =, which a hash or merge join serves,
// not with IS NOT DISTINCT FROM, which only a nested loop over every pair of
// rows can evaluate (issue #243). The fixture's session disables nested
// loops, so one in the plan means the planner had no other way. A join on
// keys the query declares nullable is hashed or merged too when on or where
// also requires notEqual(key, null), as the specification says (issue #249).
// Needs Docker.
public sealed class TranslatedJoinTests(PostgreSqlDatabase database)
    : IClassFixture<PostgreSqlDatabase>
{
    private const string Employees = "schema_with_foreign_keys.employees";

    private readonly PostgreSqlDatabase _database = database;

    [Fact]
    public void JoinOnFieldsDeclaredNonNullIsTranslatedToAHashOrMergeJoin()
    {
        Sample sample = Sample.Named("InnerJoinQuery");
        PureQLProjection projection = new(_database.Datasets, sample.Query);

        List<IRow> rows = [.. projection];
        string plan = _database.LastPlan();

        Assert.Equal(QueryExecution.Translated, projection.Execution);
        Assert.Null(projection.FallbackReason);
        new ExpectedTable(sample.Result!).AssertUnordered(projection);
        Assert.Equal(sample.Result!.Count(), rows.Count);
        Assert.Matches("(Hash|Merge) Join", plan);
        Assert.Matches("(Hash|Merge) Cond:", plan);
        Assert.DoesNotContain("Nested Loop", plan);
        Assert.DoesNotContain("Join Filter", plan);
        Assert.DoesNotContain("IS NOT DISTINCT FROM", plan);
        Assert.Contains(" AS uuid) = CAST(", plan);
    }

    [Fact]
    public void JoinOnNullableKeysGuardedInOnIsTranslatedToAHashOrMergeJoin()
    {
        Sample sample = Sample.Named("JoinOnNullableKeyQuery");
        PureQLProjection projection = new(_database.Datasets, sample.Query);

        List<IRow> rows = [.. projection];
        string plan = _database.LastPlan();

        Assert.Equal(QueryExecution.Translated, projection.Execution);
        Assert.Equal(sample.Result!.Count(), rows.Count);
        Assert.Matches("(Hash|Merge) Join", plan);
        Assert.Matches("(Hash|Merge) Cond:", plan);
        Assert.DoesNotContain("Nested Loop", plan);
        Assert.DoesNotContain("DISTINCT FROM", plan);
    }

    [Fact]
    public void JoinOnNullableKeysGuardedInWhereIsTranslatedToAHashOrMergeJoin()
    {
        PureQLQuery query = GuardedInWhere();
        PureQLProjection projection = new(_database.Datasets, query);

        IReadOnlyList<string> count = ResultColumn.Texts(projection, "n");
        string plan = _database.LastPlan();

        Assert.Equal(QueryExecution.Translated, projection.Execution);
        Assert.Equal(
            ResultColumn.Texts(
                new PureQLProjection(
                    [new SchemaDataSetWithForeignKeys(), new AuditSchemaDataSet()],
                    query
                ),
                "n"
            ),
            count
        );
        Assert.Matches("(Hash|Merge) Join", plan);
        Assert.Matches("(Hash|Merge) Cond:", plan);
        Assert.DoesNotContain("Nested Loop", plan);
        Assert.DoesNotContain("DISTINCT FROM", plan);
    }

    // The count of the pairs of employees a and b with the same manager, where
    // a has one.
    private static PureQLQuery GuardedInWhere()
    {
        return new PureQLQuery(
            new MainTotalQuery(
                new From(new FromEntity(Employees, "a")),
                [
                    new SelectItemTotal(
                        new SelectItemTotalNonNullable(
                            new SelectItemTotalInteger(
                                "n",
                                new IntegerTotal(
                                    new AggregateIntegerTotal(new CountTotal())
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
                            Employees,
                            new BooleanRow(
                                new ComparisonRow(
                                    new EqualRow(
                                        new EqualUuidRow(ManagerId("a"), ManagerId("b"))
                                    )
                                )
                            ),
                            "b"
                        )
                    ),
                ],
                where: new BooleanRow(
                    new ComparisonRow(
                        new NotEqualRow(
                            new NotEqualUuidRow(
                                ManagerId("a"),
                                new UuidNullableRow(
                                    new ExactUuidNullableRow(new LiteralUuidNullable())
                                )
                            )
                        )
                    )
                )
            )
        );
    }

    private static UuidNullableRow ManagerId(string source)
    {
        return new UuidNullableRow(
            new ExactUuidNullableRow(
                new FieldUuidNullable(source, "employee_manager_id")
            )
        );
    }
}
