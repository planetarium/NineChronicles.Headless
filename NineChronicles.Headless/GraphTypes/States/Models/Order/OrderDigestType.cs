using GraphQL.Types;
using Lib9c.Model.Order;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class OrderDigestType : OrderBaseType<OrderDigest>
    {
        public OrderDigestType()
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(OrderDigest.SellerAgentAddress),
                description: "Address of seller agent.",
                resolve: context => context.Source.SellerAgentAddress);
            Field<NonNullGraphType<StringGraphType>>(
                nameof(OrderDigest.Price),
                description: "Order price.",
                resolve: context => context.Source.Price.ToString());
            Field<NonNullGraphType<IntGraphType>>(
                nameof(OrderDigest.CombatPoint),
                resolve: context => context.Source.CombatPoint);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(OrderDigest.Level),
                resolve: context => context.Source.Level);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(OrderDigest.ItemId),
                description: "Id of item.",
                resolve: context => context.Source.ItemId);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(OrderDigest.ItemCount),
                description: "Count of item.",
                resolve: context => context.Source.ItemCount);
        }
    }
}
