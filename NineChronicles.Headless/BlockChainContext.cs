using Libplanet.Blockchain;
using Libplanet.Explorer.Indexing;
using Libplanet.Explorer.Interfaces;
using Libplanet.Net;
using Libplanet.Store;

namespace NineChronicles.Headless
{
    public class BlockChainContext : IBlockChainContext
    {
        private readonly StandaloneContext _standaloneContext;

        public BlockChainContext(StandaloneContext standaloneContext)
        {
            _standaloneContext = standaloneContext;
        }

        public bool Preloaded => _standaloneContext.NodeStatus.PreloadEnded;
        public BlockChain BlockChain => _standaloneContext.BlockChain;
        public IStore Store => _standaloneContext.Store;
        public Swarm Swarm => _standaloneContext.Swarm;
        public IBlockChainIndex Index => new RocksDbBlockChainIndex("/tmp/no/no/no/store");
    }
}
