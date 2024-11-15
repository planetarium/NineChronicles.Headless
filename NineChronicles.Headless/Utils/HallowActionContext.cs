using System;
using System.Collections.Generic;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Crypto;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Evidence;
using Libplanet.Types.Tx;

namespace NineChronicles.Headless.Utils
{
    public class HallowActionContext : IActionContext
    {
        public Address Signer => throw new NotImplementedException();
        public TxId? TxId => throw new NotImplementedException();
        public Address Miner => throw new NotImplementedException();
        public long BlockIndex => throw new NotImplementedException();
        public int BlockProtocolVersion => throw new NotImplementedException();
        public IWorld PreviousState => throw new NotImplementedException();
        public bool IsPolicyAction => throw new NotImplementedException();
        public IReadOnlyList<ITransaction> Txs => throw new NotImplementedException();
        public IReadOnlyList<EvidenceBase> Evidence => throw new NotImplementedException();
        public BlockCommit LastCommit => throw new NotImplementedException();
        public int RandomSeed => throw new NotImplementedException();
        public FungibleAssetValue? MaxGasPrice => throw new NotImplementedException();
        public IRandom GetRandom() => throw new NotImplementedException();
    }
}
