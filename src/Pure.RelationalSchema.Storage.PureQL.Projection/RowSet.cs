using System.Linq.Expressions;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// The rows an aggregate folds when they are not the query's or a group's:
// the rows of a window. Element is the type of one of them, Row the scope a
// row expression of the query compiles in against one of them, and Window
// the window itself, which ranking functions and previous / next read.
internal sealed record RowSet(
    Expression Rows,
    Type Element,
    Func<Expression, Scope> Row,
    Expression Window
);
