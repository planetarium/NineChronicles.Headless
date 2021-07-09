using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Libplanet.Tx;
using Nekoyume.Action;
using NineChronicles.Headless.GraphTypes;
using Xunit;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class TransactionHeadlessQueryTest
    {
        private readonly BlockChain<NCAction> _blockChain;

        public TransactionHeadlessQueryTest()
        {
            _blockChain = new BlockChain<NCAction>(
                new BlockPolicy<NCAction>(),
                new VolatileStagePolicy<NCAction>(),
                new DefaultStore(null),
                new TrieStateStore(new DefaultKeyValueStore(null), new DefaultKeyValueStore(null)),
                BlockChain<NCAction>.MakeGenesisBlock());
        }

        [Fact]
        public async Task NextTxNonce()
        {
            var userPrivateKey = new PrivateKey();
            var userAddress = userPrivateKey.ToAddress();
            string query = $"{{ nextTxNonce(address: \"{userAddress}\") }}";
            var queryResult = await ExecuteAsync(query);
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["nextTxNonce"] = 0L
                },
                queryResult.Data
            );

            _blockChain.MakeTransaction(userPrivateKey, new PolymorphicAction<ActionBase>[] { });
            queryResult = await ExecuteAsync(query);
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["nextTxNonce"] = 1L
                },
                queryResult.Data
            );
        }

        [Fact]
        public async Task GetTx()
        {
            var userPrivateKey = new PrivateKey();
            var queryFormat = @"query {{
                getTx(txId: ""{0}"") {{
                    id
                    nonce
                    signer
                    signature
                    timestamp
                    updatedAddresses
                    actions {{
                        inspection
                    }}
                }}
            }}";
            var queryResult = await ExecuteAsync(string.Format(queryFormat, new TxId()));
            Assert.Equal(
                new Dictionary<string, object?>
                {
                    ["getTx"] = null
                },
                queryResult.Data
            );

            var action = new CreateAvatar2
            {
                index = 0,
                hair = 1,
                lens = 2,
                ear = 3,
                tail = 4,
                name = "action",
            };
            var transaction = _blockChain.MakeTransaction(userPrivateKey, new PolymorphicAction<ActionBase>[] { action });
            _blockChain.StageTransaction(transaction);
            await _blockChain.MineBlock(new Address());
            queryResult = await ExecuteAsync(string.Format(queryFormat, transaction.Id));
            var tx = queryResult.Data
                .As<Dictionary<string, object>>()["getTx"]
                .As<Dictionary<string, object>>();

            Assert.Equal(tx["id"], transaction.Id.ToString());
            Assert.Equal(tx["nonce"], transaction.Nonce);
            Assert.Equal(tx["signer"], transaction.Signer.ToString());
            Assert.Equal(tx["signature"], ByteUtil.Hex(transaction.Signature));
            Assert.Equal(tx["timestamp"], transaction.Timestamp.ToString());
            Assert.Equal(tx["updatedAddresses"], transaction.UpdatedAddresses.Select(a => a.ToString()));

            var plainValue = tx["actions"]
                .As<List<object>>()
                .First()
                .As<Dictionary<string, object>>()["inspection"];
            Assert.Equal(transaction.Actions.First().PlainValue.Inspection, plainValue);
        }

        private Task<ExecutionResult> ExecuteAsync(string query)
        {
            return GraphQLTestUtils.ExecuteQueryAsync<TransactionHeadlessQuery>(query, standaloneContext: new StandaloneContext
            {
                BlockChain = _blockChain,
            });
        }  
    }     
}
