using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes
{
    public class TransferNCGHistoryType : ObjectGraphType<TransferNCGHistory>
    {
        public TransferNCGHistoryType()
        {
            Field<NonNullGraphType<ByteStringType>>("blockHash")
                .Resolve(context => context.Source.BlockHash.ToByteArray());
            Field<NonNullGraphType<ByteStringType>>("txId")
                .Resolve(context => context.Source.TxId.ToByteArray());
            Field<NonNullGraphType<AddressType>>("sender")
                .Resolve(context => context.Source.Sender);
            Field<NonNullGraphType<AddressType>>("recipient")
                .Resolve(context => context.Source.Recipient);
            Field<NonNullGraphType<StringGraphType>>("amount")
                .Resolve(context => context.Source.Amount.GetQuantityString());
            Field<StringGraphType>("memo")
                .Resolve(context => context.Source.Memo);
        }
    }
}
