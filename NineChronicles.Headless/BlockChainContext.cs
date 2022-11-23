using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Explorer.Interfaces;
using Libplanet.Store;
using Nekoyume.Action;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless
{
    public class BlockChainContext : IBlockChainContext<NCAction>
    {
        private readonly StandaloneContext _standaloneContext;
        private readonly IStore _store;

        public BlockChainContext(StandaloneContext standaloneContext, IStore store)
        {
            _standaloneContext = standaloneContext;
            _store = store;
        }

        public bool Preloaded => _standaloneContext.NodeStatus.PreloadEnded;
        public BlockChain<PolymorphicAction<ActionBase>>? BlockChain => _standaloneContext.BlockChain;
        public IStore Store => _store;
    }
}
