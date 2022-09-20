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
        private PublicKey PublicKey;
        private long? Nonce;

        public ActionTxQuery(StandaloneContext standaloneContext, PublicKey publicKey, long? nonce) : base(standaloneContext)
        {
            PublicKey = publicKey;
            Nonce = nonce;
        }

        internal new byte[] Encode(NCAction action)
        {
            if (!(standaloneContext.BlockChain is BlockChain<PolymorphicAction<ActionBase>> blockChain))
            {
                throw new ExecutionError(
                    $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
            }

            Address signer = PublicKey.ToAddress();
            long nonce = Nonce ?? blockChain.GetNextTxNonce(signer);
            Transaction<NCAction> unsignedTransaction =
                Transaction<NCAction>.CreateUnsigned(nonce, PublicKey, blockChain.Genesis.Hash, new[] { action });

            return unsignedTransaction.Serialize(false);
        }
    }
}
