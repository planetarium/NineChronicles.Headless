using System;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using Libplanet;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Store;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.Store;
using NineChronicles.Headless.Executable.Tests.IO;
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

        public void Dispose()
        {
            if (Directory.Exists(_storePath))
            {
                Directory.Delete(_storePath, true);
            }
        }
    }
}
