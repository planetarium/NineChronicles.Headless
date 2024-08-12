using System.Collections.Generic;
using System.Security.Cryptography;
using Libplanet.Common;
using Libplanet.Crypto;
using NineChronicles.Headless.GraphTypes.Diff;

namespace NineChronicles.Headless.Repositories.StateTrie;

public interface IStateTrieRepository
{
    IEnumerable<IDiffType> CompareStateTrie(HashDigest<SHA256> baseStateRootHash, HashDigest<SHA256> targetStateRootHash);
    IEnumerable<StateDiffType.Value> CompareStateAccountTrie(HashDigest<SHA256> baseStateRootHash, HashDigest<SHA256> targetStateRootHash, Address accountAddress);
}
