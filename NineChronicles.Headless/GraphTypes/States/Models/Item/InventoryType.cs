using System;
using System.Collections.Generic;
using System.Linq;
using GraphQL;
using GraphQL.Types;
using Nekoyume.Model.Item;
using NineChronicles.Headless.GraphTypes.States.Models.Item.Enum;

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
                description: "List of Equipments.",
                arguments: new QueryArguments(
                    new QueryArgument<BooleanGraphType>
                    {
                        Name = "equipped",
                        Description = "filter equipped inventory item"
                    },
                    new QueryArgument<ItemSubTypeEnumType>
                    {
                        Name = "itemSubType",
                        Description = "An item subtype for fetching only equipment where " +
                                      "its subtype is the same. If it wasn't given, you'll " +
                                      "get all equipment without relationship to the subtype."
                    },
                    new QueryArgument<ListGraphType<NonNullGraphType<GuidGraphType>>>
                    {
                        Name = "itemIds",
                        Description = "ItemIds for fetching only equipment where id is in " +
                                      "the given argument."
                    }),
                resolve: context =>
                {
                    var equipments = context.Source.Equipments;
                    var equippedFilter = context.GetArgument<bool?>("equipped");
                    var itemSubTypeFilter = context.GetArgument<ItemSubType?>("itemSubType");
                    var itemIdsFilter = context.GetArgument<Guid[]?>("itemIds");
                    if (equippedFilter.HasValue)
                    {
                        equipments = equipments.Where(x => x.equipped == equippedFilter.Value).ToList();
                    }

                    if (itemSubTypeFilter is not null)
                    {
                        equipments = equipments.Where(equipment => equipment.ItemSubType == itemSubTypeFilter);
                    }

                    if (itemIdsFilter is not null)
                    {
                        var set = itemIdsFilter.ToHashSet();
                        equipments = equipments.Where(equipment => set.Contains(equipment.ItemId));
                    }

                    return equipments;
                }
            );
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<CostumeType>>>>(
                nameof(Inventory.Costumes),
                description: "List of Costumes."
            );
            Field<NonNullGraphType<ListGraphType<NonNullGraphType<InventoryItemType>>>>(
                nameof(Inventory.Items),
                arguments: new QueryArguments(
                    new QueryArgument<IntGraphType>
                    {
                        Name = "inventoryItemId",
                        Description = "An Id to find Inventory Item"
                    },
                    new QueryArgument<BooleanGraphType>
                    {
                        Name = "locked",
                        Description = "filter locked Inventory Item"
                    }
                ),
                description: "List of Inventory Item.",
                resolve: context =>
                {
                    IReadOnlyList<Inventory.Item>? items = context.Source.Items;
                    var itemId = context.GetArgument<int?>("inventoryItemId");
                    var filter = context.GetArgument<bool?>("locked");
                    if (itemId.HasValue)
                    {
                        items = items.Where(i => i.item.Id == itemId.Value).ToList();
                    }
                    if (filter.HasValue)
                    {
                        items = items.Where(i => i.Locked == filter.Value).ToList();
                    }
                    return items;
                }
            );
        }
    }
}
