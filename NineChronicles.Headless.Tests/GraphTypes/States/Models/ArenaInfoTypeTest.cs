using System.Collections.Generic;
using System.Threading.Tasks;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class ArenaInfoTypeTest
    {
        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Query(bool active)
        {
            const string query = @"
            {
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
            }";

            var arenaInfo = new ArenaInfo(Fixtures.AvatarStateFX, Fixtures.TableSheetsFX.CharacterSheet, active);
            var queryResult = await ExecuteQueryAsync<ArenaInfoType>(query, source: arenaInfo);

            Assert.Equal(
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
                    ["active"] = active,
                    ["dailyChallengeCount"] = 5,
                    ["score"] = 1000,
                },
                queryResult.Data
            );
        }
    }
}
