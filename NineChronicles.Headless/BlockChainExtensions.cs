using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blocks;

namespace NineChronicles.Headless
{
    public static class BlockChainExtensions
    {
        public static AccountStateGetter ToAccountStateGetter<T>(this BlockChain<T> chain, BlockHash? blockHash = null)
            where T : IAction, new() =>
            addresses => chain.GetStates(addresses, blockHash ?? chain.Tip.Hash);

        public static AccountBalanceGetter ToAccountBalanceGetter<T>(this BlockChain<T> chain, BlockHash? blockHash = null)
            where T : IAction, new() =>
            (address, currency) => chain.GetBalance(address, currency, blockHash ?? chain.Tip.Hash);
    }
}
