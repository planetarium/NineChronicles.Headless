using System.ComponentModel;
using Bencodex;
using GraphQL.Types;
using Libplanet.Common;
using Libplanet.Action;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;

namespace NineChronicles.Headless.GraphTypes
{
    public class TransactionResultType : ObjectGraphType<TransactionResult>
    {
        private static readonly Codec Codec = new Codec();

        public TransactionResultType()
        {
            Field<NonNullGraphType<TransactionStatusType>>(
                nameof(TransactionResult.TransactionStatus),
                description: "The transaction status.",
                resolve: context => context.Source.TransactionStatus
            );

            Field<LongGraphType>(
                nameof(TransactionResult.BlockIndex),
                description: "The block index which the target transaction executed.",
                resolve: context => context.Source.BlockIndex
            );

            Field<StringGraphType>(
                nameof(TransactionResult.BlockHash),
                description: "The block hash which the target transaction executed.",
                resolve: context => context.Source.BlockHash
            );

            Field<StringGraphType>(
                nameof(TransactionResult.ExceptionName),
                description: "The name of the exception when the transaction failed. "
                    + "There will be a value when only the transaction fails.",
                resolve: context => context.Source.ExceptionName
            );

            Field<StringGraphType>(
                nameof(TransactionResult.ExceptionMetadata),
                description: "A hexadecimal string to present the metadata of the exception when the transaction failed. "
                    + "It is a Bencodex value. There will be a value when only the transaction fails.",
                resolve: context => context.Source.ExceptionMetadata is { } metadata ? ByteUtil.Hex(Codec.Encode(metadata)) : null
            );
        }
    }
}
