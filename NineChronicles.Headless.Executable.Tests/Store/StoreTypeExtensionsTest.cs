using System;
using System.IO;
using Libplanet.RocksDBStore;
using Libplanet.Store;
using NineChronicles.Headless.Executable.Store;
using Xunit;

namespace NineChronicles.Headless.Executable.Tests.Store
{
    public class StoreTypeExtensionsTest : IDisposable
    {
        private readonly string _storePath;

        public StoreTypeExtensionsTest()
        {
            _storePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
        }

        [Theory]
        [InlineData(StoreType.RocksDb, typeof(RocksDBStore))]
        [InlineData(StoreType.Default, typeof(DefaultStore))]
        [InlineData(StoreType.Memory, typeof(MemoryStore))]
        public void ToStoreConstructor(StoreType storeType, Type expectedType)
        {
            IStore store = storeType.CreateStore(_storePath);
            Assert.IsType(expectedType, store);
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
