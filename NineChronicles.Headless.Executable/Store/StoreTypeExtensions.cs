using System;
using Libplanet.RocksDBStore;
using Libplanet.Store;

namespace NineChronicles.Headless.Executable.Store
{
    public static class StoreTypeExtensions
    {
        public static IStore CreateStore(this StoreType storeType, string storePath) => storeType switch
        {
            StoreType.RocksDb => new RocksDBStore(storePath, dbConnectionCacheSize: 5),
            StoreType.Memory => new MemoryStore(),
            StoreType.Default => new DefaultStore(storePath),
            _ => throw new ArgumentOutOfRangeException(nameof(storeType))
        };
    }
}
