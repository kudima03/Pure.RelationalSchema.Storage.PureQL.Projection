using PureQL.CSharp.Model;

namespace Pure.RelationalSchema.Storage.PureQL.Projection;

// One orderBy item of either context.
internal sealed record OrderKey(object Expression, SortDirection Direction)
{
    public static OrderKey Of(OrderItemProjection item)
    {
        return new OrderKey(item.Expression, item.Direction);
    }

    public static OrderKey Of(OrderItemGroup item)
    {
        return new OrderKey(item.Expression, item.Direction);
    }
}
