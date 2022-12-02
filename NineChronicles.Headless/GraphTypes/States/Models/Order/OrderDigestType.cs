using GraphQL.Types;
using Lib9c.Model.Order;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class OrderDigestType : OrderBaseType<OrderDigest>
    {
        public OrderDigestType()
        {
            Field<NonNullGraphType<AddressType>>(nameof(OrderDigest.SellerAgentAddress))
                .Description("Address of seller agent.")
                .Resolve(context => context.Source.SellerAgentAddress);
            Field<NonNullGraphType<StringGraphType>>(nameof(OrderDigest.Price))
                .Description("Order price.")
                .Resolve(context => context.Source.Price.ToString());
            Field<NonNullGraphType<IntGraphType>>(nameof(OrderDigest.CombatPoint))
                .Resolve(context => context.Source.CombatPoint);
            Field<NonNullGraphType<IntGraphType>>(nameof(OrderDigest.Level))
                .Resolve(context => context.Source.Level);
            Field<NonNullGraphType<IntGraphType>>(nameof(OrderDigest.ItemId))
                .Description("Id of item.")
                .Resolve(context => context.Source.ItemId);
            Field<NonNullGraphType<IntGraphType>>(nameof(OrderDigest.ItemCount))
                .Description("Count of item.")
                .Resolve(context => context.Source.ItemCount);
        }
    }
}
