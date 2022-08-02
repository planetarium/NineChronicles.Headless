using System.Collections.Generic;
using System.Threading.Tasks;
using GraphQL.Execution;
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
                avatarAddress
                arenaRecord {
                    win
                    lose
                    draw
                }
                active
                dailyChallengeCount
                score
            }";

            var arenaInfo = new ArenaInfo(Fixtures.AvatarStateFX, Fixtures.TableSheetsFX.CharacterSheet, active);
            var queryResult = await ExecuteQueryAsync<ArenaInfoType>(query, source: arenaInfo);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;

            Assert.Equal(
                new Dictionary<string, object>
                {
                    ["avatarAddress"] = "0x983c3Fbfe8243a0e36D55C6C1aE26A7c8Bb6CBd4",
                    ["arenaRecord"] = new Dictionary<string, object>
                    {
                        ["win"] = 0,
                        ["lose"] = 0,
                        ["draw"] = 0,
                    },
                    ["active"] = active,
                    ["dailyChallengeCount"] = 5,
                    ["score"] = 1000,
                },
                data
            );
        }
    }
}
