using GraphQL.Types;
using Nekoyume.Model.Item;
using NineChronicles.Headless.GraphTypes.States.Models.Item.Enum;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class ConsumableType : ItemBaseType<Consumable>
    {
        public ConsumableType()
        {
            Field<NonNullGraphType<GuidGraphType>>(nameof(Consumable.ItemId));
            Field<NonNullGraphType<StatTypeEnumType>>(nameof(Consumable.MainStat));
        }
    }
}
