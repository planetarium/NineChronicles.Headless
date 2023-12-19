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
            Field<ListGraphType<FungibleItemType>>(
                "items",
                resolve: context =>
                {
                    var result = new List<IFungibleItem>();
                    foreach (var pair in context.Source.itemResult)
                    {
                        for (int i = 0; i < pair.Value; i++)
                        {
                            result.Add((IFungibleItem)pair.Key);
                        }
                    }

                    return result;
                });
        }
    }
}
