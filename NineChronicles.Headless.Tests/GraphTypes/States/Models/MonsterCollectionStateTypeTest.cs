using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States;
using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL.Execution;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class MonsterCollectionStateTypeTest
    {
        [Fact]
        public async Task Query()
        {
            const string query = @"{
                address
                level
                expiredBlockIndex
                startedBlockIndex
                receivedBlockIndex
                claimableBlockIndex
            }";
            var state = new MonsterCollectionState(
                default,
                1,
                2,
                Fixtures.TableSheetsFX.MonsterCollectionRewardSheet
            );
            var queryResult = await ExecuteQueryAsync<MonsterCollectionStateType>(query, source: state);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            var expected = new Dictionary<string, object>
            {
                ["address"] = state.address.ToString(),
                ["level"] = 1L,
                ["expiredBlockIndex"] = 201600L + 2L,
                ["startedBlockIndex"] = 2L,
                ["receivedBlockIndex"] = 0L,
                ["claimableBlockIndex"] = 50400L + 2L,
            };
            Assert.Equal(expected, data);
        }
    }
}
