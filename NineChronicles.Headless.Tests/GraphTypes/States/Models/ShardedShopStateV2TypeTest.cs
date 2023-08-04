using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Execution;
using Libplanet.Crypto;
using Nekoyume.Model.Item;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class ShardedShopStateV2TypeTest
    {
        private readonly Address _stateAddress;
        private readonly ShardedShopStateV2 _state;

        public ShardedShopStateV2TypeTest()
        {
            _stateAddress = ShardedShopStateV2.DeriveAddress(ItemSubType.Weapon, "0");
            _state = new ShardedShopStateV2(_stateAddress);
        }

        [Fact]
        public async Task Query()
        {
            const string query = @"{
                address
                orderDigestList {
                    sellerAgentAddress
                    itemId
                    price
                    combatPoint
                    expiredBlockIndex
                }
            }";
            var queryResult = await ExecuteQueryAsync<ShardedShopStateV2Type>(query, source: _state);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["address"] = _stateAddress.ToString(),
                    ["orderDigestList"] = new List<object>()
                },
                data
            );
        }

        [Theory]
        [MemberData(nameof(Members))]
        public async Task QueryWithArguments(int id, int? price, int expected)
        {
            var queryArgs = $"id: {id}";
            if (!(price is null))
            {
                queryArgs += $", maximumPrice: {price}";
            }
            var query = $@"query {{
                orderDigestList({queryArgs}) {{
                    sellerAgentAddress
                    itemId
                    price
                    combatPoint
                    expiredBlockIndex
                }}
            }}";
            var queryResult = await ExecuteQueryAsync<ShardedShopStateV2Type>(query, source: Fixtures.ShardedWeapon0ShopStateV2FX());
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var orderDigestList = (IList)data["orderDigestList"];
            Assert.Equal(expected, orderDigestList.Count!);
        }

        [Theory]
        [InlineData("id: 1.0")]
        [InlineData("maximumPrice: 1.1")]
        public async Task QueryWithInvalidArguments(string queryArgs)
        {
            var query = $@"{{
                address
                orderDigestList({queryArgs}) {{
                    sellerAgentAddress
                    itemId
                    price
                    combatPoint
                    expiredBlockIndex
                }}
            }}";
            var queryResult = await ExecuteQueryAsync<ShardedShopStateV2Type>(query, source: _state);
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
                null,
                2,
            },
            new object?[]
            {
                10110000,
                1,
                1,
            },
            new object?[]
            {
                10110000,
                2,
                2,
            },
            new object?[]
            {
                0,
                0,
                0,
            },
            new object?[]
            {
                10110000,
                -1,
                0,
            },
        };
    }
}
