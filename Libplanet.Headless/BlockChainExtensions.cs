#nullable enable
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Renderers;
using System.Linq;

namespace Libplanet.Headless
{
    public static class BlockChainExtensions
    {
        public static DelayedRenderer<T>? GetDelayedRenderer<T>(this BlockChain<T> blockChain)
            where T : IAction, new() =>
            blockChain.Renderers
                // We must strip LoggedRenderer since all renderer was warpped by it.
                .OfType<LoggedRenderer<T>>()
                .Select(r => r.Renderer)
                .OfType<DelayedRenderer<T>>()
                .FirstOrDefault();
    }
}
