using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http.Headers;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Explorer.GraphTypes;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Battle;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.States.Models.Item;
using NineChronicles.Headless.GraphTypes.States.Models.Item.Enum;
using Org.BouncyCastle.Asn1.IsisMtt.X509;

namespace NineChronicles.Headless.GraphTypes.States
{
    public class ShopStateType : ObjectGraphType<(ShopState shopState, AccountStateGetter accountStateGetter)>
    {
        public ShopStateType()
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(ShopState.address),
                description: "Address of shop.",
                resolve: context => context.Source.shopState.address);
            Field<NonNullGraphType<ListGraphType<ShopItemType>>>(
                nameof(ShopState.Products),
                description: "List of ShopItem.",
                arguments: new QueryArguments(
                    new QueryArgument<IntGraphType>
                    {
                        Name = "id",
                        Description = "Filter for item id."
                    },
                    new QueryArgument<ItemSubTypeEnumType>
                    {
                        Name = "itemSubType",
                        Description = "Filter for ItemSubType. see from https://github.com/planetarium/lib9c/blob/main/Lib9c/Model/Item/ItemType.cs#L13"
                    },
                    new QueryArgument<IntGraphType>
                    {
                        Name = "maximumPrice",
                        Description = "Filter for item maximum price."
                    },
                    new QueryArgument<ShopSortingEnumType>
                    {
                        Name = "price",
                        Description = "Sorting by item price."
                    },
                    new QueryArgument<ShopSortingEnumType>
                    {
                        Name = "grade",
                        Description = "Sorting by item grade."
                    },
                    new QueryArgument<ShopSortingEnumType>
                    {
                        Name = "combatPoint",
                        Description = "Sorting by combat point."
                    }),
                resolve: context =>
                {
                    IEnumerable<ShopItem> products = context.Source.shopState.Products.Values;
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

                    IOrderedEnumerable<ShopItem>? orderedQuery = null;
                    Func<ShopItem, FungibleAssetValue>? orderByPrice = null;
                    Func<ShopItem, int>? orderByGrade = null;
                    Func<ShopItem, int>? orderByCP = null;
                    ShopSortingEnum? priceSorting = context.GetArgument<ShopSortingEnum?>("price");
                    ShopSortingEnum? gradeSorting = context.GetArgument<ShopSortingEnum?>("grade");
                    ShopSortingEnum? cpSorting = context.GetArgument<ShopSortingEnum?>("combatPoint");
                    if (!(priceSorting is null))
                    {
                        orderByPrice = p => p.Price;
                    }

                    if (!(gradeSorting is null))
                    {
                        orderByGrade = p => p.Costume?.Grade ?? p.ItemUsable.Grade;
                    }

                    if (!(cpSorting is null))
                    {
                        Address sheetAddress = Addresses.GetSheetAddress<CostumeStatSheet>();
                        if (context.Source.accountStateGetter(sheetAddress) is Text text)
                        {
                            CostumeStatSheet costumeStatSheet = new CostumeStatSheet();
                            costumeStatSheet.Set(text.Value);
                            orderByCP = p => p.Costume is null
                                ? CPHelper.GetCP(p.ItemUsable)
                                : CPHelper.GetCP(p.Costume, costumeStatSheet);
                        }
                    }

                    var shopItems = products.ToList();
                    orderedQuery = priceSorting switch
                    {
                        ShopSortingEnum.Asc => shopItems.OrderBy(orderByPrice),
                        ShopSortingEnum.Desc => shopItems.OrderByDescending(orderByPrice),
                        _ => orderedQuery
                    };

                    orderedQuery = gradeSorting switch
                    {
                        ShopSortingEnum.Asc => orderedQuery is null
                            ? shopItems.OrderBy(orderByGrade)
                            : orderedQuery.ThenBy(orderByGrade),
                        ShopSortingEnum.Desc => orderedQuery is null
                            ? shopItems.OrderByDescending(orderByGrade)
                            : orderedQuery.ThenByDescending(orderByGrade),
                        _ => orderedQuery
                    };

                    orderedQuery = cpSorting switch
                    {
                        ShopSortingEnum.Asc => orderedQuery is null
                            ? shopItems.OrderBy(orderByCP)
                            : orderedQuery.ThenBy(orderByCP),
                        ShopSortingEnum.Desc => orderedQuery is null
                            ? shopItems.OrderByDescending(orderByCP)
                            : orderedQuery.ThenByDescending(orderByCP),
                        _ => orderedQuery
                    };

                    return orderedQuery is null
                        ? shopItems.Select(product => (product, context.Source.accountStateGetter)).ToList()
                        : orderedQuery.Select(product => (product, context.Source.accountStateGetter)).ToList();
                }
            );
        }
    }
}
