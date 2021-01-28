using System.Collections.Generic;
using System.Threading.Tasks;
using Nekoyume;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class ShopStateTypeTest
    {
        [Fact]
        public async Task QueryShopState()
        {
            const string query = @"{
                address
                products {
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
                }
            }";
            var shopState = new ShopState();
            var queryResult = await ExecuteQueryAsync<ShopStateType>(query, source: shopState);
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["address"] = Addresses.Shop.ToString(),
                    ["products"] = new List<object>()
                },
                queryResult.Data
            );
        }
    }
}
