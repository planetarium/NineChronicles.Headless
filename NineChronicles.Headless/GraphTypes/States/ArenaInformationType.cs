using System;
using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.Arena;

namespace NineChronicles.Headless.GraphTypes.States;

public class ArenaInformationType : ObjectGraphType<(Address, ArenaInformation, ArenaScore)>
{
    public ArenaInformationType()
    {
        Field<NonNullGraphType<AddressType>>(
            name: "avatarAddress",
            resolve: context => context.Source.Item1
        );
        Field<NonNullGraphType<AddressType>>(
            nameof(ArenaInformation.Address),
            resolve: context => context.Source.Item2.Address
        );
        Field<NonNullGraphType<IntGraphType>>(
            nameof(ArenaInformation.Win),
            resolve: context => context.Source.Item2.Win
        );
        Field<NonNullGraphType<IntGraphType>>(
            nameof(ArenaInformation.Lose),
            resolve: context => context.Source.Item2.Lose
        );
        Field<NonNullGraphType<IntGraphType>>(
            nameof(ArenaInformation.Ticket),
            resolve: context => context.Source.Item2.Ticket
        );
        Field<NonNullGraphType<IntGraphType>>(
            nameof(ArenaInformation.TicketResetCount),
            resolve: context => context.Source.Item2.TicketResetCount
        );
        Field<NonNullGraphType<IntGraphType>>(
            nameof(ArenaInformation.PurchasedTicketCount),
            resolve: context => context.Source.Item2.PurchasedTicketCount
        );
        Field<NonNullGraphType<IntGraphType>>(
            name: "score",
            resolve: context => context.Source.Item3.Score
        );
    }
}
