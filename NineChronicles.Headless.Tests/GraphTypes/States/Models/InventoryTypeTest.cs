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
        [Theory]
        [InlineData(100000)]
        [InlineData(700000)]
        public async Task Query(int inventoryItemId)
        {
            string query = $@"
            {{
                items(inventoryItemId: {inventoryItemId})
                {{
                    count
                    id
                    itemType
                }}
            }}";

            var row = Fixtures.TableSheetsFX.MaterialItemSheet.Values.Single(r => r.Id == inventoryItemId);
            var item = ItemFactory.CreateMaterial(row);
            Inventory inventory = Fixtures.AvatarStateFX.inventory;
            inventory.AddFungibleItem(item);

            ExecutionResult queryResult = await ExecuteQueryAsync<InventoryType>(query, source: inventory);
            Assert.Null(queryResult.Errors);
        }
    }
}
