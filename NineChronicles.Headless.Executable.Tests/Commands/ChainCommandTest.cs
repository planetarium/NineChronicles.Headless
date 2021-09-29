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
using Libplanet.Crypto;
using Libplanet.Store;
using Nekoyume.Action;
using Nekoyume.BlockChain;
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
        private readonly string _storePath2;

        public ChainCommandTest()
        {
            _console = new StringIOConsole();
            _command = new ChainCommand(_console);

            _storePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            _storePath2 = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        }

        [Theory]
        [InlineData(StoreType.Default)]
        [InlineData(StoreType.RocksDb)]
        [InlineData(StoreType.MonoRocksDb)]
        public void Tip(StoreType storeType)
        {
            Block<NCAction> genesisBlock = BlockChain<NCAction>.MakeGenesisBlock(
                HashAlgorithmType.Of<SHA256>()
            );
            IStore store = storeType.CreateStore(_storePath);
            Guid chainId = Guid.NewGuid();
            store.SetCanonicalChainId(chainId);
            store.PutBlock(genesisBlock);
            store.AppendIndex(chainId, genesisBlock.Hash);
            store.Dispose();

            // FIXME For an unknown reason, BlockHeader.TimeStamp precision issue occurred and the store we should open it again.
            store = storeType.CreateStore(_storePath);
            genesisBlock = store.GetBlock<NCAction>(genesisBlock.Hash);
            store.Dispose();

            _command.Tip(storeType, _storePath);
            Assert.Equal(JsonSerializer.Serialize(genesisBlock.Header), _console.Out.ToString().Trim());
        }

        [Theory]
        [InlineData(StoreType.Default)]
        [InlineData(StoreType.RocksDb)]
        [InlineData(StoreType.MonoRocksDb)]
        public async Task Inspect(StoreType storeType)
        {
            Block<NCAction> genesisBlock = BlockChain<NCAction>.MakeGenesisBlock(
                HashAlgorithmType.Of<SHA256>()
            );
            IStore store = storeType.CreateStore(_storePath2);
            Guid chainId = Guid.NewGuid();
            store.SetCanonicalChainId(chainId);
            store.PutBlock(genesisBlock);
            store.AppendIndex(chainId, genesisBlock.Hash);

            const int minimumDifficulty = 5000000, maximumTransactions = 100;
            IStagePolicy<NCAction> stagePolicy = new VolatileStagePolicy<PolymorphicAction<ActionBase>>();
            IBlockPolicy<NCAction> blockPolicy = new BlockPolicySource(Logger.None).GetPolicy(minimumDifficulty, maximumTransactions);
            BlockChain<NCAction> chain = new BlockChain<NCAction>(
                blockPolicy,
                stagePolicy,
                store,
                new NoOpStateStore(),
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
            var miner = minerKey.ToAddress();
            chain.MakeTransaction(minerKey, new PolymorphicAction<ActionBase>[] { action });
            await chain.MineBlock(miner, DateTimeOffset.Now);
            store.Dispose();

            _command.Inspect(storeType, _storePath2);
            List<double> output = _console.Out.ToString().Split("\n")[1]
                .Split(',').Select(double.Parse).ToList();
            var totalTxCount = Convert.ToInt32(output[2]);
            var hackandslashCount = Convert.ToInt32(output[3]);

            Assert.Equal(1, totalTxCount);
            Assert.Equal(1, hackandslashCount);
        }

        public void Dispose()
        {
            if (Directory.Exists(_storePath))
            {
                Directory.Delete(_storePath, true);
            }

            if (Directory.Exists(_storePath2))
            {
                Directory.Delete(_storePath2, true);
            }
        }
    }
}
