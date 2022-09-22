using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes
{
    public class TransferGovHistoryType : ObjectGraphType<TransferGovHistory>
    {
        public TransferGovHistoryType()
        {
            Field<NonNullGraphType<ByteStringType>>(
                name: "blockHash",
                resolve: context => context.Source.BlockHash.ToByteArray());
            Field<NonNullGraphType<ByteStringType>>(
                name: "txId",
                resolve: context => context.Source.TxId.ToByteArray());
            Field<NonNullGraphType<AddressType>>(
                name: "sender",
                resolve: context => context.Source.Sender);
            Field<NonNullGraphType<AddressType>>(
                name: "recipient",
                resolve: context => context.Source.Recipient);
            Field<NonNullGraphType<StringGraphType>>(
                name: "amount",
                resolve: context => context.Source.Amount.GetQuantityString());
            Field<StringGraphType>(
                name: "memo",
                resolve: context => context.Source.Memo);
        }
    }
}
