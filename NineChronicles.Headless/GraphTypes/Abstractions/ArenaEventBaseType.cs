using GraphQL.Types;
using Nekoyume.Model;
using Nekoyume.Model.BattleStatus.Arena;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NineChronicles.Headless.GraphTypes.Abstractions
{
    internal class ArenaEventBaseType : ObjectGraphType<ArenaEventBase>
    {
        public ArenaEventBaseType()
        {
            Field<StringGraphType>(
                "EventType", 
                resolve: context => context.Source.GetType().Name
            );
            Field<NonNullGraphType<ArenaCharacterType>>(
                nameof(ArenaEventBase.Character),
                resolve: context => context.Source.Character
            );
            Field<ArenaSkillType>(
                nameof(ArenaSkill),
                resolve: context =>
                {
                    if (context.Source is ArenaSkill skill)
                    {

                        return skill;
                    }
                    else
                    {
                        return null;
                    }
                }
            );
            Field<StringGraphType>(
                "Text",
                resolve: context =>
                {
                    var sb = new StringBuilder();
                    var arenaEvent = context.Source;
                    if (arenaEvent is ArenaSkill arenaSkillEvent)
                    {
                        sb.AppendLine($"Arena Attack {arenaSkillEvent.GetType()}");
                        if (arenaSkillEvent is ArenaBuff arenaBuffEvent)
                        {
                            sb.AppendLine($"Buff {arenaBuffEvent.SkillInfos.First().SkillCategory}");
                        }
                        else
                        {
                            foreach (var hit in arenaSkillEvent.SkillInfos)
                            {
                                sb.AppendLine($"{(arenaSkillEvent.SkillInfos.First().Target.IsEnemy ? "Enemy " : "Player ")} Damaged for: {hit.Effect} - Crit? {hit.Critical}");
                            }
                            sb.AppendLine($"Total Damage: {arenaSkillEvent.SkillInfos.Sum(ae => ae.Effect)}");
                            sb.AppendLine($"Target HP Remaining: {arenaSkillEvent.SkillInfos.Last().Target.CurrentHP}");
                        }
                    }
                    else if (arenaEvent is ArenaTurnEnd arenaTurnEnd)
                    {
                        sb.AppendLine($"Turn #: {arenaTurnEnd.TurnNumber}; Remaining HP: {arenaTurnEnd.Character.CurrentHP}");
                    }
                    else
                    {
                        sb.AppendLine(arenaEvent.ToString());
                    }
                    return sb.ToString();
                });
        }

    }
}
