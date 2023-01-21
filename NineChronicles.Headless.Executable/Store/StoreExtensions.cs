#nullable enable
using System;
using System.Linq;
using Libplanet.Action;
using Libplanet.Blocks;
using Libplanet.Store;

namespace NineChronicles.Headless.Executable.Store
{
    public static class StoreExtensions
    {
        public static Block<T> GetGenesisBlock<T>(
            this IStore store)
            where T : IAction, new()
        {
            Guid? chainId = store.GetCanonicalChainId();
            if (chainId is null)
            {
                throw new InvalidOperationException("The store doesn't have genesis block.");
            }

            BlockHash genesisBlockHash = store.IterateIndexes(chainId.Value).First();
            Block<T> genesisBlock = store.GetBlock<T>(genesisBlockHash);
            return genesisBlock;
        }
    }
}
