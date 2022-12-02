using System.Collections.Generic;
using System.Linq;
using GraphQL;
using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States.Models.Item;
using NineChronicles.Headless.GraphTypes.States.Models.Item.Enum;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class ShopStateType : ObjectGraphType<ShopState>
    {
        public ShopStateType()
        {
            Field<NonNullGraphType<AddressType>>(nameof(ShopState.address))
                .Description("Address of shop.")
                .Resolve(context => context.Source.address);
            Field<NonNullGraphType<ListGraphType<ShopItemType>>>(nameof(ShopState.Products))
                .Description("List of ShopItem.")
                .Argument<int?>("id", true, "Filter for item id.")
                .Argument<ItemSubType?>(
                    "itemSubType",
                    true,
                    "Filter for ItemSubType. see from https://github.com/planetarium/lib9c/blob/main/Lib9c/Model/Item/ItemType.cs#L13")
                .Argument<int?>("maximumPrice", true, "Filter for item maximum price.")
                .Resolve(context =>
                {
                    IEnumerable<ShopItem> products = context.Source.Products.Values;
                    if (context.GetArgument<int?>("id") is int id)
                    {
                        products = products
                            .Where(si => si.ItemUsable?.Id == id || si.Costume?.Id == id);
                    }

                    if (context.GetArgument<ItemSubType?>("itemSubType") is ItemSubType subType)
                    {
                        products = products
                            .Where(si => si.ItemUsable?.ItemSubType == subType || si.Costume?.ItemSubType == subType);
                    }
                    if (context.GetArgument<int?>("maximumPrice") is int maximumPrice)
                    {
                        products = products
                            .Where(si => si.Price <= maximumPrice * si.Price.Currency);
                    }
                    return products.ToList();
                });
        }
    }
}
