using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL;
using Libplanet;
using Libplanet.Action;
using Nekoyume;
using Nekoyume.Model.State;
using Nekoyume.TableData;
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
            var queryResult = await ExecuteQueryAsync<ShopStateType>(query, source: (shopState,
                (AccountStateGetter)CostumeStatSheetMock));
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["address"] = Addresses.Shop.ToString(),
                    ["products"] = new List<object>()
                },
                queryResult.Data
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
            var queryResult = await ExecuteQueryAsync<ShopStateType>(query, source: (Fixtures.ShopStateFX(),
                (AccountStateGetter)CostumeStatSheetMock));
            var products = queryResult.Data.As<Dictionary<string, object>>()["products"].As<List<object>>();
            Assert.Equal(expected, products.Count);
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
            var queryResult = await ExecuteQueryAsync<ShopStateType>(query,
                source: (new ShopState(), (AccountStateGetter) CostumeStatSheetMock));
            Assert.Null(queryResult.Data);
            Assert.Single(queryResult.Errors);
            Assert.Equal("ARGUMENTS_OF_CORRECT_TYPE", queryResult.Errors.First().Code);
        }

        [Theory]
        [InlineData(ShopSortingEnum.asc, ShopSortingEnum.asc, ShopSortingEnum.asc)]
        [InlineData(ShopSortingEnum.desc, ShopSortingEnum.desc, ShopSortingEnum.desc)]
        public async Task QueryWithSorting(ShopSortingEnum priceSorting, ShopSortingEnum gradeSorting, ShopSortingEnum cpSorting)
        {
            string queryArgs = $"price: {priceSorting}, grade: {gradeSorting}, combatPoint: {cpSorting}";
            var query = $@"{{
                address
                products({queryArgs}) {{
                    price
                    itemUsable {{
                        combatPoint
                        grade
                    }}
                    costume {{
                        combatPoint
                        grade
                    }}
                }}
            }}";

            var queryResult = await ExecuteQueryAsync<ShopStateType>(query, source: (Fixtures.ShopStateFX(),
                (AccountStateGetter)CostumeStatSheetMock));
            var products = queryResult.Data.As<Dictionary<string, object>>()["products"].As<List<object>>();
            Assert.NotEmpty(products);
            Dictionary<string, object> first = products.First().As<Dictionary<string, object>>();
            Dictionary<string, object> last = products.Last().As<Dictionary<string, object>>();
            int price = int.Parse(((string) first["price"]).Split("NCG")[0]);
            int price2 = int.Parse(((string) last["price"]).Split("NCG")[0]);
            Dictionary<string, object> item = first["itemUsable"] is null
                ? first["costume"].As<Dictionary<string, object>>()
                : first["itemUsable"].As<Dictionary<string, object>>();
            Dictionary<string, object> item2 = last["itemUsable"] is null
                ? last["costume"].As<Dictionary<string, object>>()
                : last["itemUsable"].As<Dictionary<string, object>>();
            int grade = (int) item["grade"];
            int grade2 = (int) item2["grade"];
            int cp = (int) item["combatPoint"];
            int cp2 = (int) item2["combatPoint"];

            if (priceSorting is ShopSortingEnum.asc)
            {
                Assert.True(price < price2);
            }
            else
            {
                Assert.True(price > price2);
            }

            if (gradeSorting is ShopSortingEnum.asc)
            {
                Assert.True(grade < grade2);
            }
            else
            {
                Assert.True(grade > grade2);
            }

            if (cpSorting is ShopSortingEnum.asc)
            {
                Assert.True(cp < cp2);
            }
            else
            {
                Assert.True(cp > cp2);
            }
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


        public static IValue? CostumeStatSheetMock(Address address)
        {
            Address sheetAddress = Addresses.GetSheetAddress<CostumeStatSheet>();
            return sheetAddress == address ? Fixtures.TableSheetsFX.CostumeStatSheet.Serialize() : null;
        }
    }
}
