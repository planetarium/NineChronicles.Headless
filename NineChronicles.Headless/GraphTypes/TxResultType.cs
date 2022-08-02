using System.ComponentModel;
using Bencodex;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blocks;
using Libplanet.Tx;

namespace NineChronicles.Headless.GraphTypes
{
    public class TxResultType : ObjectGraphType<TxResult>
    {
        private static readonly Codec Codec = new Codec();

        public TxResultType()
        {
            Field<NonNullGraphType<TxStatusType>>(
                nameof(TxResult.TxStatus),
                description: "The transaction status.",
                resolve: context => context.Source.TxStatus
            );

            Field<LongGraphType>(
                nameof(TxResult.BlockIndex),
                description: "The block index which the target transaction executed.",
                resolve: context => context.Source.BlockIndex
            );

            Field<StringGraphType>(
                nameof(TxResult.BlockHash),
                description: "The block hash which the target transaction executed.",
                resolve: context => context.Source.BlockHash
            );

            Field<StringGraphType>(
                nameof(TxResult.ExceptionName),
                description: "The name of the exception when the transaction failed. "
                    + "There will be a value when only the transaction fails.",
                resolve: context => context.Source.ExceptionName
            );

            Field<StringGraphType>(
                nameof(TxResult.ExceptionMetadata),
                description: "A hexadecimal string to present the metadata of the exception when the transaction failed. "
                    + "It is a Bencodex value. There will be a value when only the transaction fails.",
                resolve: context => context.Source.ExceptionMetadata is { } metadata ? ByteUtil.Hex(Codec.Encode(metadata)) : null
            );
        }
    }
}
