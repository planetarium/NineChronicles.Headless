using System;
using System.IO;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Types.Blocks;
using Libplanet.Store;
using Libplanet.Store.Trie;
using Nekoyume.Action.Loader;
using NineChronicles.Headless.Executable.Store;
using Xunit;

namespace NineChronicles.Headless.Executable.Tests.Store
{
    public class StoreExtensionsTest : IDisposable
    {
        private readonly string _storePath;

        public StoreExtensionsTest()
        {
            _storePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        }

        [Theory]
        [InlineData(StoreType.Default)]
        [InlineData(StoreType.RocksDb)]
        [InlineData(StoreType.Memory)]
        public void GetGenesisBlock(StoreType storeType)
        {
            IStore store = storeType.CreateStore(_storePath);
            IActionEvaluator actionEvaluator = new ActionEvaluator(
                _ => new BlockPolicy().BlockAction,
                new TrieStateStore(new MemoryKeyValueStore()),
                new NCActionLoader());
            Block genesisBlock = BlockChain.ProposeGenesisBlock(actionEvaluator);
            Guid chainId = Guid.NewGuid();
            store.SetCanonicalChainId(chainId);
            store.PutBlock(genesisBlock);
            store.AppendIndex(chainId, genesisBlock.Hash);

            Assert.Equal(genesisBlock, store.GetGenesisBlock());

            (store as IDisposable)?.Dispose();
        }

        [Theory]
        [InlineData(StoreType.Default)]
        [InlineData(StoreType.RocksDb)]
        [InlineData(StoreType.Memory)]
        public void GetGenesisBlock_ThrowsInvalidOperationException_IfChainIdNotExist(StoreType storeType)
        {
            IStore store = storeType.CreateStore(_storePath);
            Assert.Throws<InvalidOperationException>(
                () => store.GetGenesisBlock()
            );
            (store as IDisposable)?.Dispose();
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
