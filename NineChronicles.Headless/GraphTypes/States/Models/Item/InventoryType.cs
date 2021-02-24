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
                description: "List of Consumables."
            );
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<MaterialType>>>>(
                nameof(Inventory.Materials),
                description: "List of Materials."
            );
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<EquipmentType>>>>(
                nameof(Inventory.Equipments),
                description: "List of Equipments."
            );
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<CostumeType>>>>(
                nameof(Inventory.Costumes),
                description: "List of Costumes."
            );
        }
    }
}
