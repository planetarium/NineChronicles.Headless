#nullable enable
using System;
using System.Linq;
using System.Security.Cryptography;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blocks;
using Libplanet.Store;

namespace NineChronicles.Headless.Executable.Store
{
    public static class IStoreExtensions
    {
        public static Block<T> GetGenesisBlock<T>(this IStore store)
            where T : IAction, new()
        {
            Guid? chainId = store.GetCanonicalChainId();
            if (chainId is null)
            {
                throw new ArgumentException("The store doesn't have genesis block.", nameof(store));
            }

            HashDigest<SHA256> genesisBlockHash = store.IterateIndexes(chainId.Value).First();
            Block<T> genesisBlock = store.GetBlock<T>(genesisBlockHash);
            return genesisBlock;
        }
    }
}
