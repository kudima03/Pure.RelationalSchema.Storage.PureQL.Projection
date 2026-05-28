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
using PureQL.CSharp.Model.Scalars;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Helpers;

internal sealed record TestEnvironment(
    ISchema Schema,
    IStoredSchemaDataSet Dataset,
    ITable Table,
    IColumn First,
    IColumn Second,
    string SchemaName,
    string TableName,
    string Entity
);

internal static class QueryHelpers
{
    internal static TestEnvironment NewEnv()
    {
        ISchema schema = new FakeSchema();
        IStoredSchemaDataSet dataset = new FakeStoredSchemaDataset(schema);
        ITable table = schema.Tables.First();
        IColumn first = table.Columns.First();
        IColumn second = table.Columns.Skip(1).First();
        string schemaName = schema.Name.TextValue;
        string tableName = table.Name.TextValue;
        return new TestEnvironment(
            schema,
            dataset,
            table,
            first,
            second,
            schemaName,
            tableName,
            $"{schemaName}.{tableName}"
        );
    }

    internal static SelectExpression SelectStringField(
        string entity,
        string fieldName
    )
    {
        return new SelectExpression(
            new ArrayReturning(
                new StringArrayReturning(new StringField(entity, fieldName))
            )
        );
    }

    internal static BooleanArrayReturning EachStringEqualsScalar(
        string entity,
        string fieldName,
        string value
    )
    {
        return new BooleanArrayReturning(
            new EachEquality(
                new EachStringEquality(
                    new StringArrayReturning(new StringField(entity, fieldName)),
                    OneOf<StringReturning, StringArrayReturning>.FromT0(
                        new StringReturning(new StringScalar(value))
                    )
                )
            )
        );
    }

    internal static OrderByItem OrderByString(
        string entity,
        string fieldName,
        SortDirection direction = SortDirection.Asc
    )
    {
        return new OrderByItem(
            new Field(new StringField(entity, fieldName)),
            direction
        );
    }
}
