using Bencodex;
using GraphQL.Types;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActionEvaluationType : ObjectGraphType<ActionBase.ActionEvaluation<ActionBase>>
    {
        private static readonly Codec Codec = new Codec();

        public ActionEvaluationType()
        {
            Field<NonNullGraphType<ByteStringType>>(name: "action",
                resolve: context => Codec.Encode(context.Source.Action.PlainValue));
            Field<NonNullGraphType<AddressType>>(name: "signer",
                resolve: context => context.Source.Signer);
            Field<NonNullGraphType<LongGraphType>>(name: "blockIndex",
                resolve: context => context.Source.BlockIndex);
        }
    }
}
