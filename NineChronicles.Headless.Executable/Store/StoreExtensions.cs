#nullable enable
using System;
using System.Linq;
using Libplanet.Action;
using Libplanet.Types.Blocks;
using Libplanet.Store;

namespace NineChronicles.Headless.Executable.Store
{
    public static class StoreExtensions
    {
        public static Block GetGenesisBlock(this IStore store)
        {
            Guid? chainId = store.GetCanonicalChainId();
            if (chainId is null)
            {
                throw new InvalidOperationException("The store doesn't have genesis block.");
            }

            BlockHash genesisBlockHash = store.IterateIndexes(chainId.Value).First();
            Block? genesisBlock = store.GetBlock(genesisBlockHash);
            if (genesisBlock == null)
            {
                throw new InvalidOperationException("The store doesn't have genesis block.");
            }

            return genesisBlock;
        }
    }
}
