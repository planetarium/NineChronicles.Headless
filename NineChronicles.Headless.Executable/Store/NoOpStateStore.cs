using System;
using System.Collections.Immutable;
using Bencodex.Types;
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

        public IValue? GetState(string stateKey, BlockHash? blockHash = null) =>
            null;

        public bool ContainsBlockStates(BlockHash blockHash) =>
            false;

        public void ForkStates<T>(Guid sourceChainId, Guid destinationChainId, Block<T> branchpoint) where T : IAction, new()
        {
        }
    }
}
