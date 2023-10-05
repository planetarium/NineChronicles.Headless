using System;
using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Types.Tx;

namespace NineChronicles.Headless.GraphTypes
{
    public class TransactionType : ObjectGraphType<Transaction>
    {
        public TransactionType()
        {
            Field<NonNullGraphType<TxIdType>>(
                nameof(Transaction.Id),
                description: "A unique identifier derived from this transaction content.",
                resolve: context => context.Source.Id
            );
            Field<NonNullGraphType<LongGraphType>>(
                nameof(Transaction.Nonce),
                description: "The number of previous transactions committed by the signer of this transaction.",
                resolve: context => context.Source.Nonce
            );
            Field<NonNullGraphType<PublicKeyType>>(
                nameof(Transaction.PublicKey),
                description: "A PublicKey of the account who signed this transaction.",
                resolve: context => context.Source.PublicKey
            );
            Field<NonNullGraphType<ByteStringType>>(
                nameof(Transaction.Signature),
                description: "A digital signature of the content of this transaction.",
                resolve: context => context.Source.Signature
            );
            Field<NonNullGraphType<AddressType>>(
                nameof(Transaction.Signer),
                description: "An address of the account who signed this transaction.",
                resolve: context => context.Source.Signer
            );
            Field<NonNullGraphType<StringGraphType>>(
                nameof(Transaction.Timestamp),
                description: "The time this transaction was created and signed.",
                resolve: context => context.Source.Timestamp
            );
            Field<NonNullGraphType<ListGraphType<AddressType>>>(
                nameof(Transaction.UpdatedAddresses),
                description: "Addresses whose states were affected by Actions.",
                resolve: context => context.Source.UpdatedAddresses
            );
            Field<NonNullGraphType<ListGraphType<ActionType>>>(
                nameof(Transaction.Actions),
                description: "A list of actions in this transaction.",
                resolve: context => context.Source.Actions
            );

            Field<NonNullGraphType<StringGraphType>>(
                name: "SerializedPayload",
                description: "A serialized tx payload in base64 string.",
                resolve: x =>
                {
                    byte[] bytes = x.Source.Serialize();
                    return Convert.ToBase64String(bytes);
                });
        }
    }
}
