using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Security.Cryptography;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blocks;
using Libplanet.Tx;
using Nekoyume.Action;

namespace NineChronicles.Headless
{
    public class ReorgPolicy : IBlockPolicy<PolymorphicAction<ActionBase>>
    {
        private readonly long _difficulty;

        public IAction BlockAction { get; }

        public IImmutableSet<Currency> NativeTokens { get; }

        public ReorgPolicy(IAction blockAction, long difficulty, IImmutableSet<Currency> nativeTokens)
        {
            BlockAction = blockAction;
            NativeTokens = nativeTokens;
            _difficulty = difficulty;
        }

        public TxPolicyViolationException? ValidateNextBlockTx(
            BlockChain<PolymorphicAction<ActionBase>> blockChain,
            Transaction<PolymorphicAction<ActionBase>> transaction)
        {
            return null;
        }

        public BlockPolicyViolationException? ValidateNextBlock(
            BlockChain<PolymorphicAction<ActionBase>> blockChain,
            Block<PolymorphicAction<ActionBase>> nextBlock)
        {
            return null;
        }

        public long GetNextBlockDifficulty(BlockChain<PolymorphicAction<ActionBase>> blocks)
        {
            return blocks.Tip is null ? 0 : _difficulty;
        }

        public IComparer<IBlockExcerpt> CanonicalChainComparer { get; } = new TotalDifficultyComparer();

        public HashAlgorithmType GetHashAlgorithm(long blockIndex) =>
            HashAlgorithmType.Of<SHA256>();

        public long GetMaxTransactionsBytes(long index) => long.MaxValue;

        public int GetMinTransactionsPerBlock(long index) => 0;

        public int GetMaxTransactionsPerBlock(long index) => int.MaxValue;

        public int GetMaxTransactionsPerSignerPerBlock(long index)
        {
            throw new NotImplementedException();
        }
    }
}
