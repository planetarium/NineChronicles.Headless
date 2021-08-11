using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Libplanet;
using Libplanet.Action;
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
        
        public ReorgPolicy(IAction blockAction, long difficulty)
        {
            BlockAction = blockAction;
            _difficulty = difficulty;
        }

        public int MaxTransactionsPerBlock => int.MaxValue;

        public int GetMaxBlockBytes(long index) => int.MaxValue;

        public bool DoesTransactionFollowsPolicy(
            Transaction<PolymorphicAction<ActionBase>> transaction,
            BlockChain<PolymorphicAction<ActionBase>> blockChain
        ) => true;

        public InvalidBlockException? ValidateNextBlock(BlockChain<PolymorphicAction<ActionBase>> blocks, Block<PolymorphicAction<ActionBase>> nextBlock)
        {
            return null;
        }

        public long GetNextBlockDifficulty(BlockChain<PolymorphicAction<ActionBase>> blocks)
        {
            return blocks.Tip is null ? 0 : _difficulty;
        }

        public IComparer<BlockPerception> CanonicalChainComparer { get; } = new TotalDifficultyComparer(TimeSpan.FromSeconds(30));

        public IAction BlockAction { get; }

        public HashAlgorithmType GetHashAlgorithm(long blockIndex) =>
            HashAlgorithmType.Of<SHA256>();
    }
}
