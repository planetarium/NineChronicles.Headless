using System.Collections.Generic;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Extensions.ActionEvaluatorCommonComponents;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace NineChronicles.Headless.Tests.Action;

public class ActionContext : IActionContext
{
    private long UsedGas { get; set; }

    public Address Signer { get; init; }
    public TxId? TxId { get; init; }
    public Address Miner { get; init; }
    public long BlockIndex { get; init; }
    public int BlockProtocolVersion { get; init; }
    public IWorld PreviousState { get; init; }
    public int RandomSeed { get; init; }
    public bool IsPolicyAction { get; init; }
    public IReadOnlyList<ITransaction> Txs { get; init; }
    public IReadOnlyList<EvidenceBase> Evidence { get; init; }
    public void UseGas(long gas)
    {
        UsedGas += gas;
    }

    public IRandom GetRandom()
    {
        return new Random(RandomSeed);
    }

    public long GasUsed()
    {
        return UsedGas;
    }

    public long GasLimit()
    {
        return 0L;
    }
}
