using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using GraphQL.Execution;
using Nekoyume.Model.Item;
using Nekoyume.Model.Skill;
using Nekoyume.Model.Stat;
using NineChronicles.Headless.GraphTypes.States.Models.Item;
using Xunit;
using static NineChronicles.Headless.Tests.GraphQLTestUtils;

namespace NineChronicles.Headless.Tests.GraphTypes.States.Models
{
    public class EquipmentTypeTest
    {
        [Fact]
        public async Task Query()
        {
            const string query = @"
            {
                id
                grade
                level
                elementalType
                setId
                itemId
                stat {
                    statType
                    baseValue
                    additionalValue
                    totalValue
                }
                statsMap {
                    hP
                    aTK
                    dEF
                    cRI
                    hIT
                    sPD
                }
                skills {
                    id
                    elementalType
                    chance
                    power
                    statPowerRatio
                    referencedStatType
                }
                requiredBlockIndex
            }";

            var row = Fixtures.TableSheetsFX.EquipmentItemSheet.OrderedList.First(r => r.ItemSubType == ItemSubType.Weapon);
            var equipment = new Weapon(row, Guid.NewGuid(), 10L);
            var skillRow = Fixtures.TableSheetsFX.SkillSheet.OrderedList.First();
            var skill = SkillFactory.Get(skillRow, 1, 1, 100, StatType.HP);
            equipment.Skills.Add(skill);

            var queryResult = await ExecuteQueryAsync<EquipmentType>(query, source: equipment);
            Assert.Null(queryResult.Errors);
            var data = (Dictionary<string, object>)((ExecutionNode)queryResult.Data!).ToValue()!;
            Assert.Equal(10L, data["requiredBlockIndex"]);
        }
    }
}
