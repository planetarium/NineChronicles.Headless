using System;
using System.IO;
using System.Linq;
using System.Text;
using Bencodex;
using Libplanet;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.RocksDBStore;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Nekoyume.BlockChain.Policy;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.Store;
using NineChronicles.Headless.Executable.Tests.IO;
using Serilog.Core;
using Xunit;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;
using Lib9cUtils = Lib9c.DevExtensions.Utils;

namespace NineChronicles.Headless.Executable.Tests.Commands
{
    public class AccountCommandTest
    {
        private readonly StringIOConsole _console;
        private readonly Codec _codec = new Codec();
        private readonly AccountCommand _command;

        private readonly string _storePath;

        public AccountCommandTest()
        {
            _console = new StringIOConsole();
            _command = new AccountCommand(_console);
            _storePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        }

        [Theory]
        [InlineData(StoreType.Default)]
        [InlineData(StoreType.RocksDb)]
        public void Balance(StoreType storeType)
        {
            var statesPath = Path.Combine(_storePath, "states");
            Address targetAddress = new PrivateKey().ToAddress();
            int targetCurrency = 10000; // 100 NCG
            Block<NCAction> genesisBlock = GenesisHelper.MineGenesisBlock(targetAddress, targetCurrency);
            IStore store = storeType.CreateStore(_storePath);
            Guid chainId = Guid.NewGuid();
            store.SetCanonicalChainId(chainId);
            store.PutBlock(genesisBlock);
            store.AppendIndex(chainId, genesisBlock.Hash);
            var stateKeyValueStore = new RocksDBKeyValueStore(statesPath);
            var stateStore = new TrieStateStore(stateKeyValueStore);
            IStagePolicy<NCAction> stagePolicy = new VolatileStagePolicy<NCAction>();
            IBlockPolicy<NCAction> blockPolicy = new BlockPolicySource(Logger.None).GetPolicy();
            BlockChain<NCAction> chain = new BlockChain<NCAction>(
                blockPolicy,
                stagePolicy,
                store,
                stateStore,
                genesisBlock);
            chain.ExecuteActions(chain.Tip);
            int prevStatesCount = stateKeyValueStore.ListKeys().Count();
            stateKeyValueStore.Set(
                new KeyBytes("alpha", Encoding.UTF8),
                ByteUtil.ParseHex("00"));
            stateKeyValueStore.Set(
                new KeyBytes("beta", Encoding.UTF8),
                ByteUtil.ParseHex("00"));
            Assert.Equal(prevStatesCount + 2, stateKeyValueStore.ListKeys().Count());
            store.Dispose();
            stateStore.Dispose();

            _command.Balance(false, _storePath, address: targetAddress.ToString());
            string[] result = _console.Out.ToString().Trim().Split("\t");
            Assert.Equal(targetAddress.ToString(), result[0]);
            // NCG recognizes the last two digits after the decimal point.
            Assert.Equal(targetCurrency / 100, Int32.Parse(result[1].Split(" ")[0]));
        }
    }
}
