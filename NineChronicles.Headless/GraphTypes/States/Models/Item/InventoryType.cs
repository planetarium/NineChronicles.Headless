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
                nameof(Inventory.Consumables))
                .Description("List of Consumables.")
                .Resolve(ctx => ctx.Source.Consumables);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<MaterialType>>>>(
                nameof(Inventory.Materials))
                .Description("List of Materials.")
                .Resolve(ctx => ctx.Source.Materials);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<EquipmentType>>>>(
                nameof(Inventory.Equipments))
                .Description("List of Equipments.")
                .Resolve(ctx => ctx.Source.Equipments);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<CostumeType>>>>(
                nameof(Inventory.Costumes))
                .Description("List of Costumes.")
                .Resolve(ctx => ctx.Source.Costumes);
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<InventoryItemType>>>>(
                nameof(Inventory.Items))
                .Argument<NonNullGraphType<IntGraphType>>(
                    "inventoryItemId",
                    "An Id to find Inventory Item")
                .Description("List of Inventory Item.")
                .Resolve(context =>
                {
                    IReadOnlyList<Inventory.Item>? items = context.Source.Items;
                    int Id = context.GetArgument<int>("inventoryItemId");

                    return items.Where(i => i.item.Id == Id);
                });
        }
    }
}
