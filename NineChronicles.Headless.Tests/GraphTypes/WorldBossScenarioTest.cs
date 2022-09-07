using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL.Execution;
using Libplanet;
using Libplanet.Assets;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.GraphTypes.States;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class WorldBossScenarioTest
    {
        private readonly Address _avatarAddress;
        private readonly Address _raiderStateAddress;
        private readonly Address _worldBossAddress;
        private readonly Address _worldBossKillRewardRecordAddress;
        private readonly RaiderState _raiderState;
        private readonly StateContext _stateContext;
        private readonly WorldBossState _worldBossState;
        private readonly WorldBossKillRewardRecord _worldBossKillRewardRecord;

        public WorldBossScenarioTest()
        {
            _avatarAddress = new Address("4FcaCfCeC22717789Cb00b427b95B476BBAaA5b2");
            _raiderStateAddress = new Address("Bd9a12559be0F746Cade6272b6ACb1F1426C8c5D");
            _worldBossAddress = Addresses.GetWorldBossAddress(1);
            _worldBossKillRewardRecordAddress = new Address("0xE9653E92a5169bFbA66a4CbC07780ED370986d98");
            _stateContext = new StateContext(GetStatesMock, GetBalanceMock);
            _raiderState = new RaiderState
            {
                TotalScore = 2_000,
                HighScore = 1_000,
                TotalChallengeCount = 3,
                RemainChallengeCount = 2,
                LatestRewardRank = 1,
                ClaimedBlockIndex = 1L,
                RefillBlockIndex = 2L,
                PurchaseCount = 1,
                Cp = 3,
                Level = 4,
                IconId = 5,
                AvatarAddress = _avatarAddress,
                AvatarName = "avatar",
                LatestBossLevel = 1,
            };
            _worldBossState = new WorldBossState(List.Empty.Add("1").Add("2").Add(10).Add("2").Add("3"));
            _worldBossKillRewardRecord = new WorldBossKillRewardRecord
            {
                [1] = true,
                [2] = false,
            };
        }

        [Theory]
        [InlineData(true, 0L, false)]
        [InlineData(false, 0L, false)]
        [InlineData(true, 150L, true)]
        [InlineData(false, 150L, true)]
        public async Task RaiderState(bool stateExist, long blockIndex, bool prev)
        {
            int raidId = await GetRaidId(blockIndex, prev);

            // Find address.
            var addressQuery = $@"query {{
raiderAddress(avatarAddress: ""{_avatarAddress}"", raidId: {raidId})
}}";
            var addressQueryResult = await ExecuteQueryAsync<AddressQuery>(addressQuery);
            var addressData = (Dictionary<string, object>)((ExecutionNode)addressQueryResult.Data!).ToValue()!;
            Assert.Equal("0xBd9a12559be0F746Cade6272b6ACb1F1426C8c5D", addressData["raiderAddress"]);

            var raiderAddress = stateExist ? addressData["raiderAddress"] : default;
            // Get RaiderState.
            var stateQuery = $@"query {{
    raiderState(raiderAddress: ""{raiderAddress}"") {{
        totalScore
        highScore
        totalChallengeCount
        remainChallengeCount
        latestRewardRank
        claimedBlockIndex
        refillBlockIndex
        purchaseCount
        cp
        level
        iconId
        avatarAddress
        avatarName
        latestBossLevel
    }}
}}";

            var stateQueryResult = await ExecuteQueryAsync<StateQuery>(stateQuery, source: _stateContext);
            var raiderStateData =
                ((Dictionary<string, object>)((ExecutionNode)stateQueryResult.Data!).ToValue()!)[
                    "raiderState"];

            Assert.Equal(!stateExist, raiderStateData is null);
            if (stateExist)
            {
                var expectedData = new Dictionary<string, object>
                {
                    ["totalScore"] = 2_000,
                    ["highScore"] = 1_000,
                    ["totalChallengeCount"] = 3,
                    ["remainChallengeCount"] = 2,
                    ["latestRewardRank"] = 1,
                    ["claimedBlockIndex"] = 1L,
                    ["refillBlockIndex"] = 2L,
                    ["purchaseCount"] = 1,
                    ["cp"] = 3,
                    ["level"] = 4,
                    ["iconId"] = 5,
                    ["avatarAddress"] = _avatarAddress.ToString(),
                    ["avatarName"] = "avatar",
                    ["latestBossLevel"] = 1,
                };
                Assert.Equal(expectedData, raiderStateData);
            }
        }

        [Theory]
        [InlineData(true, 0L, false)]
        [InlineData(false, 0L, false)]
        [InlineData(true, 150L, true)]
        [InlineData(false, 150L, true)]
        public async Task WorldBossState(bool stateExist, long blockIndex, bool prev)
        {
            int raidId = await GetRaidId(blockIndex, prev);
            var expectedAddress = Addresses.GetWorldBossAddress(raidId);

            // Find address.
            var addressQuery = $@"query {{ worldBossAddress(raidId: {raidId}) }}";
            var addressQueryResult = await ExecuteQueryAsync<AddressQuery>(addressQuery);
            var addressData = (Dictionary<string, object>)((ExecutionNode)addressQueryResult.Data!).ToValue()!;
            Assert.Equal(expectedAddress.ToString(), addressData["worldBossAddress"]);
            var worldBossAddress = stateExist ? addressData["worldBossAddress"] : default;

            // Get RaiderState.
            var stateQuery = $@"query {{
    worldBossState(bossAddress: ""{worldBossAddress}"") {{
        id
        level
        currentHp
        startedBlockIndex
        endedBlockIndex
    }}
}}";

            var stateQueryResult = await ExecuteQueryAsync<StateQuery>(stateQuery, source: _stateContext);
            var worldBossStateData =
                ((Dictionary<string, object>)((ExecutionNode)stateQueryResult.Data!).ToValue()!)[
                    "worldBossState"];

            Assert.Equal(!stateExist, worldBossStateData is null);
            if (stateExist)
            {
                var expectedData = new Dictionary<string, object>
                {
                    ["id"] = 1,
                    ["level"] = 2,
                    ["currentHp"] = (BigInteger)10,
                    ["startedBlockIndex"] = 2L,
                    ["endedBlockIndex"] = 3L,
                };
                Assert.Equal(expectedData, worldBossStateData);
            }
        }

        [Theory]
        [InlineData(true, 0L, false)]
        [InlineData(false, 0L, false)]
        [InlineData(true, 150L, true)]
        [InlineData(false, 150L, true)]
        public async Task WorldBossKillRewardRecord(bool stateExist, long blockIndex, bool prev)
        {
            int raidId = await GetRaidId(blockIndex, prev);

            // Find address.
            var addressQuery = $@"query {{
worldBossKillRewardRecordAddress(avatarAddress: ""{_avatarAddress}"", raidId: {raidId})
}}";
            var addressQueryResult = await ExecuteQueryAsync<AddressQuery>(addressQuery);
            var addressData = (Dictionary<string, object>)((ExecutionNode)addressQueryResult.Data!).ToValue()!;
            Assert.Equal("0xE9653E92a5169bFbA66a4CbC07780ED370986d98", addressData["worldBossKillRewardRecordAddress"]);

            var worldBossKillRewardRecordAddress = stateExist ? addressData["worldBossKillRewardRecordAddress"] : default;
            // Get WorldBossKillRewardRecord.
            var stateQuery = $@"query {{
    worldBossKillRewardRecord(worldBossKillRewardRecordAddress: ""{worldBossKillRewardRecordAddress}"") {{        
        map {{
            bossLevel
            claimed
        }}
    }}
}}";

            var stateQueryResult = await ExecuteQueryAsync<StateQuery>(stateQuery, source: _stateContext);
            var stateData =
                ((Dictionary<string, object>)((ExecutionNode)stateQueryResult.Data!).ToValue()!)[
                    "worldBossKillRewardRecord"];
            Assert.Equal(!stateExist, stateData is null);
            if (stateExist)
            {
                var expectedData = new Dictionary<string, object>
                {
                    ["map"] = new List<Dictionary<string, object>>
                    {
                        new()
                        {
                            ["bossLevel"] = 1,
                            ["claimed"] = true,
                        },
                        new()
                        {
                            ["bossLevel"] = 2,
                            ["claimed"] = false,
                        },
                    }
                };
                Assert.Equal(expectedData, stateData);
            }
        }
        private IValue? GetStateMock(Address address)
        {
            if (address.Equals(_raiderStateAddress))
            {
                return _raiderState.Serialize();
            }

            if (address.Equals(Addresses.GetSheetAddress<WorldBossListSheet>()))
            {
                return @"id,boss_id,started_block_index,ended_block_index,fee,ticket_price,additional_ticket_price,max_purchase_count
1,205005,0,100,300,200,100,10
2,205005,200,300,300,200,100,10
".Serialize();
            }

            if (address.Equals(_worldBossAddress))
            {
                return _worldBossState.Serialize();
            }

            if (address.Equals(_worldBossKillRewardRecordAddress))
            {
                return _worldBossKillRewardRecord.Serialize();
            }

            return null;
        }

        private IReadOnlyList<IValue?> GetStatesMock(IReadOnlyList<Address> addresses) =>
            addresses.Select(GetStateMock).ToArray();

        private FungibleAssetValue GetBalanceMock(Address address, Currency currency)
        {
            return FungibleAssetValue.FromRawValue(currency, 0);
        }

        private async Task<int> GetRaidId(long blockIndex, bool prev)
        {
            // Get RaidId.
            var queryArgs = $"blockIndex: {blockIndex}";
            if (prev)
            {
                queryArgs += ", prev: true";
            }
            var raidIdQuery = @$"query {{ raidId({queryArgs}) }}";
            var raidIdQueryResult = await ExecuteQueryAsync<StateQuery>(raidIdQuery, source: _stateContext);
            var raidIdData = (Dictionary<string, object>)((ExecutionNode)raidIdQueryResult.Data!).ToValue()!;
            var raidId = raidIdData["raidId"];
            Assert.Equal(1, raidId);

            return 1;
        }
    }
}
