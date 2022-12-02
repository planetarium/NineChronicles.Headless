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
            Field<NonNullGraphType<AddressType>>(nameof(ShardedShopStateV2.address))
                .Description("Address of sharded shop.")
                .Resolve(context => context.Source.address);
            Field<NonNullGraphType<ListGraphType<OrderDigestType>>>(
                nameof(ShardedShopStateV2.OrderDigestList))
                .Description("List of OrderDigest.")
                .Argument<int?>("id", true, "Filter for item id.")
                .Argument<int?>("maximumPrice", true, "Filter for item maximum price.")
                .Resolve(context =>
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
                });
        }
    }
}
