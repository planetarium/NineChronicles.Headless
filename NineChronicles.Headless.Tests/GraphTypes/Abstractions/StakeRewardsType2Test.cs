using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Execution;
using Lib9c;
using Libplanet.Types.Assets;
using Nekoyume.Model.Item;
using NineChronicles.Headless.GraphTypes.Abstractions;
using NineChronicles.Headless.GraphTypes.States.Models.Table;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.Abstractions
{
    public class StakeRewardsType2Test
    {
        [Fact]
        public async Task Query()
        {
            const string query = @"
            {
                favs {
                    quantity
                    currency
                }
                items {
                    fungibleItemId
                }
            }";
            var mateiralItemSheet = Fixtures.TableSheetsFX.MaterialItemSheet;
            var row = mateiralItemSheet.First;
            var item = ItemFactory.CreateMaterial(row);
            var fav = 1 * Currencies.Crystal;
            var itemResult = new Dictionary<ItemBase, int>();
            itemResult[item] = 2;
            var favs = new List<FungibleAssetValue>
            {
                fav,
                fav,
            };
            var queryResult = await ExecuteQueryAsync<StakeRewardsType2>(
                query,
                source: (itemResult, favs));
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.NotEmpty(data);
            Assert.Null(queryResult.Errors);
        }
    }
}
