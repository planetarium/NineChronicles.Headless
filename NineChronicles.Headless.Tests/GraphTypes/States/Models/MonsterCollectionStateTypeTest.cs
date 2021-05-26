using System.Collections.Generic;
using System.Threading.Tasks;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.States;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class MonsterCollectionStateTypeTest
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
                totalRewards {
                    itemId
                    quantity
                }
                claimableBlockIndex
            }";
            MonsterCollectionState state = new MonsterCollectionState(default, 1, 2, Fixtures.TableSheetsFX.MonsterCollectionRewardSheet);
            var ri = new MonsterCollectionRewardSheet.RewardInfo("1", "1", "1");
            var result = new MonsterCollectionResult(default, default, new List<MonsterCollectionRewardSheet.RewardInfo>
            {
                ri
            });
            state.UpdateRewardMap(rewardLevel, result, 4);

            var queryResult = await ExecuteQueryAsync<MonsterCollectionStateType>(query, source: state);
            var expected = new Dictionary<string, object>
            {
                ["address"] = state.address.ToString(),
                ["level"] = 1L,
                ["expiredBlockIndex"] = 201602L,
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
                            ["quantity"] = 80,
                        },
                    },
                    new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["itemId"] = 400000,
                            ["quantity"] = 80,
                        },
                    },
                    new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["itemId"] = 400000,
                            ["quantity"] = 80,
                        },
                    },
                    new List<object>
                    {
                        new Dictionary<string, object>
                        {
                            ["itemId"] = 400000,
                            ["quantity"] = 80,
                        },
                    },
                },
                ["totalRewards"] = new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["itemId"] = 400000,
                        ["quantity"] = 80 * (int) rewardLevel,
                    },
                },
                ["claimableBlockIndex"] = 50404L,
            };
            Assert.Equal(expected, queryResult.Data);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(1)]
        [InlineData(2)]
        [InlineData(3)]
        [InlineData(4)]
        public async Task Query_With_RewardLevel(int rewardLevel)
        {
            string query = $@"{{
                totalRewards(rewardLevel: {rewardLevel}) {{
                    itemId
                    quantity
                }}
            }}";
            MonsterCollectionState state = new MonsterCollectionState(default, 1, 2, Fixtures.TableSheetsFX.MonsterCollectionRewardSheet);
            var ri = new MonsterCollectionRewardSheet.RewardInfo("1", "1", "1");
            var result = new MonsterCollectionResult(default, default, new List<MonsterCollectionRewardSheet.RewardInfo>
            {
                ri
            });
            state.UpdateRewardMap(rewardLevel, result, 4);

            var queryResult = await ExecuteQueryAsync<MonsterCollectionStateType>(query, source: state);
            Assert.Null(queryResult.Errors);
            var totalRewards = new List<object>();
            if (rewardLevel > 0)
            {
                totalRewards.Add(new Dictionary<string, object>
                    {
                        ["itemId"] = 400000,
                        ["quantity"] = 80 * rewardLevel,
                    }
                );
            }

            var expected = new Dictionary<string, object>
            {
                ["totalRewards"] = totalRewards,
            };
            Assert.Equal(expected, queryResult.Data);
        }
    }
}
