using System.Collections.Generic;

namespace NineChronicles.Headless.Repositories.Swarm
{
    public class SwarmRepository(Libplanet.Net.Swarm swarm) : ISwarmRepository
    {
        public void BroadcastTxs(IEnumerable<Libplanet.Types.Tx.Transaction> txs)
        {
            swarm.BroadcastTxs(txs);
        }
    }
}
