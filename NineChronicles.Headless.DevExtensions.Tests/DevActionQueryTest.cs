using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using GraphQL.Execution;
using Lib9c.DevExtensions.Action;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Nekoyume.Action;
using Nekoyume.Model.State;
using NineChronicles.Headless.DevExtensions.GraphTypes;
using Xunit;
using static NineChronicles.Headless.DevExtensions.Tests.GraphQLTestUtils;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.DevExtensions.Tests
{
    public class ActionQueryTest
    {
        private readonly Codec _codec;
        private readonly StandaloneContext _standaloneContext;

        public ActionQueryTest()
        {
            _codec = new Codec();

            var store = new DefaultStore(null);
            var stateStore = new TrieStateStore(new DefaultKeyValueStore(null));
            var minerPrivateKey = new PrivateKey();
            var genesisBlock = BlockChain<NCAction>.MakeGenesisBlock(
                new PolymorphicAction<ActionBase>[]
                {
                    new InitializeStates(
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
                    ),
                },
                privateKey: minerPrivateKey
            );
            var blockchain = new BlockChain<PolymorphicAction<ActionBase>>(
                new BlockPolicy<PolymorphicAction<ActionBase>>(),
                new VolatileStagePolicy<PolymorphicAction<ActionBase>>(),
                store,
                stateStore,
                genesisBlock);
            _standaloneContext = new StandaloneContext(blockchain, store);
        }

        [Theory]
        [InlineData(null, null, 0, 0)]
        [InlineData(null, 100, 0, 100)]
        [InlineData(100, null, 100, 0)]
        [InlineData(100, 10, 100, 10)]
        public async Task FaucetCurrency(int? faucetNcg, int? faucetCrystal, int expectedNcg, int expectedCrystal)
        {
            var agentAddress = new PrivateKey().ToAddress();
            var args = $"agentAddress: \"{agentAddress}\"";
            if (!(faucetNcg is null))
            {
                args += $", faucetNcg: {faucetNcg}";
            }

            if (!(faucetCrystal is null))
            {
                args += $", faucetCrystal: {faucetCrystal}";
            }

            var query = $"{{faucetCurrency({args})}}";
            var queryResult = await ExecuteQueryAsync<DevActionQuery>(query, standaloneContext: _standaloneContext);
            Assert.Null(queryResult.Errors);

            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["faucetCurrency"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<FaucetCurrency>(polymorphicAction.InnerAction);
            Assert.Equal(agentAddress, action.AgentAddress);
            Assert.Equal(expectedNcg, action.FaucetNcg);
            Assert.Equal(expectedCrystal, action.FaucetCrystal);
        }

        private class FaucetRuneInfoGenerator : IEnumerable<object[]>
        {
            private readonly List<object[]> _data = new List<object[]>
            {
                new object[]
                {
                    new List<FaucetRuneInfo>
                    {
                        new(10001, 10),
                    },
                },
                new object[]
                {
                    new List<FaucetRuneInfo>
                    {
                        new(10001, 10),
                        new(30001, 10),
                    },
                },
                new object[]
                {
                    new List<FaucetRuneInfo>
                    {
                        new(10001, 10),
                        new(10002, 10),
                        new(30001, 10),
                    },
                },
            };

            public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();
        }

        [Theory]
        [ClassData(typeof(FaucetRuneInfoGenerator))]
        public async Task FaucetRune(List<FaucetRuneInfo> faucetRuneInfos)
        {
            Address avatarAddress = new PrivateKey().ToAddress();
            var runeInfos = string.Empty;
            foreach (var faucetRuneInfo in faucetRuneInfos)
            {
                runeInfos += $"{{runeId: {faucetRuneInfo.RuneId}, amount: {faucetRuneInfo.Amount}}}";
            }

            var query = $"{{faucetRune (avatarAddress: \"{avatarAddress}\", faucetRuneInfos: [{runeInfos}])}}";
            var queryResult = await ExecuteQueryAsync<DevActionQuery>(query, standaloneContext: _standaloneContext);
            Assert.Null(queryResult.Errors);

            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string)data["faucetRune"]));
            Assert.IsType<Dictionary>(plainValue);
            var polymorphicAction = DeserializeNCAction(plainValue);
            var action = Assert.IsType<FaucetRune>(polymorphicAction.InnerAction);
            Assert.Equal(avatarAddress, action.AvatarAddress);
            for (var i = 0; i < faucetRuneInfos.Count; i++)
            {
                // Assert.Equal(faucetRuneInfos[i], action.FaucetRuneInfos[i]);
                Assert.Equal(faucetRuneInfos[i].RuneId, action.FaucetRuneInfos[i].RuneId);
                Assert.Equal(faucetRuneInfos[i].Amount, action.FaucetRuneInfos[i].Amount);
            }
        }
    }
}
