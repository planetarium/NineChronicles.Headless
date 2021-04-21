using System.Collections.Generic;
using System.Threading.Tasks;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.States;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class StakingStateTypeTest
    {
        [Theory]
        [InlineData(3, false)]
        [InlineData(4, true)]
        public async Task Query(long rewardLevel, bool end)
        {
            const string query = @"{
                address
                level
                expiredBlockIndex
                startedBlockIndex
                receivedBlockIndex
                rewardLevel
                end
                rewardMap {
                    avatarAddress
                    rewards {
                        itemId
                        quantity
                    }
                }
                rewardLevelMap {
                    itemId
                    quantity
                }
            }";
            StakingState state = new StakingState(default, 1, 2, Fixtures.TableSheetsFX.StakingRewardSheet);
            var ri = new StakingRewardSheet.RewardInfo("1", "1", "1");
            var result = new StakingResult(default, default, new List<StakingRewardSheet.RewardInfo>
            {
                ri
            });
            state.UpdateRewardMap(rewardLevel, result, 4);

            var queryResult = await ExecuteQueryAsync<StakingStateType>(query, source: state);
            var expected = new Dictionary<string, object>
            {
                ["address"] = state.address.ToString(),
                ["level"] = 1L,
                ["expiredBlockIndex"] = 160002L,
                ["startedBlockIndex"] = 2L,
                ["receivedBlockIndex"] = 4L,
                ["rewardLevel"] = rewardLevel,
                ["end"] = end,
                ["rewardMap"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["avatarAddress"] = result.avatarAddress.ToString(),
                        ["rewards"] = new List<object>
                        {
                            new Dictionary<string, object>
                            {
                                ["itemId"] = 1,
                                ["quantity"] = 1,
                            }
                        }
                    }
                },
                ["rewardLevelMap"] = new List<object>
                {
                    new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["itemId"] = 400000,
                            ["quantity"] = 200,
                        },
                    },
                    new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["itemId"] = 400000,
                            ["quantity"] = 200,
                        },
                    },
                    new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["itemId"] = 400000,
                            ["quantity"] = 200,
                        },
                    },
                    new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["itemId"] = 400000,
                            ["quantity"] = 200,
                        },
                    },
                }
            };
            Assert.Equal(expected, queryResult.Data);
        }
    }
}
