using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text.Json;
using System.Threading.Tasks;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blocks;
using Libplanet.Extensions.Cocona;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Nekoyume.Action;
using Nekoyume.BlockChain.Policy;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.Store;
using NineChronicles.Headless.Executable.Tests.IO;
using Serilog.Core;
using Xunit;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.Executable.Tests.Commands
{
    public class ChainCommandTest : IDisposable
    {
        private readonly StringIOConsole _console;
        private readonly ChainCommand _command;

        private readonly string _storePath;

        public ChainCommandTest()
        {
            _console = new StringIOConsole();
            _command = new ChainCommand(_console);

            _storePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        }

        [Theory]
        [InlineData(StoreType.Default)]
        [InlineData(StoreType.RocksDb)]
        public void Tip(StoreType storeType)
        {
            HashAlgorithmType hashAlgo = HashAlgorithmType.Of<SHA256>();
            Block<NCAction> genesisBlock = BlockChain<NCAction>.MakeGenesisBlock(hashAlgo);
            IStore store = storeType.CreateStore(_storePath);
            Guid chainId = Guid.NewGuid();
            store.SetCanonicalChainId(chainId);
            store.PutBlock(genesisBlock);
            store.AppendIndex(chainId, genesisBlock.Hash);
            store.Dispose();

            // FIXME For an unknown reason, BlockHeader.TimeStamp precision issue occurred and the store we should open it again.
            store = storeType.CreateStore(_storePath);
            genesisBlock = store.GetBlock<NCAction>(_ => hashAlgo, genesisBlock.Hash);
            store.Dispose();

            _command.Tip(storeType, _storePath);
            Assert.Equal(
                Utils.SerializeHumanReadable(genesisBlock.Header),
                _console.Out.ToString().Trim()
            );
        }

        [Theory]
        [InlineData(StoreType.Default)]
        [InlineData(StoreType.RocksDb)]
        public async Task Inspect(StoreType storeType)
        {
            Block<NCAction> genesisBlock = BlockChain<NCAction>.MakeGenesisBlock(
                HashAlgorithmType.Of<SHA256>()
            );
            IStore store = storeType.CreateStore(_storePath);
            Guid chainId = Guid.NewGuid();
            store.SetCanonicalChainId(chainId);
            store.PutBlock(genesisBlock);
            store.AppendIndex(chainId, genesisBlock.Hash);
            var stateStore = new TrieStateStore(new DefaultKeyValueStore(null));

            IStagePolicy<NCAction> stagePolicy = new VolatileStagePolicy<PolymorphicAction<ActionBase>>();
            IBlockPolicy<NCAction> blockPolicy = new BlockPolicySource(Logger.None).GetPolicy();
            BlockChain<NCAction> chain = new BlockChain<NCAction>(
                blockPolicy,
                stagePolicy,
                store,
                stateStore,
                genesisBlock);

            var action = new HackAndSlash
            {
                costumes = new List<Guid>(),
                equipments = new List<Guid>(),
                foods = new List<Guid>(),
                worldId = 1,
                stageId = 1,
                avatarAddress = default
            };

            var minerKey = new PrivateKey();
            chain.MakeTransaction(minerKey, new PolymorphicAction<ActionBase>[] { action });
            await chain.MineBlock(minerKey, DateTimeOffset.Now);
            store.Dispose();

            _command.Inspect(storeType, _storePath);
            List<double> output = _console.Out.ToString().Split("\n")[1]
                .Split(',').Select(double.Parse).ToList();
            var totalTxCount = Convert.ToInt32(output[2]);
            var hackandslashCount = Convert.ToInt32(output[3]);

            Assert.Equal(1, totalTxCount);
            Assert.Equal(1, hackandslashCount);
        }

        [Theory]
        [InlineData(StoreType.Default)]
        [InlineData(StoreType.RocksDb)]
        public async Task Truncate(StoreType storeType)
        {
            Block<NCAction> genesisBlock = BlockChain<NCAction>.MakeGenesisBlock(
                HashAlgorithmType.Of<SHA256>()
            );
            IStore store = storeType.CreateStore(_storePath);
            Guid chainId = Guid.NewGuid();
            store.SetCanonicalChainId(chainId);
            store.PutBlock(genesisBlock);
            store.AppendIndex(chainId, genesisBlock.Hash);
            var stateStore = new TrieStateStore(new DefaultKeyValueStore(null));

            IStagePolicy<NCAction> stagePolicy = new VolatileStagePolicy<PolymorphicAction<ActionBase>>();
            IBlockPolicy<NCAction> blockPolicy = new BlockPolicySource(Logger.None).GetPolicy();
            BlockChain<NCAction> chain = new BlockChain<NCAction>(
                blockPolicy,
                stagePolicy,
                store,
                stateStore,
                genesisBlock);

            var action = new HackAndSlash
            {
                costumes = new List<Guid>(),
                equipments = new List<Guid>(),
                foods = new List<Guid>(),
                worldId = 1,
                stageId = 1,
                avatarAddress = default
            };

            var minerKey = new PrivateKey();
            for (var i = 0; i < 2; i++)
            {
                chain.MakeTransaction(minerKey, new PolymorphicAction<ActionBase>[] { action });
                await chain.MineBlock(minerKey, DateTimeOffset.Now);
            }

            var indexCountBeforeTruncate = store.CountIndex(chainId);
            store.Dispose();
            _command.Truncate(storeType, _storePath, 1);
            IStore storeAfterTruncate = storeType.CreateStore(_storePath);
            chainId = storeAfterTruncate.GetCanonicalChainId() ?? new Guid();
            var indexCountAfterTruncate = storeAfterTruncate.CountIndex(chainId);
            storeAfterTruncate.Dispose();

            Assert.Equal(3, indexCountBeforeTruncate);
            Assert.Equal(2, indexCountAfterTruncate);
        }

        public void Dispose()
        {
            if (Directory.Exists(_storePath))
            {
                Directory.Delete(_storePath, true);
            }
        }
    }
}
