using Pure.RelationalSchema.ColumnType;
using Pure.RelationalSchema.Storage.Abstractions;
using Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Data;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Fields;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.ProjectionExpressions;
using PureQL.CSharp.Model.RowExpressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection.Tests.Semantics;

// A field reference declares the nullability the field has, and the cells
// are read as declared: a nullable field holds null for an empty cell, the
// storage layer's NULL; a non-null field holds the value its text parses to,
// so the tree carries a value a SQL provider can hash and index on, and an
// empty cell, which the declaration rules out, fails the parse (an empty
// string cell is the empty string, which is what SQL reads too).
public sealed class NullabilityTests
{
    private static readonly IStoredSchemaDataSet Items = new InlineTable(
        "t",
        "items",
        ("id", new LongColumnType()),
        ("name", new StringColumnType())
    ).With(["1", "pen"], ["", ""], ["3", "pad"]);

    [Fact]
    public void FieldDeclaredNullableReadsAnEmptyCellAsNull()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                select:
                [
                    new SelectItemProjection("id", new FieldIntegerNullable("i", "id")),
                ]
            )
        );

        Assert.Equal(["1", "", "3"], ResultColumn.Texts(projection, "id"));
    }

    [Fact]
    public void FieldDeclaredNonNullFailsOnAnEmptyCell()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                select: [new SelectItemProjection("id", new FieldInteger("i", "id"))]
            )
        );

        _ = Assert.Throws<FormatException>(() => projection.ToList());
    }

    [Fact]
    public void StringFieldDeclaredNonNullReadsAnEmptyCellAsTheEmptyString()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                where: new EqualStringRow(
                    new FieldString("i", "name"),
                    new LiteralString("")
                ),
                select: [new SelectItemProjection("name", new FieldString("i", "name"))]
            )
        );

        Assert.Equal([""], ResultColumn.Texts(projection, "name"));
    }

    [Fact]
    public void StringFieldDeclaredNullableReadsAnEmptyCellAsNullNotTheEmptyString()
    {
        PureQLProjection projection = new(
            [Items],
            new MainPlainQueryWithoutPagination(
                from: new FromEntity("t.items", "i"),
                where: new EqualStringRow(
                    new FieldStringNullable("i", "name"),
                    new LiteralString("")
                ),
                select:
                [
                    new SelectItemProjection(
                        "name",
                        new FieldStringNullable("i", "name")
                    ),
                ]
            )
        );

        Assert.Empty(ResultColumn.Texts(projection, "name"));
    }
}
