using System;
using System.Collections.Generic;
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
    public class RankingMapStateTypeTest: GraphQLTestBase
    {
        public RankingMapStateTypeTest(ITestOutputHelper output) : base(output)
        {
        }

        [Theory]
        [MemberData(nameof(Members))]
        public async Task QueryRankingMapState(bool rankingInfoExist, List<object> expected)
        {
            var userPrivateKey =
                new PrivateKey(ByteUtil.ParseHex("b934cb79757b1dec9f89caa01c4b791a6de6937dbecdc102fbdca217156cc2f5"));
            var minerAddress = new PrivateKey().PublicKey.ToAddress();
            var avatarAddress = new Address("983c3Fbfe8243a0e36D55C6C1aE26A7c8Bb6CBd4");

            const string query = @"query {
                stateQuery {
                    rankingMap(index: 0) {
                        address
                        capacity
                        rankingInfos {
                            agentAddress
                            avatarAddress
                        }
                    }
                }
            }";
            var rankingMapAddress = RankingState.Derive(0);
           if (rankingInfoExist)
            {
                var action = new SetAvatarState();
                var action2 = new HackAndSlash4
                {
                    avatarAddress = avatarAddress,
                    costumes = new List<Guid>(),
                    equipments = new List<Guid>(),
                    foods = new List<Guid>(),
                    RankingMapAddress = rankingMapAddress,
                    stageId = 1,
                    worldId = 1,
                    WeeklyArenaAddress = WeeklyArenaState.DeriveAddress(0),
                };
                BlockChain.MakeTransaction(
                    userPrivateKey,
                    new PolymorphicAction<ActionBase>[] { action }
                );
                await BlockChain.MineBlock(minerAddress);

                BlockChain.MakeTransaction(
                    userPrivateKey,
                    new PolymorphicAction<ActionBase>[] { action2 }
                );
                await BlockChain.MineBlock(minerAddress);
            }

            var queryResult = await ExecuteQueryAsync(query);
            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["stateQuery"] = new Dictionary<string, object>
                    {
                        ["rankingMap"] = new Dictionary<string, object>
                        {
                            ["address"] = rankingMapAddress.ToString(),
                            ["capacity"] = RankingMapState.Capacity,
                            ["rankingInfos"] = expected,
                        }
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
                new List<object>
                {
                    new Dictionary<string, object>
                    {
                        ["agentAddress"] = "0xfc2a412ea59122B114B672a5518Bc113955Dd2FE",
                        ["avatarAddress"] = "0x983c3Fbfe8243a0e36D55C6C1aE26A7c8Bb6CBd4",
                    },
                },
            },
            new object[]
            {
                false,
                new List<object>(),
            },
        };
    }
}
