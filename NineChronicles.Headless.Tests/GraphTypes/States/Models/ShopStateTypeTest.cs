using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using GraphQL.Execution;
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
        public async Task Query()
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
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["address"] = Addresses.Shop.ToString(),
                    ["products"] = new List<object>()
                },
                data
            );
        }

        [Theory]
        [MemberData(nameof(Members))]
        public async Task QueryWithArguments(int? id, string itemSubType, int? price, int expected)
        {
            var queryArgs = $"id: {id}";
            if (!(itemSubType is null))
            {
                queryArgs += $", itemSubType: {itemSubType}";
            }

            if (!(price is null))
            {
                queryArgs += $", maximumPrice: {price}";
            }
            var query = $@"query {{
                products({queryArgs}) {{
                    sellerAgentAddress
                    sellerAvatarAddress
                    price
                    itemUsable {{
                        itemId
                        itemType
                        itemSubType
                    }}
                    costume {{
                        itemId
                        itemType
                        itemSubType
                    }}
                }}
            }}";
            var queryResult = await ExecuteQueryAsync<ShopStateType>(query, source: Fixtures.ShopStateFX());
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var products = (IList)data["products"];
            Assert.Equal(expected, products.Count!);
        }

        [Theory]
        [InlineData("id: 1.0")]
        [InlineData("maximumPrice: 1.1")]
        [InlineData("itemSubType: Costume")]
        public async Task QueryWithInvalidArguments(string queryArgs)
        {
            var query = $@"{{
                address
                products({queryArgs}) {{
                    sellerAgentAddress
                    sellerAvatarAddress
                    price
                    itemUsable {{
                        itemId
                        itemType
                        itemSubType
                    }}
                    costume {{
                        itemId
                        itemType
                        itemSubType
                    }}
                }}
            }}";
            var queryResult = await ExecuteQueryAsync<ShopStateType>(query, source: new ShopState());
            Assert.Null(queryResult.Data);
            Assert.NotNull(queryResult.Errors);
            Assert.Single(queryResult.Errors!);
            Assert.Equal("ARGUMENTS_OF_CORRECT_TYPE", queryResult.Errors!.First().Code);
        }

        public static IEnumerable<object?[]> Members => new List<object?[]>
        {
            new object?[]
            {
                10110000,
                "Weapon",
                1,
                1,
            },
            new object?[]
            {
                10110000,
                null,
                1,
                1,
            },
            new object?[]
            {
                10110000,
                null,
                0,
                0,
            },
            new object?[]
            {
                0,
                null,
                0,
                0,
            },
            new object?[]
            {
                10110000,
                null,
                -1,
                0,
            },
        };
    }
}
