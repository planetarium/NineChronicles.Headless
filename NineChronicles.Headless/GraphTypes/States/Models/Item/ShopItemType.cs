using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.Item;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class ShopItemType : ObjectGraphType<ShopItem>
    {
        public ShopItemType()
        {
            Field<NonNullGraphType<AddressType>>(nameof(ShopItem.SellerAgentAddress))
                .Description("Address of seller agent.")
                .Resolve(context => context.Source.SellerAgentAddress);
            Field<NonNullGraphType<AddressType>>(nameof(ShopItem.SellerAvatarAddress))
                .Description("Address of seller avatar.")
                .Resolve(context => context.Source.SellerAvatarAddress);
            Field<NonNullGraphType<GuidGraphType>>(nameof(ShopItem.ProductId))
                .Description("Guid of product registered.")
                .Resolve(context => context.Source.ProductId);
            Field<NonNullGraphType<StringGraphType>>(nameof(ShopItem.Price))
                .Description("Item price.")
                .Resolve(context => context.Source.Price.ToString());
            Field<ItemUsableType>(nameof(ShopItem.ItemUsable))
                .Description("Equipment / Consumable information.")
                .Resolve(context => context.Source.ItemUsable);
            Field<CostumeType>(nameof(ShopItem.Costume))
                .Description("Costume information.")
                .Resolve(context => context.Source.Costume);
        }
    }
}
