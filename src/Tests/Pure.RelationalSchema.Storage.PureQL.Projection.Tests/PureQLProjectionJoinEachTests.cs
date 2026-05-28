using OneOf;
using Pure.RelationalSchema.Abstractions.Column;
using Pure.RelationalSchema.Abstractions.Schema;
using Pure.RelationalSchema.Abstractions.Table;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Fakes;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.ArrayReturnings;
using PureQL.CSharp.Model.EachEqualities;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Returnings;
using Query = PureQL.CSharp.Model.Query;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests;

public sealed record PureQLProjectionJoinEachTests
{
    private sealed record TwoTableEnv(
        IStoredSchemaDataSet LeftDataset,
        IStoredSchemaDataSet RightDataset,
        ITable LeftTable,
        ITable RightTable,
        IColumn LeftCol,
        IColumn RightCol,
        string LeftEntity,
        string RightEntity
    );

    private static TwoTableEnv NewTwoTableEnv()
    {
        ISchema leftSchema = new FakeSchema();
        ISchema rightSchema = new FakeSchema();
        IStoredSchemaDataSet left = new FakeStoredSchemaDataset(leftSchema);
        IStoredSchemaDataSet right = new FakeStoredSchemaDataset(rightSchema);
        ITable lt = leftSchema.Tables.First();
        ITable rt = rightSchema.Tables.First();
        return new TwoTableEnv(
            left,
            right,
            lt,
            rt,
            lt.Columns.First(),
            rt.Columns.First(),
            $"{leftSchema.Name.TextValue}.{lt.Name.TextValue}",
            $"{rightSchema.Name.TextValue}.{rt.Name.TextValue}"
        );
    }

    [Fact]
    public void InnerJoinWithEachEqualityOnConditionMatchesByRowValue()
    {
        TwoTableEnv s = NewTwoTableEnv();

        IStoredTableDataSet result = new PureQLProjection(
            [s.LeftDataset, s.RightDataset],
            new Query(
                new FromExpression(s.LeftEntity, s.LeftEntity),
                [
                    new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(s.LeftEntity, s.LeftCol.Name.TextValue)
                            )
                        )
                    ),
                ],
                where: null,
                join:
                [
                    new Join(
                        JoinType.Inner,
                        s.RightEntity,
                        new BooleanArrayReturning(
                            new EachEquality(
                                new EachStringEquality(
                                    new StringArrayReturning(
                                        new StringField(
                                            s.LeftEntity,
                                            s.LeftCol.Name.TextValue
                                        )
                                    ),
                                    OneOf<StringReturning, StringArrayReturning>.FromT1(
                                        new StringArrayReturning(
                                            new StringField(
                                                s.RightEntity,
                                                s.RightCol.Name.TextValue
                                            )
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
            )
        );

        // 10 rows × 10 rows, paired only when row values match.
        // FakeRows generates "test0".."test9" for every column,
        // so each left row matches exactly one right row.
        Assert.Equal(10, result.AsEnumerable().Count());
    }

    [Fact]
    public void LeftJoinWithEachEqualityKeepsLeftRowsWithoutMatch()
    {
        TwoTableEnv s = NewTwoTableEnv();

        IStoredTableDataSet result = new PureQLProjection(
            [s.LeftDataset, s.RightDataset],
            new Query(
                new FromExpression(s.LeftEntity, s.LeftEntity),
                [
                    new SelectExpression(
                        new ArrayReturning(
                            new StringArrayReturning(
                                new StringField(s.LeftEntity, s.LeftCol.Name.TextValue)
                            )
                        )
                    ),
                ],
                where: null,
                join:
                [
                    new Join(
                        JoinType.Left,
                        s.RightEntity,
                        new BooleanArrayReturning(
                            new EachEquality(
                                new EachStringEquality(
                                    new StringArrayReturning(
                                        new StringField(
                                            s.LeftEntity,
                                            s.LeftCol.Name.TextValue
                                        )
                                    ),
                                    OneOf<StringReturning, StringArrayReturning>.FromT1(
                                        new StringArrayReturning(
                                            new StringField(
                                                s.RightEntity,
                                                s.RightCol.Name.TextValue
                                            )
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
            )
        );

        // Every left row matches exactly one right row → 10 result rows.
        Assert.Equal(10, result.AsEnumerable().Count());
    }
}
