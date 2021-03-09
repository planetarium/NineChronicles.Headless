using System;
using Libplanet.RocksDBStore;
using Libplanet.Store;

namespace NineChronicles.Headless.Executable.Store
{
    public static class StoreTypeExtensions
    {
        public static Func<string, IStore> ToStoreConstructor(this StoreType storeType) => storeType switch
        {
            StoreType.RocksDb => path => new RocksDBStore(path),
            StoreType.Default => path => new DefaultStore(path),
            _ => throw new ArgumentOutOfRangeException(nameof(storeType))
        };
    }
}
