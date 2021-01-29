using GraphQL.Types;
using Nekoyume.Action;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States.Models.Item;

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
                resolve: context => context.Source.Products.Values
            );
        }
    }
}
