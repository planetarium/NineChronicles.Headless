using System.Collections.Generic;
using System.Linq;
using GraphQL.Types;
using Libplanet.Types.Assets;
using Nekoyume.Model.Item;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.States.Models.Item;

namespace NineChronicles.Headless.GraphTypes.Abstractions
{
    public class StakeRewardsType2 : ObjectGraphType<(Dictionary<ItemBase, int> itemResult, List<FungibleAssetValue> favs)>
    {
        public StakeRewardsType2()
        {
            Field<ListGraphType<FungibleAssetValueType>>(
                "favs",
                resolve: context => context.Source.favs);
            Field<ListGraphType<InventoryItemType>>(
                "items",
                resolve: context =>
                {
                    var result = new List<Inventory.Item>();
                    foreach (var pair in context.Source.itemResult)
                    {
                        var item = new Inventory.Item(pair.Key, pair.Value);
                        result.Add(item);
                    }

                    return result;
                });
        }
    }
}
