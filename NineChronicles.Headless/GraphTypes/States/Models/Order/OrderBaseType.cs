using GraphQL.Types;
using Lib9c.Model.Order;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public abstract class OrderBaseType<T> : ObjectGraphType<T>
        where T : OrderBase
    {
        protected OrderBaseType()
        {
            Field<NonNullGraphType<GuidGraphType>>(
                nameof(NonFungibleOrder.OrderId),
                description: "Guid of order.");
            Field<NonNullGraphType<GuidGraphType>>(
                nameof(NonFungibleOrder.TradableId),
                description: "Tradable guid of order.");
            Field<NonNullGraphType<IntGraphType>>(
                nameof(NonFungibleOrder.StartedBlockIndex),
                description: "Block index order started.");
            Field<NonNullGraphType<IntGraphType>>(
                nameof(NonFungibleOrder.ExpiredBlockIndex),
                description: "Block index order expired.");
        }
    }
}
