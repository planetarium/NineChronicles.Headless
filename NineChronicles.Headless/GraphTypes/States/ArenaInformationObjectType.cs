using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Model.Arena;

namespace NineChronicles.Headless.GraphTypes.States;

public class ArenaInformationObjectType : ObjectGraphType<ArenaInformation>
{
    public ArenaInformationObjectType()
    {
        Field<NonNullGraphType<AddressType>>(
            nameof(ArenaInformation.Address),
            resolve: context => context.Source.Address
        );
        Field<NonNullGraphType<IntGraphType>>(
            nameof(ArenaInformation.Win),
            resolve: context => context.Source.Win
        );
        Field<NonNullGraphType<IntGraphType>>(
            nameof(ArenaInformation.Lose),
            resolve: context => context.Source.Lose
        );
        Field<NonNullGraphType<IntGraphType>>(
            nameof(ArenaInformation.Ticket),
            resolve: context => context.Source.Ticket
        );
        Field<NonNullGraphType<IntGraphType>>(
            nameof(ArenaInformation.TicketResetCount),
            resolve: context => context.Source.TicketResetCount
        );
        Field<NonNullGraphType<IntGraphType>>(
            nameof(ArenaInformation.PurchasedTicketCount),
            resolve: context => context.Source.PurchasedTicketCount
        );
    }
}
