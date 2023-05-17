using Libplanet.Action;
using Libplanet.Blockchain.Renderers;
using Libplanet.Blocks;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class DummyRenderer : IRenderer
    {
        public void RenderBlock(
            Block oldTip,
            Block newTip
        )
        {
        }
    }
}

