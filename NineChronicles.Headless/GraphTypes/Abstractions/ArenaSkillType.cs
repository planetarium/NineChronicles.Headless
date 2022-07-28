using GraphQL.Types;
using Nekoyume.Model.BattleStatus.Arena;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NineChronicles.Headless.GraphTypes.Abstractions
{
    internal class ArenaSkillType : ObjectGraphType<ArenaSkill>
    {
        public ArenaSkillType()
        {
            Field<NonNullGraphType<ArenaCharacterType>>(
                nameof(ArenaEventBase.Character),
                resolve: context => context.Source.Character
            );
            Field<ListGraphType<ArenaSkillInfoType>>(
                nameof(ArenaSkill.SkillInfos),
                resolve: context => context.Source.SkillInfos);
            Field<ListGraphType<ArenaSkillInfoType>>(
                nameof(ArenaSkill.BuffInfos),
                resolve: context => context.Source.BuffInfos);
        }
    }
}
