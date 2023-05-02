using Libplanet.Action;
using Libplanet.Blockchain.Renderers;
using Libplanet.Blocks;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class DummyRenderer<T> : IRenderer<T>
        where T : IAction, new()
    {
        public void RenderBlock(
            Block oldTip,
            Block newTip
        )
        {
        }

        public void RenderReorg(
            Block oldTip,
            Block newTip,
            Block branchpoint
        )
        {
        }

        public void RenderReorgEnd(Block oldTip, Block newTip, Block branchpoint)
        {
        }
    }
}

