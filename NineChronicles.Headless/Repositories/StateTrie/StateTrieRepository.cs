using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Bencodex.Types;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Store.Trie;
using NineChronicles.Headless.GraphTypes.Diff;

namespace NineChronicles.Headless.Repositories.StateTrie;

public class StateTrieRepository : IStateTrieRepository
{
    private readonly IStateStore _stateStore;

    public StateTrieRepository(IStateStore stateStore)
    {
        _stateStore = stateStore;
    }

    public IEnumerable<IDiffType> CompareStateTrie(HashDigest<SHA256> baseStateRootHash, HashDigest<SHA256> targetStateRootHash)
    {
        var baseTrieModel = _stateStore.GetStateRoot(baseStateRootHash);
        var targetTrieModel = _stateStore.GetStateRoot(targetStateRootHash);

        return baseTrieModel
            .Diff(targetTrieModel)
            .Select(x =>
            {
                if (x.TargetValue is not null)
                {
                    var baseSubTrieModel = _stateStore.GetStateRoot(new HashDigest<SHA256>((Binary)x.SourceValue));
                    var targetSubTrieModel = _stateStore.GetStateRoot(new HashDigest<SHA256>((Binary)x.TargetValue));
                    var subDiff = baseSubTrieModel
                        .Diff(targetSubTrieModel)
                        .Select(diff =>
                        {
                            return new StateDiffType.Value(
                                Encoding.Default.GetString(diff.Path.ByteArray.ToArray()),
                                diff.SourceValue,
                                diff.TargetValue);
                        }).ToArray();
                    return (IDiffType)new RootStateDiffType.Value(
                        Encoding.Default.GetString(x.Path.ByteArray.ToArray()),
                        subDiff
                    );
                }
                else
                {
                    return new StateDiffType.Value(
                        Encoding.Default.GetString(x.Path.ByteArray.ToArray()),
                        x.SourceValue,
                        x.TargetValue
                    );
                }
            });
    }

    public IEnumerable<StateDiffType.Value> CompareStateAccountTrie(HashDigest<SHA256> baseStateRootHash, HashDigest<SHA256> targetStateRootHash, Address accountAddress)
    {
        var baseTrieModel = _stateStore.GetStateRoot(baseStateRootHash);
        var targetTrieModel = _stateStore.GetStateRoot(targetStateRootHash);

        var accountKey = new KeyBytes(ByteUtil.Hex(accountAddress.ByteArray));

        Binary GetAccountState(ITrie model, KeyBytes key)
        {
            return model.Get(key) is Binary state ? state : throw new Exception($"Account state not found.");
        }

        var baseAccountState = GetAccountState(baseTrieModel, accountKey);
        var targetAccountState = GetAccountState(targetTrieModel, accountKey);

        var baseSubTrieModel = _stateStore.GetStateRoot(new HashDigest<SHA256>(baseAccountState));
        var targetSubTrieModel = _stateStore.GetStateRoot(new HashDigest<SHA256>(targetAccountState));

        return baseSubTrieModel
            .Diff(targetSubTrieModel)
            .Select(diff => new StateDiffType.Value(
                Encoding.Default.GetString(diff.Path.ByteArray.ToArray()),
                diff.SourceValue,
                diff.TargetValue));
    }
}
