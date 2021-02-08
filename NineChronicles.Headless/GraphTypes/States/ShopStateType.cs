using System.Linq;
using GraphQL;
using GraphQL.Types;
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
            Field<NonNullGraphType<AddressType>>(
                nameof(ShopState.address),
                resolve: context => context.Source.address);
            Field<NonNullGraphType<ListGraphType<ShopItemType>>>(
                nameof(ShopState.Products),
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
                    }),
                resolve: context =>
                {
                    var products = context.Source.Products.Values;
                    var id = context.GetArgument<int?>("id");
                    if (!(id is null))
                    {
                        products = products
                            .Where(si => si.ItemUsable?.Id == id || si.Costume?.Id == id);
                    }
                    var subType = context.GetArgument<ItemSubType?>("itemSubType");
                    if (!(subType is null))
                    {
                        products = products
                            .Where(si => si.ItemUsable?.ItemSubType == subType || si.Costume?.ItemSubType == subType);
                    }
                    var maximumPrice = context.GetArgument<int?>("maximumPrice");
                    if (!(maximumPrice is null))
                    {
                        products = products
                            .Where(si => si.Price <= maximumPrice * si.Price.Currency);
                    }
                    return products.ToList();
                }
            );
        }
    }
}
