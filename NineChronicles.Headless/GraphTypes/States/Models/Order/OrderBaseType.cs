using GraphQL.Types;
using Lib9c.Model.Order;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public abstract class OrderBaseType<T> : ObjectGraphType<T>
        where T : OrderBase
    {
        protected OrderBaseType()
        {
            Field<NonNullGraphType<GuidGraphType>>(nameof(OrderBase.OrderId))
                .Description("Guid of order.")
                .Resolve(context => context.Source.OrderId);
            Field<NonNullGraphType<GuidGraphType>>(nameof(OrderBase.TradableId))
                .Description("Tradable guid of order.")
                .Resolve(context => context.Source.TradableId);
            Field<NonNullGraphType<IntGraphType>>(nameof(OrderBase.StartedBlockIndex))
                .Description("Block index order started.");
            Field<NonNullGraphType<IntGraphType>>(nameof(OrderBase.ExpiredBlockIndex))
                .Description("Block index order expired.");
        }
    }
}
