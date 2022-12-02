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
            Field<NonNullGraphType<TxIdType>>(nameof(Transaction<T>.Id))
                .Description("A unique identifier derived from this transaction content.")
                .Resolve(context => context.Source.Id);
            Field<NonNullGraphType<LongGraphType>>(nameof(Transaction<T>.Nonce))
                .Description("The number of previous transactions committed by the signer of this transaction.")
                .Resolve(context => context.Source.Nonce);
            Field<NonNullGraphType<PublicKeyType>>(nameof(Transaction<T>.PublicKey))
                .Description("A PublicKey of the account who signed this transaction.")
                .Resolve(context => context.Source.PublicKey);
            Field<NonNullGraphType<ByteStringType>>(nameof(Transaction<T>.Signature))
                .Description("A digital signature of the content of this transaction.")
                .Resolve(context => context.Source.Signature);
            Field<NonNullGraphType<AddressType>>(nameof(Transaction<T>.Signer))
                .Description("An address of the account who signed this transaction.")
                .Resolve(context => context.Source.Signer);
            Field<NonNullGraphType<DateTimeOffsetGraphType>>(nameof(Transaction<T>.Timestamp))
                .Description("The time this transaction was created and signed.")
                .Resolve(context => context.Source.Timestamp);
            Field<NonNullGraphType<ListGraphType<AddressType>>>(nameof(Transaction<T>.UpdatedAddresses))
                .Description("Addresses whose states were affected by Actions.")
                .Resolve(context => context.Source.UpdatedAddresses);
            Field<NonNullGraphType<ListGraphType<ActionType<T>>>>(nameof(Transaction<T>.CustomActions))
                .Description("A list of actions in this transaction.")
                .Resolve(context => context.Source.CustomActions);
        }
    }
}
