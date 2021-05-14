using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes
{
    public class TransferNCGHistoryType : ObjectGraphType<TransferNCGHistory>
    {
        public TransferNCGHistoryType()
        {
            Field<NonNullGraphType<AddressType>>(
                name: "sender",
                resolve: context => context.Source.Sender);
            Field<NonNullGraphType<AddressType>>(
                name: "recipient",
                resolve: context => context.Source.Recipient);
            Field<NonNullGraphType<StringGraphType>>(
                name: "amount",
                resolve: context => context.Source.Amount.GetQuantityString());
        }
    }
}
