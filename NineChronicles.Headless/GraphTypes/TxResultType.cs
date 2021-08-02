using System.ComponentModel;
using GraphQL.Types;
using Libplanet.Action;
using Libplanet.Blocks;
using Libplanet.Tx;

namespace NineChronicles.Headless.GraphTypes
{
    public class TxResultType : ObjectGraphType<TxResult>
    {
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
        }
    }
}
