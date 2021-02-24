using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.Item;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class ShopItemType : ObjectGraphType<ShopItem>
    {
        public ShopItemType()
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(ShopItem.SellerAgentAddress),
                resolve: context => context.Source.SellerAgentAddress);
            Field<NonNullGraphType<AddressType>>(
                nameof(ShopItem.SellerAvatarAddress),
                resolve: context => context.Source.SellerAvatarAddress);
            Field<NonNullGraphType<GuidGraphType>>(
                nameof(ShopItem.ProductId),
                resolve: context => context.Source.ProductId);
            Field<NonNullGraphType<StringGraphType>>(
                nameof(ShopItem.Price),
                resolve: context => context.Source.Price
            );
            Field<ItemUsableType>(
                nameof(ShopItem.ItemUsable),
                resolve: context => context.Source.ItemUsable
            );
            Field<CostumeType>(
                nameof(ShopItem.Costume),
                resolve: context => context.Source.Costume
            );
        }
    }
}
