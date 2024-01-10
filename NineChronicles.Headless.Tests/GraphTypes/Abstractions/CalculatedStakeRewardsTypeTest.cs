using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL.Execution;
using Lib9c;
using Libplanet.Types.Assets;
using Nekoyume.Model.Item;
using NineChronicles.Headless.GraphTypes.Abstractions;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.Abstractions
{
    public class CalculatedStakeRewardsTypeTest
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
                    count
                    fungibleItemId
                }
            }";
            var materialItemSheet = Fixtures.TableSheetsFX.MaterialItemSheet;
            var row = materialItemSheet.First;
            var item = ItemFactory.CreateMaterial(row);
            var fav = 1 * Currencies.Crystal;
            var itemResult = new Dictionary<ItemBase, int>
            {
                [item] = 2
            };
            var favs = new List<FungibleAssetValue>
            {
                fav,
                fav,
            };
            var queryResult = await ExecuteQueryAsync<CalculatedStakeRewardsType>(
                query,
                source: (itemResult, favs));
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.NotEmpty(data);
            Assert.Null(queryResult.Errors);
        }
    }
}
