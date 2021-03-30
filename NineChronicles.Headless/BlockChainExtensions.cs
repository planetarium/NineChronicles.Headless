using System.Security.Cryptography;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blockchain;

namespace NineChronicles.Headless
{
    public static class BlockChainExtensions
    {
        public static AccountStateGetter ToAccountStateGetter<T>(this BlockChain<T> chain, HashDigest<SHA256>? blockHash = null)
            where T : IAction, new() =>
            address => chain.GetState(address, blockHash ?? chain.Tip.Hash);
        
        public static AccountBalanceGetter ToAccountBalanceGetter<T>(this BlockChain<T> chain, HashDigest<SHA256>? blockHash = null)
            where T : IAction, new() =>
            (address, currency) => chain.GetBalance(address, currency, blockHash ?? chain.Tip.Hash);
    }
}
