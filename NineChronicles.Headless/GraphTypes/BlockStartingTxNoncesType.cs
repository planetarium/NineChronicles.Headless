using GraphQL.Types;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;

namespace NineChronicles.Headless.GraphTypes
{
    public class BlockStartingTxNoncesType : ObjectGraphType<(Address Signer, long Nonce)>
    {
        public BlockStartingTxNoncesType()
        {
            Field<NonNullGraphType<AddressType>>(
                name: "signer",
                description: "The address of the transaction signer.",
                resolve: context => context.Source.Signer
            );

            Field<NonNullGraphType<LongGraphType>>(
                name: "nonce",
                description: "The starting nonce before the block executes.",
                resolve: context => context.Source.Nonce
            );
        }
    }
}
