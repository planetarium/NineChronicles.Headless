using GraphQL.Types;
using Libplanet.Action;
using Libplanet.Tx;

namespace NineChronicles.Headless.GraphTypes
{
    public class TransactionType<T> : ObjectGraphType<Transaction<T>> where T : IAction, new()
    {
        public TransactionType()
        {
            Field<NonNullGraphType<TxIdType>>(
                nameof(Transaction<T>.Id),
                resolve: context => context.Source.Id
            );
            Field<NonNullGraphType<LongGraphType>>(
                nameof(Transaction<T>.Nonce),
                resolve: context => context.Source.Nonce
            );
            Field<NonNullGraphType<PublicKeyType>>(
                nameof(Transaction<T>.PublicKey),
                resolve: context => context.Source.PublicKey
            );
            Field<NonNullGraphType<ByteStringType>>(
                nameof(Transaction<T>.Signature),
                resolve: context => context.Source.Signature
            );
            Field<NonNullGraphType<AddressType>>(
                nameof(Transaction<T>.Signer),
                resolve: context => context.Source.Signer
            );
            Field<NonNullGraphType<StringGraphType>>(
                nameof(Transaction<T>.Timestamp),
                resolve: context => context.Source.Timestamp
            );
            Field<NonNullGraphType<ListGraphType<AddressType>>>(
                nameof(Transaction<T>.UpdatedAddresses),
                resolve: context => context.Source.UpdatedAddresses
            );
            Field<NonNullGraphType<ListGraphType<ActionType<T>>>>(
                nameof(Transaction<T>.Actions),
                resolve: context => context.Source.Actions
            );
        }
    }
}
