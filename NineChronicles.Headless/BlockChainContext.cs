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
        private readonly BlockChain<NCAction> _blockChain;

        public BlockChainContext(StandaloneContext standaloneContext, IStore store, BlockChain<NCAction> blockChain)
        {
            _standaloneContext = standaloneContext;
            _store = store;
            _blockChain = blockChain;
        }

        public bool Preloaded => _standaloneContext.NodeStatus.PreloadEnded;
        public BlockChain<PolymorphicAction<ActionBase>> BlockChain => _blockChain;
        public IStore Store => _store;
    }
}
