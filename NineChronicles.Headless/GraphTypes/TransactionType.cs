using GraphQL.Types;
using Libplanet.Action;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Tx;

namespace NineChronicles.Headless.GraphTypes
{
    public class TransactionType<T> : ObjectGraphType<Transaction<T>> where T : IAction, new()
    {
        public TransactionType()
        {
            Field<NonNullGraphType<TxIdType>>(
                nameof(Transaction<T>.Id),
                description: "A unique identifier derived from this transaction content.",
                resolve: context => context.Source.Id
            );
            Field<NonNullGraphType<LongGraphType>>(
                nameof(Transaction<T>.Nonce),
                description: "The number of previous transactions committed by the signer of this transaction.",
                resolve: context => context.Source.Nonce
            );
            Field<NonNullGraphType<PublicKeyType>>(
                nameof(Transaction<T>.PublicKey),
                description: "A PublicKey of the account who signs this transaction.",
                resolve: context => context.Source.PublicKey
            );
            Field<NonNullGraphType<ByteStringType>>(
                nameof(Transaction<T>.Signature),
                description: "A digital signature of the content of this Transaction<T>.",
                resolve: context => context.Source.Signature
            );
            Field<NonNullGraphType<AddressType>>(
                nameof(Transaction<T>.Signer),
                description: "An address of the account who signed this transaction.",
                resolve: context => context.Source.Signer
            );
            Field<NonNullGraphType<StringGraphType>>(
                nameof(Transaction<T>.Timestamp),
                description: "The time this transaction was created and signed.",
                resolve: context => context.Source.Timestamp
            );
            Field<NonNullGraphType<ListGraphType<AddressType>>>(
                nameof(Transaction<T>.UpdatedAddresses),
                description: "Addresses whose states were affected by Actions.",
                resolve: context => context.Source.UpdatedAddresses
            );
            Field<NonNullGraphType<ListGraphType<ActionType<T>>>>(
                nameof(Transaction<T>.Actions),
                description: "A list of actions in this transaction.",
                resolve: context => context.Source.Actions
            );
        }
    }
}
