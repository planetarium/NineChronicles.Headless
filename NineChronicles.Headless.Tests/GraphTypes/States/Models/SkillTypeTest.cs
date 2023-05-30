using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Execution;
using Nekoyume.Model.Skill;
using Nekoyume.Model.Stat;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class SkillTypeTest
    {
        [Theory]
        [InlineData(1, 1, 0, StatType.NONE)]
        [InlineData(20, 300, 0, StatType.NONE)]
        [InlineData(20, 300, 100, StatType.ATK)]
        [InlineData(20, 300, 1000, StatType.DEF)]
        [InlineData(20, 300, 1000, StatType.ArmorPenetration)]
        public async Task Query(int power, int chance, int statPowerRatio, StatType referencedStatType)
        {
            const string query = @"
            {
                id
                elementalType
                power
                chance
                statPowerRatio
                referencedStatType
            }";

            var row = Fixtures.TableSheetsFX.SkillSheet.OrderedList.First();
            var skill = SkillFactory.Get(row, power, chance, statPowerRatio, referencedStatType);
            var queryResult = await ExecuteQueryAsync<Headless.GraphTypes.States.Models.SkillType>(query, source: skill);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;

            // To upper snake case
            var refStatTypeString = referencedStatType.ToString();
            var expectedRefStatType = string.Concat(
                refStatTypeString.Select((x, i) =>
                    i > 0 &&
                    char.IsUpper(x) &&
                    (char.IsLower(refStatTypeString[i - 1]) ||
                    (i < refStatTypeString.Length - 1 && char.IsLower(refStatTypeString[i + 1])))
                        ? "_" + x
                        : x.ToString())).ToUpper();

            Assert.Equal(new Dictionary<string, object>
            {
                ["id"] = row.Id,
                ["elementalType"] = row.ElementalType.ToString().ToUpper(),
                ["power"] = power,
                ["chance"] = chance,
                ["statPowerRatio"] = statPowerRatio,
                ["referencedStatType"] = expectedRefStatType
            }, data);
        }
    }
}
