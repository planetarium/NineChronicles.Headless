using System.Collections.Generic;

namespace NineChronicles.Headless.Repositories.Swarm
{
    public interface ISwarmRepository
    {
        void BroadcastTxs(IEnumerable<Libplanet.Types.Tx.Transaction> txs);
    }
}
