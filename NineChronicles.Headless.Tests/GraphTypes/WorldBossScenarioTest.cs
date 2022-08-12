using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Bencodex.Types;
using GraphQL.Execution;
using Libplanet;
using Libplanet.Assets;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.GraphTypes.States;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class WorldBossScenarioTest
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task RaiderState(bool stateExist)
        {
            var avatarAddress = new Address("4FcaCfCeC22717789Cb00b427b95B476BBAaA5b2");
            var raidId = 1;

            // Find address.
            var addressQuery = $@"query {{
raiderAddress(avatarAddress: ""{avatarAddress}"", raidId: {raidId})
}}";
            var addressQueryResult = await ExecuteQueryAsync<AddressQuery>(addressQuery);
            var addressData = (Dictionary<string, object>) ((ExecutionNode) addressQueryResult.Data!).ToValue()!;
            var raiderAddress = addressData["raiderAddress"];
            Assert.Equal("0xBd9a12559be0F746Cade6272b6ACb1F1426C8c5D", raiderAddress);

            // Get RaiderState.
            const string stateQuery = @"query {
    raiderState(raiderAddress: ""0xBd9a12559be0F746Cade6272b6ACb1F1426C8c5D"") {
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
        avatarNameWithHash
        latestBossLevel
    }
}";
            var raiderState = new RaiderState
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
                AvatarAddress = avatarAddress,
                AvatarNameWithHash = "avatar",
                LatestBossLevel = 1,
            };

            IValue? GetStateMock(Address address)
            {
                return stateExist ? raiderState.Serialize() : null;
            }

            IReadOnlyList<IValue?> GetStatesMock(IReadOnlyList<Address> addresses) =>
                addresses.Select(GetStateMock).ToArray();

            FungibleAssetValue GetBalanceMock(Address address, Currency currency)
            {
                return FungibleAssetValue.FromRawValue(currency, 0);
            }

            var stateContext = new StateContext(GetStatesMock, GetBalanceMock);
            var stateQueryResult = await ExecuteQueryAsync<StateQuery>(stateQuery, source: stateContext);
            var raiderStateData =
                ((Dictionary<string, object>) ((ExecutionNode) stateQueryResult.Data!).ToValue()!)[
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
                    ["avatarAddress"] = avatarAddress.ToString(),
                    ["avatarNameWithHash"] = "avatar",
                    ["latestBossLevel"] = 1,
                };
                Assert.Equal(expectedData, raiderStateData);
            }
        }
    }
}
