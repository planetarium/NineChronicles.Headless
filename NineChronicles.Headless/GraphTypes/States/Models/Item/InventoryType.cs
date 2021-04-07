using System.Linq;
using GraphQL.Types;
using Libplanet.Action;
using Nekoyume.Model.Item;

namespace NineChronicles.Headless.GraphTypes.States.Models.Item
{
    public class InventoryType : ObjectGraphType<(Inventory inventory, AccountStateGetter accountStateGetter)>
    {
        public InventoryType()
        {
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<ConsumableType>>>>(
                nameof(Inventory.Consumables),
                description: "List of Consumables.",
                resolve: context =>
                {
                    return context.Source.inventory.Consumables
                        .Select(c => (c, context.Source.accountStateGetter))
                        .ToList();
                }
            );
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<MaterialType>>>>(
                nameof(Inventory.Materials),
                description: "List of Materials.",
                resolve: context =>
                {
                    return context.Source.inventory.Materials
                        .Select(c => (c, context.Source.accountStateGetter))
                        .ToList();
                }
            );
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<EquipmentType>>>>(
                nameof(Inventory.Equipments),
                description: "List of Equipments.",
                resolve: context =>
                {
                    return context.Source.inventory.Equipments
                        .Select(c => (c, context.Source.accountStateGetter))
                        .ToList();
                }
            );
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<CostumeType>>>>(
                nameof(Inventory.Costumes),
                description: "List of Costumes.",
                resolve: context =>
                {
                    return context.Source.inventory.Costumes
                        .Select(c => (c, context.Source.accountStateGetter))
                        .ToList();
                }
            );
        }
    }
}
