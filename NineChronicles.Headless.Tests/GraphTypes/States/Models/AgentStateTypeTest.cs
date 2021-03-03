using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Lib9c.Renderer;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blockchain.Renderers;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NineChronicles.Headless.GraphTypes.States;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class AgentStateTypeTest
    {
        [Fact]
        public async Task Query()
        {
            const string query = @"
            {
                address
                avatarAddresses
                gold
            }";
            var agentState = new AgentState(new Address());
            var standAloneContext = new StandaloneContext();
            var store = new DefaultStore(null);
            var stateStore = new TrieStateStore(
                new DefaultKeyValueStore(null),
                new DefaultKeyValueStore(null)
            );
            var goldCurrency = new Currency("NCG", 2, minter: null);

            var fixturePath = Path.Combine("..", "..", "..", "..", "Lib9c", ".Lib9c.Tests", "Data", "TableCSV");
            var sheets = TableSheetsImporter.ImportSheets(fixturePath);
            var blockAction = new RewardGold();
            var genesisBlock = BlockChain<PolymorphicAction<ActionBase>>.MakeGenesisBlock(
                new PolymorphicAction<ActionBase>[]
                {
                    new InitializeStates(
                        rankingState: new RankingState(),
                        shopState: new ShopState(),
                        gameConfigState: new GameConfigState(sheets[nameof(GameConfigSheet)]),
                        redeemCodeState: new RedeemCodeState(Bencodex.Types.Dictionary.Empty
                            .Add("address", RedeemCodeState.Address.Serialize())
                            .Add("map", Bencodex.Types.Dictionary.Empty)
                        ),
                        adminAddressState: new AdminState(default, 0),
                        activatedAccountsState: new ActivatedAccountsState(),
                        goldCurrencyState: new GoldCurrencyState(goldCurrency),
                        goldDistributions: new GoldDistribution[]{ },
                        tableSheets: sheets,
                        pendingActivationStates: new PendingActivationState[]{ }
                    ),
                }, blockAction: blockAction);

            var blockPolicy = new BlockPolicy<PolymorphicAction<ActionBase>>(blockAction: blockAction);
            var blockChain = new BlockChain<PolymorphicAction<ActionBase>>(
                blockPolicy,
                new VolatileStagePolicy<PolymorphicAction<ActionBase>>(),
                store,
                stateStore,
                genesisBlock,
                renderers: new IRenderer<PolymorphicAction<ActionBase>>[] { new BlockRenderer(), new ActionRenderer() }
            );
            standAloneContext.BlockChain = blockChain;
            var queryResult = await ExecuteQueryAsync<AgentStateType>(
                query,
                source: agentState,
                standaloneContext: standAloneContext);
            var expected = new Dictionary<string, object>()
            {
                ["address"] = agentState.address.ToString(),
                ["avatarAddresses"] = new List<string>(),
                ["gold"] = "0"
            };
            Assert.Equal(expected, queryResult.Data);
        }
    }
}
