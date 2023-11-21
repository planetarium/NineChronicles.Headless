using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;

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
            description: "Arena score of avatar.",
            resolve: context => context.Source.Score);
        Field<NonNullGraphType<IntGraphType>>(
            nameof(ArenaParticipant.Rank),
            description: "Arena rank of avatar.",
            resolve: context => context.Source.Rank);
        Field<NonNullGraphType<IntGraphType>>(
            nameof(ArenaParticipant.WinScore),
            description: "Score for victory.",
            resolve: context => context.Source.WinScore);
        Field<NonNullGraphType<IntGraphType>>(
            nameof(ArenaParticipant.LoseScore),
            description: "Score for defeat.",
            resolve: context => context.Source.LoseScore);
        Field<NonNullGraphType<IntGraphType>>(
            nameof(ArenaParticipant.Cp),
            description: "Cp of avatar.",
            resolve: context => context.Source.Cp);
        Field<NonNullGraphType<IntGraphType>>(
            nameof(ArenaParticipant.PortraitId),
            description: "Portrait icon id.",
            resolve: context => context.Source.PortraitId);
        Field<NonNullGraphType<IntGraphType>>(
            nameof(ArenaParticipant.Level),
            description: "Level of avatar.",
            resolve: context => context.Source.Level);
        Field<NonNullGraphType<StringGraphType>>(
            nameof(ArenaParticipant.NameWithHash),
            description: "Name of avatar.",
            resolve: context => context.Source.NameWithHash);
    }
}
