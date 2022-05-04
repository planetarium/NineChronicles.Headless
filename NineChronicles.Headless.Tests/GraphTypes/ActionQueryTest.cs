using System.Collections;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using GraphQL.Execution;
using Libplanet;
using Libplanet.Crypto;
using Nekoyume.Action;
using NineChronicles.Headless.GraphTypes;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes
{
    public class ActionQueryTest
    {
        private readonly Codec _codec;

        public ActionQueryTest()
        {
            _codec = new Codec();
        }

        [Theory]
        [ClassData(typeof(StakeFixture))]
        public async Task Stake(BigInteger amount)
        {
            string query = $@"
            {{
                stake(amount: {amount})
            }}";

            var queryResult = await ExecuteQueryAsync<ActionQuery>(query);
            var data = (Dictionary<string, object>)((ExecutionNode) queryResult.Data!).ToValue()!;
            var action = new Stake(amount);
            var expected = new Dictionary<string, object>()
            {
                ["stake"] = ByteUtil.Hex(_codec.Encode(action.PlainValue)),
            };
            Assert.Equal(expected, data);
        }
        
        [Fact]
        public async Task ClaimStakeReward()
        {
            var avatarAddress = new PrivateKey().ToAddress();
            string query = $@"
            {{
                claimStakeReward(avatarAddress: ""{avatarAddress.ToString()}"")
            }}";

            var queryResult = await ExecuteQueryAsync<ActionQuery>(query);
            var data = (Dictionary<string, object>)((ExecutionNode) queryResult.Data!).ToValue()!;
            var plainValue = _codec.Decode(ByteUtil.ParseHex((string) data["claimStakeReward"]));
            Assert.IsType<Dictionary>(plainValue);
            var dictionary = (Dictionary) plainValue;
            Assert.Equal((Binary) dictionary[Lib9c.SerializeKeys.AvatarAddressKey], avatarAddress.ToByteArray());
        }

        private class StakeFixture : IEnumerable<object[]>
        {
            private readonly List<object[]> _data = new List<object[]>
            {
                new object[]
                {
                    new BigInteger(1),
                },
                new object[]
                {
                    new BigInteger(100),
                },
            };

            public IEnumerator<object[]> GetEnumerator() => _data.GetEnumerator();

            IEnumerator IEnumerable.GetEnumerator() => _data.GetEnumerator();
        }
    }
}
