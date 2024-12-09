using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using GraphQL.Execution;
using Libplanet.Types.Tx;
using Nekoyume.ValidatorDelegation;
using Xunit;
using Xunit.Abstractions;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class ValidatorTypeTest : GraphQLTestBase
    {
        public ValidatorTypeTest(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public async Task ExecuteQuery()
        {
            // Given
            var block = BlockChain.Tip;
            var blockHeight = 0L;
            var tip = new Domain.Model.BlockChain.Block(
                Hash: block.Hash,
                PreviousHash: null,
                Miner: ProposerPrivateKey.Address,
                Index: blockHeight,
                Timestamp: block.Timestamp,
                StateRootHash: block.StateRootHash,
                Transactions: ImmutableArray<Transaction>.Empty);
            var worldState = BlockChain.GetNextWorldState();
            var stateRootHash = block.StateRootHash;
            var validatorAddress = ProposerPrivateKey.Address.ToHex();
            var query =
                $"query {{\n" +
                $"  stateQuery(index: 0) {{\n" +
                $"    validator(validatorAddress: \"{validatorAddress}\") {{\n" +
                $"      power\n" +
                $"      isActive\n" +
                $"      totalShares\n" +
                $"      jailed\n" +
                $"      jailedUntil\n" +
                $"      tombstoned\n" +
                $"      commissionPercentage\n" +
                $"      totalDelegated {{\n" +
                $"        currency\n" +
                $"        quantity\n" +
                $"      }}\n" +
                $"    }}\n" +
                $"  }}\n" +
                $"}}\n";

            BlockChainRepository.Setup(repository => repository.GetBlock(blockHeight)).Returns(tip);
            WorldStateRepository.Setup(repository => repository.GetWorldState(stateRootHash)).Returns(worldState);

            // When
            var result = await ExecuteQueryAsync(query);

            // Then
            var data = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            var stateQueryResult = (Dictionary<string, object>)data["stateQuery"];
            var validatorResult = (Dictionary<string, object>)stateQueryResult["validator"];

            Assert.Equal("10,000,000,000,000,000,000", validatorResult["power"]);
            Assert.Equal(true, validatorResult["isActive"]);
            Assert.Equal("10,000,000,000,000,000,000", validatorResult["totalShares"]);
            Assert.Equal(false, validatorResult["jailed"]);
            Assert.Equal(-1L, validatorResult["jailedUntil"]);
            Assert.Equal(false, validatorResult["tombstoned"]);
            Assert.Equal($"{ValidatorDelegatee.DefaultCommissionPercentage}", validatorResult["commissionPercentage"]);

            var totalDelegatedResult = (Dictionary<string, object>)validatorResult["totalDelegated"];
            Assert.Equal("GUILD_GOLD", totalDelegatedResult["currency"]);
            Assert.Equal("10.000000000000000000", totalDelegatedResult["quantity"]);
        }
    }
}
