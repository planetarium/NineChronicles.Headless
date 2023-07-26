using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Execution;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Nekoyume.Action;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class AddressQueryTest
    {
        private const string MinerPrivateKeyHex = "b8ce43967d7270348906c3b30efd41c30ab834ce07a36ee8ac5fd52cb7a3f579";
        private const string NcgMinterAddress = "0x055D75489A163a5Ee9D2744e52dae1F598CA1817";
        private readonly StandaloneContext _standaloneContext;

        public AddressQueryTest()
        {
            var minerPrivateKey = new PrivateKey(MinerPrivateKeyHex);
            var initializeStates = new InitializeStates(
                    rankingState: new RankingState0(),
                    shopState: new ShopState(),
                    gameConfigState: new GameConfigState(),
                    redeemCodeState: new RedeemCodeState(Bencodex.Types.Dictionary.Empty
                        .Add("address", RedeemCodeState.Address.Serialize())
                        .Add("map", Bencodex.Types.Dictionary.Empty)
                    ),
                    adminAddressState: new AdminState(new PrivateKey().ToAddress(), 1500000),
                    activatedAccountsState: new ActivatedAccountsState(),
#pragma warning disable CS0618
                    // Use of obsolete method Currency.Legacy(): https://github.com/planetarium/lib9c/discussions/1319
                    goldCurrencyState:
                    new GoldCurrencyState(Currency.Legacy("NCG", 2, minerPrivateKey.ToAddress())),
#pragma warning restore CS0618
                    goldDistributions: Array.Empty<GoldDistribution>(),
                    tableSheets: new Dictionary<string, string>(),
                    pendingActivationStates: new PendingActivationState[] { }
                );
            _standaloneContext = CreateStandaloneContext(initializeStates, minerPrivateKey);
        }

        [Theory]
        [InlineData(CurrencyEnum.CRYSTAL)]
        [InlineData(CurrencyEnum.NCG, NcgMinterAddress)]
        public async Task CurrencyMintersAddress(
            CurrencyEnum currency,
            params string[] expectedAddresses)
        {
            var query =
                $"{{ currencyMintersAddress(currency: {currency}) }}";
            var result = await ExecuteQueryAsync<AddressQuery>(
                query,
                standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!)
                .ToValue()!;
            var objectList = (object[]?)data["currencyMintersAddress"];
            if (objectList is null)
            {
                Assert.Empty(expectedAddresses);
            }
            else
            {
                var addressList = objectList.Select(o => (string)o).ToArray();
                Assert.Equal(expectedAddresses, addressList);
            }
        }

        [Fact]
        public async Task PledgeAddress()
        {
            var address = new Address("0x8ff5e1c64860af7d88b019837a378fbbec75c7d9");
            var query =
                $"{{ pledgeAddress(agentAddress: \"{address}\") }}";
            var result = await ExecuteQueryAsync<AddressQuery>(
                query,
                standaloneContext: _standaloneContext);
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!)
                .ToValue()!;
            Assert.Equal("0xdfEA67DEB20E2e2Bb3AcB6Bc7C858Cb7De3deB78", data["pledgeAddress"]);
        }
    }
}
