namespace NineChronicles.Headless.Executable.Store
{
    public enum StoreType
    {
        /// <summary>
        /// A store type corresponding to <see cref="Libplanet.RocksDBStore.RocksDBStore"/>.
        /// </summary>
        RocksDb,

        /// <summary>
        /// A store type corresponding to <see cref="Libplanet.Store.MemoryStore"/>.
        /// </summary>
        Memory,

        /// <summary>
        /// A store type corresponding to <see cref="Libplanet.Store.DefaultStore"/>.
        /// </summary>
        Default,
    }
}
