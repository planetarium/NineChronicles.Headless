using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Utilities;
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
                    item {{
                        id
                        grade
                        itemType
                        itemSubType
                        elementalType
                    }}
                }}
            }}";
            
            var row = Fixtures.TableSheetsFX.MaterialItemSheet.Values.Single(r => r.Id == inventoryItemId);
            var item = ItemFactory.CreateMaterial(row);
            Inventory inventory = Fixtures.AvatarStateFX.inventory;
            inventory.AddFungibleItem(item);
            
            ExecutionResult queryResult = await ExecuteQueryAsync<InventoryType>(query, source: inventory);
            Assert.Null(queryResult.Errors);
            var expected = new Dictionary<string, object>
            {
                ["items"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["count"] = 1,
                        ["id"] = row.Id,
                        ["itemType"] = row.ItemType.ToString().ToUpper(),
                        ["item"] = new Dictionary<string, object>
                        {
                            ["id"] = row.Id,
                            ["grade"] = row.Grade,
                            ["itemType"] = StringUtils.ToConstantCase(Enum.GetName(row.ItemType.GetType(), row.ItemType)),
                            ["itemSubType"] = StringUtils.ToConstantCase(Enum.GetName(row.ItemSubType.GetType(), row.ItemSubType)),
                            ["elementalType"] = StringUtils.ToConstantCase(Enum.GetName(row.ElementalType.GetType(), row.ElementalType)),
                        },
                    },
                },
            };
            Assert.Equal(expected, queryResult.Data.As<Dictionary<string, object>>());
        }
    }
}
