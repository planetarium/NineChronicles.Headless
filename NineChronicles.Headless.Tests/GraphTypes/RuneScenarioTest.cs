using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL.Execution;
using Libplanet;
using Libplanet.Assets;
using Nekoyume;
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
        public async Task Find_Rune()
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
