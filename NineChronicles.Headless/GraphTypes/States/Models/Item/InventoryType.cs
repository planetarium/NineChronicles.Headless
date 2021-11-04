using System.Collections.Generic;
using System.Linq;
using GraphQL;
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
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<InventoryItemType>>>>(
                nameof(Inventory.Items),
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<IntGraphType>>
                {
                    Name = "inventoryItemId",
                    Description = "An Id to find Inventory Item"
                }),
                description: "List of Inventory Item.",
                resolve: context =>
                {
                    IReadOnlyList<Inventory.Item>? items = context.Source.Items;
                    int Id = context.GetArgument<int>("inventoryItemId");

                    return items.Where(i => i.item.Id == Id);
                }
            );
        }
    }
}
