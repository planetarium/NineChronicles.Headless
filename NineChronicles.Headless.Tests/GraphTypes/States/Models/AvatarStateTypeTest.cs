using System.Collections.Generic;
using System.Threading.Tasks;
using Libplanet;
using Libplanet.Action;
using Libplanet.Crypto;
using Nekoyume.Action;
using Xunit;
using Xunit.Abstractions;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class AvatarStateTypeTest : GraphQLTestBase
    {
        public AvatarStateTypeTest(ITestOutputHelper output) : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(Members))]
        public async Task QueryAvatarState(bool avatarExist, object avatar)
        {
            var userPrivateKey =
                new PrivateKey(ByteUtil.ParseHex("b934cb79757b1dec9f89caa01c4b791a6de6937dbecdc102fbdca217156cc2f5"));
            var minerAddress = new PrivateKey().PublicKey.ToAddress();
            var avatarAddress = new Address("983c3Fbfe8243a0e36D55C6C1aE26A7c8Bb6CBd4");
            var query = $@"query {{
                stateQuery {{
                    avatar(address: ""{avatarAddress}"") {{
                        address
                        agentAddress
                    }}
                }}
            }}";

            if (avatarExist)
            {
                var action = new SetAvatarState();
                BlockChain.MakeTransaction(
                    userPrivateKey,
                    new PolymorphicAction<ActionBase>[] {action}
                );
                await BlockChain.MineBlock(minerAddress);
            }

            var queryResult = await ExecuteQueryAsync(query);
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["stateQuery"] = new Dictionary<string, object>
                    {
                        ["avatar"] = avatar,
                    }
                },
                queryResult.Data
            );
        }

        public static IEnumerable<object[]> Members => new List<object[]>
        {
            new object[]
            {
                true,
                new Dictionary<string, object>
                {
                    ["address"] = "0x983c3Fbfe8243a0e36D55C6C1aE26A7c8Bb6CBd4",
                    ["agentAddress"] = "0xfc2a412ea59122B114B672a5518Bc113955Dd2FE",
                },
            },
            new object[]
            {
                false,
                null,
            },
        };
    }
}
