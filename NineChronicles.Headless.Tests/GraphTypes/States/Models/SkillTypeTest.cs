using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Execution;
using Nekoyume.Model.Skill;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class SkillTypeTest
    {
        [Theory]
        [InlineData(1, 1)]
        [InlineData(20, 300)]
        public async Task Query(int power, int chance)
        {
            const string query = @"
            {
                id
                elementalType
                power
                chance
            }";

            var row = Fixtures.TableSheetsFX.SkillSheet.OrderedList.First();
            var skill = SkillFactory.Get(row, power, chance);
            var queryResult = await ExecuteQueryAsync<Headless.GraphTypes.States.Models.SkillType>(query, source: skill);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal(new Dictionary<string, object>
            {
                ["id"] = row.Id,
                ["elementalType"] = row.ElementalType.ToString().ToUpper(),
                ["power"] = power,
                ["chance"] = chance,
            }, data);

        }
    }
}
