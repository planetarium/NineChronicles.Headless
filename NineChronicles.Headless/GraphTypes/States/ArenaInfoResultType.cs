using System.Collections.Generic;
using GraphQL.Types;

namespace NineChronicles.Headless.GraphTypes.States;

public class ArenaInfoResultType : ObjectGraphType<(List<ArenaParticipant> arenaParticipants, int purchasedCountDuringInterval, long lastBattleBlockIndex)>
{
    public ArenaInfoResultType()
    {
        Field<NonNullGraphType<ListGraphType<ArenaParticipantType>>>(
            name: "arenaParticipants",
            resolve: context => context.Source.arenaParticipants);
        Field<NonNullGraphType<IntGraphType>>(
            name: "purchasedCountDuringInterval",
            resolve: context => context.Source.purchasedCountDuringInterval);
        Field<NonNullGraphType<LongGraphType>>(
            name: "lastBattleBlockIndex",
            resolve: context => context.Source.lastBattleBlockIndex);
    }
}
