using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes.States;

namespace NineChronicles.Headless.GraphTypes;

public class ArenaParticipantType : ObjectGraphType<ArenaParticipant>
{
    public ArenaParticipantType()
    {
        Field<NonNullGraphType<AddressType>>(
            nameof(ArenaParticipant.AvatarAddr),
            description: "Address of avatar.",
            resolve: context => context.Source.AvatarAddr);
        Field<NonNullGraphType<IntGraphType>>(
            nameof(ArenaParticipant.Score),
            description: "Address of avatar.",
            resolve: context => context.Source.Score);
        Field<NonNullGraphType<IntGraphType>>(
            nameof(ArenaParticipant.Rank),
            description: "Address of avatar.",
            resolve: context => context.Source.Rank);
        Field<NonNullGraphType<AvatarStateType>>(
            nameof(ArenaParticipant.AvatarState),
            description: "Address of avatar.",
            resolve: context => context.Source.AvatarState);
        Field<NonNullGraphType<ListGraphType<RuneStateType>>>(
            nameof(ArenaParticipant.RuneStates),
            description: "Address of avatar.",
            resolve: context => context.Source.RuneStates);
        Field<NonNullGraphType<IntGraphType>>(
            nameof(ArenaParticipant.ExpectDeltaScore.win),
            description: "Address of avatar.",
            resolve: context => context.Source.ExpectDeltaScore.win);
        Field<NonNullGraphType<IntGraphType>>(
            nameof(ArenaParticipant.ExpectDeltaScore.lose),
            description: "Address of avatar.",
            resolve: context => context.Source.ExpectDeltaScore.lose);
    }
}
