using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL.Execution;
using Libplanet;
using Libplanet.Assets;
using Nekoyume;
using Nekoyume.Model.EnumType;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.GraphTypes.States;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class RuneScenarioTest
    {
        private readonly Address _runeListSheetAddress;
        private readonly Address _avatarAddress;
        private readonly StateContext _stateContext;

        public RuneScenarioTest()
        {
            _runeListSheetAddress = Addresses.GetSheetAddress<RuneListSheet>();
            _avatarAddress = new Address("4FcaCfCeC22717789Cb00b427b95B476BBAaA5b2");
            _stateContext = new StateContext(GetStatesMock, GetBalanceMock, 1L);
        }

        [Fact]
        public async Task Find_Rune_And_Equip()
        {
            // Get RuneList.
            var runeListQuery = $@"query {{
                    runeList {{
                        id
                        grade
                        runeType
                        requiredLevel
                        usePlace
                    }}
                }}";
            var runeListQueryResult =
                await ExecuteQueryAsync<StateQuery>(runeListQuery, source: _stateContext);
            var runeList = (object[])((Dictionary<string, object>)((ExecutionNode)runeListQueryResult.Data!)
                .ToValue()!)["runeList"];
            var rune = (Dictionary<string, object>)runeList.First();
            var runeId = (int)rune["id"];
            Assert.Equal(1001, runeId);

            // Find address.
            var addressQuery = $@"query {{
runeStateAddress(avatarAddress: ""{_avatarAddress}"", runeId: {runeId})
}}";
            var addressQueryResult = await ExecuteQueryAsync<AddressQuery>(addressQuery);
            var addressData = (Dictionary<string, object>)((ExecutionNode)addressQueryResult.Data!).ToValue()!;
            var runeStateAddress = addressData["runeStateAddress"];
            Assert.Equal("0x3b266772DE100238fDedC30DB12e54e5422Fd5B2", runeStateAddress);

            // Find RuneState.
            var stateQuery = $@"query {{
runeState(runeStateAddress: ""{runeStateAddress}"") {{
    runeId
    level
    }}
}}";
            var stateQueryResult =
                await ExecuteQueryAsync<StateQuery>(stateQuery, source: _stateContext);
            var runeState = ((Dictionary<string, object>)((ExecutionNode)stateQueryResult.Data!)
                .ToValue()!)["runeState"];
            Assert.Equal(new Dictionary<string, int>
            {
                ["runeId"] = runeId,
                ["level"] = 1,
            }, runeState);

            // Find RuneSlotState address.
            var runeSlotStateAddressQuery = $@"query {{
runeSlotStateAddress(avatarAddress: ""{_avatarAddress}"", battleType: RAID)
}}";
            var runeSlotStateAddressQueryResult = await ExecuteQueryAsync<AddressQuery>(runeSlotStateAddressQuery);
            var runeSlotStateAddressData = (Dictionary<string, object>)((ExecutionNode)runeSlotStateAddressQueryResult.Data!).ToValue()!;
            var runeSlotStateAddress = runeSlotStateAddressData["runeSlotStateAddress"];
            Assert.Equal("0x92dE06F3E9D701C2740Ee850E0D6B35084F8B574", runeSlotStateAddress);

            // Find RuneSlotState.
            var runeSlotStateQuery = $@"query {{
runeSlotState(runeSlotStateAddress: ""{runeSlotStateAddress}"") {{
    battleType
    slots {{
        index
        runeType
        isLock
        }}
    }}
}}";
            var runeSlotStateQueryResult =
                await ExecuteQueryAsync<StateQuery>(runeSlotStateQuery, source: _stateContext);
            var runeSlotState = (Dictionary<string, object>)((Dictionary<string, object>)((ExecutionNode)runeSlotStateQueryResult.Data!)
                .ToValue()!)["runeSlotState"];
            Assert.Equal("RAID", runeSlotState["battleType"]);

            var slots = (object[])runeSlotState["slots"];
            var slot = slots
                .First(i => ((Dictionary<string, object>)i)["runeType"] == rune["runeType"] && ((Dictionary<string, object>)i)["isLock"].Equals(false));
            var index = ((Dictionary<string, object>)slot)["index"];
            Assert.Equal(0, index);
            Assert.Equal(6, slots.Length);
        }

        [Theory]
        [InlineData(BattleType.Adventure, "0x05Cd944B81a893Fa434A578665058BfC366e978e", "ADVENTURE")]
        [InlineData(BattleType.Arena, "0x6DDAF8E11f2A1694f130bb1E6Fb136f5cBa0fEc8", "ARENA")]
        [InlineData(BattleType.Raid, "0x92dE06F3E9D701C2740Ee850E0D6B35084F8B574", "RAID")]
        public async Task Find_RuneSlot(BattleType battleType, string expectedAddress, string expectedType)
        {
            // Find address.
            var addressQuery = $@"query {{
runeSlotStateAddress(avatarAddress: ""{_avatarAddress}"", battleType: {battleType})
}}";
            var addressQueryResult = await ExecuteQueryAsync<AddressQuery>(addressQuery);
            var addressData = (Dictionary<string, object>)((ExecutionNode)addressQueryResult.Data!).ToValue()!;
            var runeSlotStateAddress = addressData["runeSlotStateAddress"];
            Assert.Equal(expectedAddress, runeSlotStateAddress);

            // Find RuneSlotState.
            var stateQuery = $@"query {{
runeSlotState(runeSlotStateAddress: ""{runeSlotStateAddress}"") {{
    battleType
    slots {{
        index
        runeType
        isLock
        }}
    }}
}}";
            var stateQueryResult =
                await ExecuteQueryAsync<StateQuery>(stateQuery, source: _stateContext);
            var runeSlotState = (Dictionary<string, object>)((Dictionary<string, object>)((ExecutionNode)stateQueryResult.Data!)
                .ToValue()!)["runeSlotState"];
            Assert.Equal(expectedType, runeSlotState["battleType"]);

            var slots = (object[])runeSlotState["slots"];
            Assert.Equal(6, slots.Length);
        }
        private IValue? GetStateMock(Address address)
        {
            if (address.Equals(_runeListSheetAddress))
            {
                return @"id,_name,grade,rune_type,required_level,use_place
3001,Adventurer's Rune,1,1,1,7
1001,Rune of Health (Fenrir),2,1,1,4
1002,Rune of Attack (Fenrir),3,1,1,5
1003,Rune of Bleeding (Fenrir),5,2,1,7
2001,GoldLeaf Rune,5,2,1,7".Serialize();
            }

            if (address.Equals(RuneState.DeriveAddress(_avatarAddress, 1001)))
            {
                var runeState = new RuneState(1001);
                runeState.LevelUp();
                return runeState.Serialize();
            }

            if (address.Equals(RuneSlotState.DeriveAddress(_avatarAddress, BattleType.Adventure)))
            {
                return new RuneSlotState(BattleType.Adventure).Serialize();
            }

            if (address.Equals(RuneSlotState.DeriveAddress(_avatarAddress, BattleType.Arena)))
            {
                return new RuneSlotState(BattleType.Arena).Serialize();
            }

            if (address.Equals(RuneSlotState.DeriveAddress(_avatarAddress, BattleType.Raid)))
            {
                return new RuneSlotState(BattleType.Raid).Serialize();
            }

            return null;
        }

        private IReadOnlyList<IValue?> GetStatesMock(IReadOnlyList<Address> addresses) =>
            addresses.Select(GetStateMock).ToArray();

        private FungibleAssetValue GetBalanceMock(Address address, Currency currency)
        {
            return FungibleAssetValue.FromRawValue(currency, 0);
        }
    }
}
