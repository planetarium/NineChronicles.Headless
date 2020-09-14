using Libplanet.Action;
using Libplanet.Blockchain.Renderers;
using Libplanet.Blocks;

namespace NineChronicles.Standalone.Tests.GraphTypes
{
    public class DummyRenderer<T> : IRenderer<T>
        where T : IAction, new()
    {
        public void RenderBlock(
            Block<T> oldTip,
            Block<T> newTip
        )
        {
        }

        public void RenderReorg(
            Block<T> oldTip,
            Block<T> newTip,
            Block<T> branchpoint
        )
        {
        }

        public void RenderReorgEnd(Block<T> oldTip, Block<T> newTip, Block<T> branchpoint)
        {
        }
    }
}

