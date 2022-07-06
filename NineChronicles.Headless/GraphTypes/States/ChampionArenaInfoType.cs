using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.State;

namespace NineChronicles.Headless.GraphTypes.States
{
    internal class ChampionArenaInfoType : ObjectGraphType<ChampionArenaInfo>
    {
        public ChampionArenaInfoType()
        {
            Field<NonNullGraphType<AddressType>>(
                nameof(ChampionArenaInfo.AgentAddress),
                resolve: context => context.Source.AgentAddress);
            Field<NonNullGraphType<AddressType>>(
                nameof(ChampionArenaInfo.AvatarAddress),
                resolve: context => context.Source.AvatarAddress);
            Field<NonNullGraphType<StringGraphType>>(
                nameof(ChampionArenaInfo.AvatarName),
                resolve: context => context.Source.AvatarName);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(ChampionArenaInfo.Lose),
                resolve: context => context.Source.Lose);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(ChampionArenaInfo.Win),
                resolve: context => context.Source.Win);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(ChampionArenaInfo.PurchasedTicketCount),
                resolve: context => context.Source.PurchasedTicketCount);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(ChampionArenaInfo.TicketResetCount),
                resolve: context => context.Source.TicketResetCount);
            Field<NonNullGraphType<BooleanGraphType>>(
                nameof(ChampionArenaInfo.Active),
                resolve: context => context.Source.Active);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(ChampionArenaInfo.Ticket),
                resolve: context => context.Source.Ticket);
            Field<NonNullGraphType<IntGraphType>>(
                nameof(ChampionArenaInfo.Score),
                resolve: context => context.Source.Score);
        }
    }
}
