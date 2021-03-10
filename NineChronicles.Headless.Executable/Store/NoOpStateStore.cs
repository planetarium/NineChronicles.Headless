using System;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Bencodex.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blocks;
using Libplanet.Store;

namespace NineChronicles.Headless.Executable.Store
{
    public class NoOpStateStore : IStateStore
    {
        public void SetStates<T>(Block<T> block, IImmutableDictionary<string, IValue> states) where T : IAction, new()
        {
        }

        public IValue GetState(string stateKey, HashDigest<SHA256>? blockHash = null, Guid? chainId = null)
        {
            return null;
        }

        public bool ContainsBlockStates(HashDigest<SHA256> blockHash)
        {
            return false;
        }

        public void ForkStates<T>(Guid sourceChainId, Guid destinationChainId, Block<T> branchpoint) where T : IAction, new()
        {
        }
    }
}
