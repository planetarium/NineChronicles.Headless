using GraphQL.Types;
using Nekoyume.Battle;
using Nekoyume.Model.Item;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class ItemUsableType : ItemBaseType<ItemUsable>
    {
        public ItemUsableType()
        {
            Field<NonNullGraphType<GuidGraphType>>(
                nameof(ItemUsable.ItemId),
                description: "Guid of item.",
                resolve: context => context.Source.itemBase.ItemId
            );

            Field<NonNullGraphType<IntGraphType>>(
                "CombatPoint",
                description: "Combat point of item.",
                resolve: context => CPHelper.GetCP(context.Source.itemBase));
        }
    }
}
