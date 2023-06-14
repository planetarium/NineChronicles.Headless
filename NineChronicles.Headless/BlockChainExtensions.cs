using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.State;

namespace NineChronicles.Headless
{
    public static class BlockChainExtensions
    {
        public static AccountStateGetter ToAccountStateGetter(this BlockChain chain, BlockHash? blockHash = null) =>
            addresses => chain.GetStates(addresses, blockHash ?? chain.Tip.Hash);

        public static AccountBalanceGetter ToAccountBalanceGetter(this BlockChain chain, BlockHash? blockHash = null) =>
            (address, currency) => chain.GetBalance(address, currency, blockHash ?? chain.Tip.Hash);
    }
}
