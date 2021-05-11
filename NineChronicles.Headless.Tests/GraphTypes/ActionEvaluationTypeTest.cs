using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading.Tasks;
using Bencodex;
using GraphQL;
using Libplanet;
using Nekoyume.Action;
using NineChronicles.Headless.GraphTypes;
using Xunit;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class ActionEvaluationTypeTest
    {
        [Fact]
        public async Task Query()
        {
            var action = new Buy
            {
                purchaseInfos = ImmutableArray<PurchaseInfo>.Empty,
                buyerAvatarAddress = default,
                buyerMultipleResult = default,
                sellerMultipleResult = default,
            };
            const long blockIndex = 1000;
  
            var actionEvaluation = new ActionBase.ActionEvaluation<ActionBase>
            {
                Action = action,
                Signer = default,
                BlockIndex = blockIndex,
            };

            var result = await GraphQLTestUtils.ExecuteQueryAsync<ActionEvaluationType>(@"
            query {
                action
                signer
                blockIndex
            }
            ", source: actionEvaluation);

            var expected = new Dictionary<string, object>
            {
                ["action"] = ByteUtil.Hex(new Codec().Encode(action.PlainValue)),
                ["signer"] = default(Address).ToString(),
                ["blockIndex"] = blockIndex,
            };
            Dictionary<string, object> actual = result.Data.As<Dictionary<string, object>>();
            Assert.Equal(expected, actual);
        }
    }
}
