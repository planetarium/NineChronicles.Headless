using GraphQL.Types;
using Nekoyume.Model.Item;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class ItemUsableType : ItemBaseType<ItemUsable>
    {
        public ItemUsableType() : base()
        {
            Field<NonNullGraphType<GuidGraphType>>(
                nameof(ItemUsable.ItemId),
                description: "Guid of item."
            );
        }
    }
}
