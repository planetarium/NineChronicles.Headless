using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Bencodex.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.Net;
using Libplanet.Headless.Hosting;
using Libplanet.Tx;
using Xunit;

namespace Libplanet.Headless.Tests.Hosting
{
    public class LibplanetNodeServiceTest
    {
        [Fact]
        public void Constructor()
        {
            var genesisBlock = BlockChain<DummyAction>.MakeGenesisBlock(HashAlgorithmType.Of<SHA256>());
            var service = new LibplanetNodeService<DummyAction>(
                new LibplanetNodeServiceProperties<DummyAction>()
                {
                    AppProtocolVersion = new AppProtocolVersion(),
                    GenesisBlock = genesisBlock,
                    SwarmPrivateKey = new PrivateKey(),
                    StoreStatesCacheSize = 2,
                    StorePath = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString()),
                    Host = IPAddress.Loopback.ToString(),
                },
                blockPolicy: new BlockPolicy(),
                stagePolicy: new VolatileStagePolicy<DummyAction>(),
                renderers: null,
                minerLoopAction: (chain, swarm, pk, ct) => Task.CompletedTask,
                preloadProgress: null,
                exceptionHandlerAction:  (code, msg) => throw new Exception($"{code}, {msg}"),
                preloadStatusHandlerAction: isPreloadStart => { }
            );

            Assert.NotNull(service);
        }

        [Fact]
        public void PropertiesMustContainGenesisBlockOrPath()
        {
            Assert.Throws<ArgumentException>(() =>
            {
                var service = new LibplanetNodeService<DummyAction>(
                    new LibplanetNodeServiceProperties<DummyAction>()
                    {
                        AppProtocolVersion = new AppProtocolVersion(),
                        SwarmPrivateKey = new PrivateKey(),
                        StoreStatesCacheSize = 2,
                        Host = IPAddress.Loopback.ToString(),
                    },
                    blockPolicy: new BlockPolicy(),
                    stagePolicy: new VolatileStagePolicy<DummyAction>(),
                    renderers: null,
                    minerLoopAction: (chain, swarm, pk, ct) => Task.CompletedTask,
                    preloadProgress: null,
                    exceptionHandlerAction:  (code, msg) => throw new Exception($"{code}, {msg}"),
                    preloadStatusHandlerAction: isPreloadStart => { }
                );
            });
        }

        private class BlockPolicy : IBlockPolicy<DummyAction>
        {
            public IComparer<BlockPerception> CanonicalChainComparer { get; } = new TotalDifficultyComparer(TimeSpan.FromSeconds(3));

            public IAction BlockAction => null;

            public int MaxTransactionsPerBlock => int.MaxValue;

            public int GetMaxBlockBytes(long index) => int.MaxValue;

            public bool DoesTransactionFollowsPolicy(
                Transaction<DummyAction> transaction,
                BlockChain<DummyAction> blockChain
            )
            {
                return true;
            }

            public long GetNextBlockDifficulty(BlockChain<DummyAction> blocks)
            {
                return 0;
            }

            public InvalidBlockException ValidateNextBlock(BlockChain<DummyAction> blocks, Block<DummyAction> nextBlock)
            {
                return null;
            }

            public HashAlgorithmType GetHashAlgorithm(long blockIndex) =>
                HashAlgorithmType.Of<SHA256>();
        }

        private class DummyAction : IAction
        {
            IValue IAction.PlainValue => Dictionary.Empty;

            IAccountStateDelta IAction.Execute(IActionContext context)
            {
                return context.PreviousStates;
            }

            void IAction.LoadPlainValue(IValue plainValue)
            {
            }
        }
    }
}
