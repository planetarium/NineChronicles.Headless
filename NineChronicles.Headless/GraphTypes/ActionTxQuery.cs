using GraphQL;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blockchain;
using Libplanet.Crypto;
using Libplanet.Tx;
using Nekoyume.Action;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActionTxQuery : ActionQuery
    {
        public ActionTxQuery(StandaloneContext standaloneContext) : base(standaloneContext)
        {
        }

        internal override byte[] Encode(IResolveFieldContext context, NCAction action)
        {
            var publicKey = new PublicKey(ByteUtil.ParseHex(context.Parent!.GetArgument<string>("publicKey")));
            if (!(standaloneContext.BlockChain is BlockChain<PolymorphicAction<ActionBase>> blockChain))
            {
                throw new ExecutionError(
                    $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
            }

            Address signer = publicKey.ToAddress();
            long nonce = context.Parent!.GetArgument<long?>("nonce") ?? blockChain.GetNextTxNonce(signer);
            Transaction<NCAction> unsignedTransaction =
                Transaction<NCAction>.CreateUnsigned(nonce, publicKey, blockChain.Genesis.Hash, new[] { action });

            return unsignedTransaction.Serialize(false);
        }
    }
}
