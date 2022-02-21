using System.Collections.Generic;
using System.Linq;
using GraphQL;
using GraphQL.Types;
using Lib9c.Model.Order;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States.Models.Item;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class ShardedShopStateV2Type : ObjectGraphType<ShardedShopStateV2>
    {
        public ShardedShopStateV2Type()
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(ShardedShopStateV2.address),
                description: "Address of sharded shop.",
                resolve: context => context.Source.address);
            Field<NonNullGraphType<ListGraphType<OrderDigestType>>>(
                nameof(ShardedShopStateV2.OrderDigestList),
                description: "List of OrderDigest.",
                arguments: new QueryArguments(
                    new QueryArgument<IntGraphType>
                    {
                        Name = "id",
                        Description = "Filter for item id."
                    },
                    new QueryArgument<IntGraphType>
                    {
                        Name = "maximumPrice",
                        Description = "Filter for item maximum price."
                    }),
                resolve: context =>
                {
                    IEnumerable<OrderDigest> orderDigestList = context.Source.OrderDigestList;
                    if (context.GetArgument<int?>("id") is int id)
                    {
                        orderDigestList = orderDigestList.Where(si => si.ItemId == id);
                    }
                    if (context.GetArgument<int?>("maximumPrice") is int maximumPrice)
                    {
                        orderDigestList = orderDigestList
                            .Where(si => si.Price <= maximumPrice * si.Price.Currency);
                    }
                    return orderDigestList.ToList();
                }
            );
        }
    }
}
