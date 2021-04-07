using GraphQL.Types;
using Libplanet.Action;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.Item;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class ShopItemType : ObjectGraphType<(ShopItem shopItem, AccountStateGetter accountStateGetter)>
    {
        public ShopItemType()
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(ShopItem.SellerAgentAddress),
                description: "Address of seller agent.",
                resolve: context => context.Source.shopItem.SellerAgentAddress);
            Field<NonNullGraphType<AddressType>>(
                nameof(ShopItem.SellerAvatarAddress),
                description: "Address of seller avatar.",
                resolve: context => context.Source.shopItem.SellerAvatarAddress);
            Field<NonNullGraphType<GuidGraphType>>(
                nameof(ShopItem.ProductId),
                description: "Guid of product registered.",
                resolve: context => context.Source.shopItem.ProductId);
            Field<NonNullGraphType<StringGraphType>>(
                nameof(ShopItem.Price),
                description: "Item price.",
                resolve: context => context.Source.shopItem.Price
            );
            Field<ItemUsableType>(
                nameof(ShopItem.ItemUsable),
                description: "Equipment / Consumable information.",
                resolve: context =>
                {
                    if (context.Source.shopItem.ItemUsable is null)
                    {
                        return null;
                    }

                    return (context.Source.shopItem.ItemUsable, context.Source.accountStateGetter);
                });
            Field<CostumeType>(
                nameof(ShopItem.Costume),
                description: "Costume information.",
                resolve: context =>
                {
                    if (context.Source.shopItem.Costume is null)
                    {
                        return null;
                    }

                    return (context.Source.shopItem.Costume, context.Source.accountStateGetter);
                });
        }
    }
}
