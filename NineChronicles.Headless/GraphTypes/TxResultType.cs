using System.ComponentModel;
using Bencodex;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blocks;
using Libplanet.Tx;

namespace NineChronicles.Headless.GraphTypes
{
    public class TransactionResultType : ObjectGraphType<TransactionResult>
    {
        private static readonly Codec Codec = new Codec();

        public TransactionResultType()
        {
            Field<NonNullGraphType<TransactionStatusType>>(nameof(TransactionResult.TransactionStatus))
                .Description("The transaction status.")
                .Resolve(context => context.Source.TransactionStatus);

            Field<LongGraphType>(nameof(TransactionResult.BlockIndex))
                .Description("The block index which the target transaction executed.")
                .Resolve(context => context.Source.BlockIndex);

            Field<StringGraphType>(nameof(TransactionResult.BlockHash))
                .Description("The block hash which the target transaction executed.")
                .Resolve(context => context.Source.BlockHash);

            Field<StringGraphType>(nameof(TransactionResult.ExceptionName))
                .Description(
                    "The name of the exception when the transaction failed. " +
                        "There will be a value when only the transaction fails.")
                .Resolve(context => context.Source.ExceptionName);

            Field<StringGraphType>(nameof(TransactionResult.ExceptionMetadata))
                .Description(
                    "A hexadecimal string to present the metadata of the exception when the transaction failed. " +
                        "It is a Bencodex value. There will be a value when only the transaction fails.")
                .Resolve(context => context.Source.ExceptionMetadata is { } metadata
                    ? ByteUtil.Hex(Codec.Encode(metadata))
                    : null);
        }
    }
}
