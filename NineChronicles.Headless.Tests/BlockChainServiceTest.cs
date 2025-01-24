using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Bencodex.Types;
using Lib9c.Renderers;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Policies;
using Libplanet.Crypto;
using Libplanet.Headless.Hosting;
using Libplanet.Mocks;
using Libplanet.Types.Tx;
using Moq;
using Nekoyume.Action;
using NineChronicles.Headless.Repositories.BlockChain;
using NineChronicles.Headless.Repositories.Swarm;
using NineChronicles.Headless.Repositories.Transaction;
using NineChronicles.Headless.Repositories.WorldState;
using Xunit;

namespace NineChronicles.Headless.Tests
{
    public class BlockChainServiceTest
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task PutTransaction(bool stageResult)
        {
            // Arrange
            var mockBlockChain = new Mock<IBlockChainRepository>();
            var mockTransaction = new Mock<ITransactionRepository>();
            var mockWorld = new Mock<IWorldStateRepository>();
            var mockSwarm = new Mock<ISwarmRepository>();
            var mockRpcContext = new Mock<RpcContext>();
            var mockProperties = new Mock<LibplanetNodeServiceProperties>();
            var mockCache = new Mock<StateMemoryCache>();
            var mockPolicy = new Mock<IBlockPolicy>();

            // Mocking dependencies
            mockPolicy
                .Setup(bc => bc.ValidateNextBlockTx(It.IsAny<BlockChain>(), It.IsAny<Transaction>()))
                .Returns((TxPolicyViolationException?)null);
            mockBlockChain
                .Setup(bc => bc.StageTransaction(It.IsAny<Transaction>()))
                .Returns(stageResult);
            mockBlockChain
                .Setup(bc => bc.GetStagedTransactionIds())
                .Returns(ImmutableHashSet<TxId>.Empty);

            var service = new BlockChainService(
                mockBlockChain.Object,
                mockTransaction.Object,
                mockWorld.Object,
                mockSwarm.Object,
                mockRpcContext.Object,
                mockProperties.Object,
                new ActionEvaluationPublisher(
                    new BlockRenderer(),
                    new ActionRenderer(),
                    new ExceptionRenderer(),
                    new NodeStatusRenderer(),
                    new MockBlockChainStates(),
                    "",
                    0,
                    new RpcContext(),
                    new StateMemoryCache()
                ),
                mockCache.Object);

            var tx = Transaction.Create(0, new PrivateKey(), null, new List<IValue>
            {
                new DailyReward
                {
                    avatarAddress = new Address(),
                }.PlainValue,
            }); // Create a valid transaction

            // Act
            var result = await service.PutTransaction(tx.Serialize());

            // Assert
            Assert.Equal(stageResult, result);
            mockBlockChain.Verify(bc => bc.StageTransaction(It.IsAny<Transaction>()), Times.Once);
            mockSwarm.Verify(s => s.BroadcastTxs(It.IsAny<Transaction[]>()), Times.Once);
        }
    }
}
