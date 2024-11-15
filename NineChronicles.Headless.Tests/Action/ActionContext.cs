using System.Collections.Generic;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Extensions.ActionEvaluatorCommonComponents;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace NineChronicles.Headless.Tests.Action;

public class ActionContext : IActionContext
{
    public Address Signer { get; init; }
    public TxId? TxId { get; init; }
    public Address Miner { get; init; }
    public long BlockIndex { get; init; }
    public int BlockProtocolVersion { get; init; }
    public BlockCommit LastCommit { get; init; }
    public IWorld PreviousState { get; init; }
    public int RandomSeed { get; init; }
    public bool IsPolicyAction { get; init; }
    public FungibleAssetValue? MaxGasPrice { get; set; }
    public IReadOnlyList<ITransaction> Txs { get; init; }
    public IReadOnlyList<EvidenceBase> Evidence { get; init; }

    public IRandom GetRandom()
    {
        return new Random(RandomSeed);
    }
}
