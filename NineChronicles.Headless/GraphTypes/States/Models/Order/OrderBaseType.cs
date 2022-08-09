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
                nameof(OrderBase.OrderId),
                description: "Guid of order.",
                resolve: context => context.Source.OrderId);
            Field<NonNullGraphType<GuidGraphType>>(
                nameof(OrderBase.TradableId),
                description: "Tradable guid of order.",
                resolve: context => context.Source.TradableId);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(OrderBase.StartedBlockIndex),
                description: "Block index order started.");
            Field<NonNullGraphType<IntGraphType>>(
                nameof(OrderBase.ExpiredBlockIndex),
                description: "Block index order expired.");
        }
    }
}
