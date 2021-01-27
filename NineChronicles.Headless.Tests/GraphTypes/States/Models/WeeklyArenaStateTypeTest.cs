using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using Libplanet;
using Libplanet.Action;
using Libplanet.Crypto;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Xunit;
using Xunit.Abstractions;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class WeeklyArenaStateTypeTest: GraphQLTestBase
    {
        public WeeklyArenaStateTypeTest(ITestOutputHelper output) : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(Members))]
        public async Task QueryWeeklyArenaState(bool arenaInfoExist, List<object> expected)
        {
            var userPrivateKey =
                new PrivateKey(ByteUtil.ParseHex("b934cb79757b1dec9f89caa01c4b791a6de6937dbecdc102fbdca217156cc2f5"));
            var minerAddress = new PrivateKey().PublicKey.ToAddress();

            if (arenaInfoExist)
            {
                var action = new SetAvatarState();
                BlockChain.MakeTransaction(
                    userPrivateKey,
                    new PolymorphicAction<ActionBase>[] { action }
                );
            }
            await BlockChain.MineBlock(minerAddress);

            const string query = @"query {
                stateQuery {
                    weeklyArena(index: 0) {
                        address
                        ended
                        orderedArenaInfos {
                            agentAddress
                            avatarAddress
                            arenaRecord {
                                win
                                lose
                                draw
                            }
                            level
                            combatPoint
                            armorId
                            active
                            dailyChallengeCount
                            score
                        }
                    }
                }
            }";

            var queryResult = await ExecuteQueryAsync(query);

            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["stateQuery"] = new Dictionary<string, object>
                    {
                        ["weeklyArena"] = new Dictionary<string, object>
                        {
                            ["address"] = WeeklyArenaState.DeriveAddress(0).ToString(),
                            ["ended"] = false,
                            ["orderedArenaInfos"] = expected,
                        },
                    },
                },
                queryResult.Data
            );
        }

        public static IEnumerable<object[]> Members => new List<object[]>
        {
            new object[]
            {
                true,
                new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["agentAddress"] = "0xfc2a412ea59122B114B672a5518Bc113955Dd2FE",
                        ["avatarAddress"] = "0x983c3Fbfe8243a0e36D55C6C1aE26A7c8Bb6CBd4",
                        ["arenaRecord"] = new Dictionary<string, object>
                        {
                            ["win"] = 0,
                            ["lose"] = 0,
                            ["draw"] = 0,
                        },
                        ["level"] = 1,
                        ["combatPoint"] = 1142,
                        ["armorId"] = 10200000,
                        ["active"] = false,
                        ["dailyChallengeCount"] = 5,
                        ["score"] = 1000,
                    }
                }
            },
            new object[]
            {
                false,
                new List<object>(),
            },
        };
    }
}
