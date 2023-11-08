using System.Collections.Generic;
using GraphQL.Types;
using Nekoyume.Model.Arena;

namespace NineChronicles.Headless.GraphTypes.States;

public class ArenaParticipantsResultType : ObjectGraphType<(List<ArenaParticipant> arenaParticipants, ArenaInformation arenaInformation, int purchasedCountDuringInterval, long lastBattleBlockIndex)>
{
    public ArenaParticipantsResultType()
    {
        Field<NonNullGraphType<ListGraphType<ArenaParticipantType>>>(
            name: "arenaParticipants",
            resolve: context => context.Source.arenaParticipants);
        Field<NonNullGraphType<ArenaInformationObjectType>>(
            name: "arenaInformation",
            resolve: context => context.Source.arenaInformation);
        Field<NonNullGraphType<IntGraphType>>(
            name: "purchasedCountDuringInterval",
            resolve: context => context.Source.purchasedCountDuringInterval);
        Field<NonNullGraphType<LongGraphType>>(
            name: "lastBattleBlockIndex",
            resolve: context => context.Source.lastBattleBlockIndex);
    }
}
