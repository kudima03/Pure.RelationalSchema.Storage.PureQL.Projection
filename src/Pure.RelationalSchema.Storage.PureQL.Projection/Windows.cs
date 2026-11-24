using System.Linq.Expressions;
using PureQL.CSharp.Model;
using PureQL.CSharp.Model.Literals;
using PureQL.CSharp.Model.Parameters;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// Window functions of a plain query. Each is computed by a pass over the
// query's rows before its select, PureQLWindows.Over, which gives every row
// a window: the rows of its partition, ordered, and of them those of its
// frame. An aggregate folds the frame's rows; a ranking function reads the
// row's position; previous and next read the operand in another row of the
// partition, or the default, evaluated in the current row, where there is
// none. Partition keys, sort keys, operands and aggregate bodies are row
// expressions, so window functions do not nest.
internal static class Windows
{
    public static Expression RowNumber(object _, Scope scope)
    {
        return Expression.Property(scope.Window(), "RowNumber");
    }

    public static Expression Rank(object _, Scope scope)
    {
        return Expression.Property(scope.Window(), "Rank");
    }

    public static Expression DenseRank(object _, Scope scope)
    {
        return Expression.Property(scope.Window(), "DenseRank");
    }

    public static Expression PercentRank(object _, Scope scope)
    {
        return Expression.Property(scope.Window(), "PercentRank");
    }

    public static Expression CumulativeDistribution(object _, Scope scope)
    {
        return Expression.Property(scope.Window(), "CumulativeDistribution");
    }

    public static Expression Ntile(object node, Scope scope)
    {
        return Functions.Call(
            scope.Window(),
            "Ntile",
            Typed.As(scope.Compile(ModelNode.Child(node, "Buckets")), typeof(long))
        );
    }

    public static Expression Previous(object node, Scope scope)
    {
        return Shifted(node, scope, true);
    }

    public static Expression Next(object node, Scope scope)
    {
        return Shifted(node, scope, false);
    }

    // Whether a node is a window function: an aggregate over a window, a
    // ranking function, previous or next.
    public static bool IsFunction(object node)
    {
        return node is IOperator
            && ModelNode.Optional(node, "Over") is object over
            && over is not AggregateOver;
    }

    // The frame of a window function's window: rows counted, a range of the
    // one sort key, or the whole partition.
    public static Expression Frame(object over, Scope scope)
    {
        if (ModelNode.Optional(over, "Frame") is object frame)
        {
            return Expression.Call(
                Functions.Method(
                    typeof(PureQLFrame),
                    nameof(PureQLFrame.Rows),
                    typeof(long?),
                    typeof(long?)
                ),
                Typed.As(Distance(frame, "Preceding", scope).Amount, typeof(long?)),
                Typed.As(Distance(frame, "Following", scope).Amount, typeof(long?))
            );
        }

        if (ModelNode.Optional(over, "Range") is not object range)
        {
            return Expression.Property(
                null,
                typeof(PureQLFrame),
                nameof(PureQLFrame.Partition)
            );
        }

        (Expression Amount, PureQLRangeUnit Unit) preceding = Distance(
            range,
            "Preceding",
            scope
        );
        (Expression Amount, PureQLRangeUnit Unit) following = Distance(
            range,
            "Following",
            scope
        );

        return Expression.Call(
            Functions.Method(
                typeof(PureQLFrame),
                nameof(PureQLFrame.Range),
                typeof(bool),
                typeof(decimal?),
                typeof(PureQLRangeUnit),
                typeof(decimal?),
                typeof(PureQLRangeUnit)
            ),
            Expression.Constant(
                (SortDirection)
                    ModelNode.Child(ModelNode.Children(over, "OrderBy").Single(), "Direction")
                    == SortDirection.Desc
            ),
            preceding.Amount,
            Expression.Constant(preceding.Unit),
            following.Amount,
            Expression.Constant(following.Unit)
        );
    }

    // A side of a frame or of a range: no rows (or only the rows that share
    // the key) when it is absent, every row for "all" (null), a number, a
    // parameter, or a number of days, months or seconds.
    private static (Expression Amount, PureQLRangeUnit Unit) Distance(
        object frame,
        string side,
        Scope scope
    )
    {
        return Distance(ModelNode.Optional(frame, side), PureQLRangeUnit.Number, scope);
    }

    private static (Expression Amount, PureQLRangeUnit Unit) Distance(
        object? distance,
        PureQLRangeUnit unit,
        Scope scope
    )
    {
        return distance switch
        {
            null => (Expression.Constant(0m, typeof(decimal?)), unit),
            All => (Expression.Constant(null, typeof(decimal?)), unit),
            long number => (Expression.Constant((decimal)number, typeof(decimal?)), unit),
            IParameter or ILiteral => (
                Typed.As(scope.Compile(distance), typeof(decimal?)),
                unit
            ),
            _ => ModelNode.Optional(distance, "Days") is object days
                ? Distance(days, PureQLRangeUnit.Days, scope)
            : ModelNode.Optional(distance, "Months") is object months
                ? Distance(months, PureQLRangeUnit.Months, scope)
            : Distance(
                ModelNode.Child(distance, "Seconds"),
                PureQLRangeUnit.Seconds,
                scope
            ),
        };
    }

    // The operand in the row offset positions before or after the current
    // one, or the default, evaluated in the current row, where the partition
    // has no such row; null without a default.
    private static Expression Shifted(object node, Scope scope, bool before)
    {
        Type type = ValueTypes.Clr(((IOperator)node).Type);
        Expression window = scope.Window();
        Expression distance = ModelNode.Optional(node, "Offset") is object offset
            ? Typed.As(scope.Compile(offset), typeof(long))
            : Expression.Constant(1L);
        Expression shift = before ? Expression.Negate(distance) : distance;

        return Expression.Condition(
            Functions.Call(window, "Has", shift),
            Typed.As(
                scope
                    .Row(Functions.Call(window, "At", shift))
                    .Compile(ModelNode.Child(node, "Operand")),
                type
            ),
            ModelNode.Optional(node, "Default") is object fallback
                ? Typed.As(
                    scope.Row(Expression.Property(window, "Row")).Compile(fallback),
                    type
                )
                : Typed.Null(type)
        );
    }
}
