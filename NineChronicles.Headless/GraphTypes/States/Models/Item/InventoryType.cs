using GraphQL.Types;
using Nekoyume.Model.Item;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class InventoryType : ObjectGraphType<Inventory>
    {
        public InventoryType()
        {
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<ConsumableType>>>>(
                nameof(Inventory.Consumables),
                description: "List of Consumable."
            );
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<MaterialType>>>>(
                nameof(Inventory.Materials),
                description: "List of Material."
            );
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<EquipmentType>>>>(
                nameof(Inventory.Equipments),
                description: "List of Equipment."
            );
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<CostumeType>>>>(
                nameof(Inventory.Costumes),
                description: "List of Costumez."
            );
        }
    }
}
