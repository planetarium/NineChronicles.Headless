using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Execution;
using GraphQL.NewtonsoftJson;
using Nekoyume.Model.Item;
using NineChronicles.Headless.GraphTypes.States.Models.Item;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class ShopItemTypeTest
    {
        [Theory]
        [MemberData(nameof(Members))]
        public async Task Query(
            ItemUsable itemUsable,
            Costume costume,
            Dictionary<string, object> itemUsableDict,
            Dictionary<string, object> costumeDict
        )
        {
            const string query = @"
            {
                sellerAgentAddress
                sellerAvatarAddress
                price
                itemUsable {
                    itemId
                    itemType
                    itemSubType
                }
                costume {
                    itemId
                    itemType
                    itemSubType
                }
            }";

            var itemId = new Guid("220acb43-095e-46f6-9725-4223c69827e8");
            ShopItem shopItem;
            if (costume is null)
            {
                shopItem = new ShopItem(Fixtures.UserAddress, Fixtures.AvatarAddress,
                    new Guid("d3d9ac06-eb91-4cc4-863a-5b4769ad633e"), 100 * Fixtures.CurrencyFX, itemUsable);
            }
            else
            {
                shopItem = new ShopItem(Fixtures.UserAddress, Fixtures.AvatarAddress,
                    new Guid("d3d9ac06-eb91-4cc4-863a-5b4769ad633e"), 100 * Fixtures.CurrencyFX, costume);
            }

            var expected = new Dictionary<string, object>
            {
                ["sellerAgentAddress"] = "0xfc2a412ea59122B114B672a5518Bc113955Dd2FE",
                ["sellerAvatarAddress"] = "0x983c3Fbfe8243a0e36D55C6C1aE26A7c8Bb6CBd4",
                ["price"] = "100 NCG",
                ["itemUsable"] = itemUsableDict,
                ["costume"] = costumeDict,
            };

            var queryResult = await ExecuteQueryAsync<ShopItemType>(query, source: shopItem);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var expectedItemUsable = (Dictionary<string, object>)expected["itemUsable"];
            var dataItemUsable = (Dictionary<string, object>)data["itemUsable"];
            var expectedCostume = (Dictionary<string, object>)expected["costume"];
            var dataCostume = (Dictionary<string, object>)data["costume"];
            Assert.Equal(expected["sellerAgentAddress"], data["sellerAgentAddress"]);
            Assert.Equal(expected["sellerAvatarAddress"], data["sellerAvatarAddress"]);
            Assert.Equal(expected["price"], data["price"]);
            if (itemUsableDict != null)
            {
                Assert.Equal(expectedItemUsable["itemId"].ToString(), dataItemUsable["itemId"].ToString());
                Assert.Equal(expectedItemUsable["itemType"].ToString(), dataItemUsable["itemType"].ToString());
                Assert.Equal(expectedItemUsable["itemSubType"].ToString(), dataItemUsable["itemSubType"].ToString());
            }

            if (costumeDict != null)
            {
                Assert.Equal(expectedCostume["itemId"].ToString(), dataCostume["itemId"].ToString());
                Assert.Equal(expectedCostume["itemType"].ToString(), dataCostume["itemType"].ToString());
                Assert.Equal(expectedCostume["itemSubType"].ToString(), dataCostume["itemSubType"].ToString());
            }
        }

        public static IEnumerable<object?[]> Members => new List<object?[]>
        {
            new object?[]
            {
                Equipment(),
                null,
                new Dictionary<string, object>
                {
                    ["itemId"] = new Guid("220acb43-095e-46f6-9725-4223c69827e8"),
                    ["itemType"] = ItemType.Equipment.ToString().ToUpper(),
                    ["itemSubType"] = ItemSubType.Weapon.ToString().ToUpper(),
                },
                null,
            },
            new object?[]
            {
                null,
                Costume(),
                null,
                new Dictionary<string, object>
                {
                    ["itemId"] = new Guid("220acb43-095e-46f6-9725-4223c69827e8"),
                    ["itemType"] = ItemType.Costume.ToString().ToUpper(),
                    ["itemSubType"] = "FULL_COSTUME",
                },
            },
        };

        private static ItemUsable Equipment()
        {
            var row = Fixtures.TableSheetsFX.EquipmentItemSheet.OrderedList.First();
            return ItemFactory.CreateItemUsable(row, new Guid("220acb43-095e-46f6-9725-4223c69827e8"), 0);
        }

        private static Costume Costume()
        {
            var row = Fixtures.TableSheetsFX.CostumeItemSheet.OrderedList.First();
            return ItemFactory.CreateCostume(row, new Guid("220acb43-095e-46f6-9725-4223c69827e8"));
        }
    }
}
