using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using Nekoyume.Model.Item;
using NineChronicles.Headless.GraphTypes.States.Models.Item;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;


namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class InventoryTypeTest
    {
        [Fact]
        public async Task Query()
        {
            const string query = @"
            {
                items(inventoryItemId: 700000)
                {
                    count
                    id
                    itemType
                }
            }";
            
            var row = Fixtures.TableSheetsFX.MaterialItemSheet.Values.Single(r => r.Id == 700000);
            var item = ItemFactory.CreateMaterial(row);
            Inventory inventory = Fixtures.AvatarStateFX.inventory;
            inventory.AddFungibleItem(item);
            
            ExecutionResult queryResult = await ExecuteQueryAsync<InventoryType>(query, source: inventory);
            Assert.Null(queryResult.Errors);
        }
    }
}
