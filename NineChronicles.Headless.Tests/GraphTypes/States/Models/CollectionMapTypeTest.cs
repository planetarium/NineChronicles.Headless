using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL.Execution;
using Nekoyume.Model;
using NineChronicles.Headless.GraphTypes.States.Models;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class CollectionMapTypeTest
    {
        [Theory]
        [MemberData(nameof(QueryPairs))]
        public async Task Query(int[][] pairs)
        {
            var collectionMap = new CollectionMap();
            foreach (int[] pair in pairs)
            {
                collectionMap.Add(
                    pair[0],
                    pair[1]);
            }
            var result = await ExecuteQueryAsync<CollectionMapType>(
                "{ count pairs }",
                source: collectionMap);
            var resultData = (Dictionary<string, object>)((ExecutionNode)result.Data!).ToValue()!;
            Assert.Equal(pairs.Length, resultData["count"]);
            Assert.Equal(pairs, resultData["pairs"]);
        }

        public static IEnumerable<object[]> QueryPairs => new[]
        {
            new[]
            {
               new int[][] {},
            },
            new[]
            {
                new[]
                {
                    new[] { 1, 2, },
                    new[] { 3, 4, },
                },
            },
        };
    }
}
