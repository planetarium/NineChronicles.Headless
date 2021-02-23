using GraphQL.Types;
using Nekoyume.Model.Item;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class ShopItemType : ObjectGraphType<ShopItem>
    {
        public ShopItemType()
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(ShopItem.SellerAgentAddress),
                description: "Address of seller agent.",
                resolve: context => context.Source.SellerAgentAddress);
            Field<NonNullGraphType<AddressType>>(
                nameof(ShopItem.SellerAvatarAddress),
                description: "Address of seller avatar.",
                resolve: context => context.Source.SellerAvatarAddress);
            Field<NonNullGraphType<GuidGraphType>>(
                nameof(ShopItem.ProductId),
                description: "Guid of shop registered.",
                resolve: context => context.Source.ProductId);
            Field<NonNullGraphType<StringGraphType>>(
                nameof(ShopItem.Price),
                description: "Item price.",
                resolve: context => context.Source.Price
            );
            Field<ItemUsableType>(
                nameof(ShopItem.ItemUsable),
                description: "Equipment / Consumable information.",
                resolve: context => context.Source.ItemUsable
            );
            Field<CostumeType>(
                nameof(ShopItem.Costume),
                description: "Costume information.",
                resolve: context => context.Source.Costume
            );
        }
    }
}
