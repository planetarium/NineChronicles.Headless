using GraphQL.Types;
using Nekoyume.Model.Item;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class InventoryType : ObjectGraphType<Inventory>
    {
        public InventoryType()
        {
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<ConsumableType>>>>(nameof(Inventory.Consumables));
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<MaterialType>>>>(nameof(Inventory.Materials));
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<EquipmentType>>>>(nameof(Inventory.Equipments));
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<CostumeType>>>>(nameof(Inventory.Costumes));
        }
    }
}
