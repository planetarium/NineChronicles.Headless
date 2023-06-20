using System;
using System.Linq;
using GraphQL;
using Libplanet;
using Libplanet.Blockchain;
using Libplanet.Crypto;
using Libplanet.Tx;
using Nekoyume.Action;

namespace NineChronicles.Headless.GraphTypes
{
    public class ActionTxQuery : ActionQuery
    {
        public ActionTxQuery(StandaloneContext standaloneContext) : base(standaloneContext)
        {
        }

        internal override byte[] Encode(IResolveFieldContext context, ActionBase action)
        {
            var publicKey = new PublicKey(ByteUtil.ParseHex(context.Parent!.GetArgument<string>("publicKey")));
            if (!(StandaloneContext.BlockChain is BlockChain blockChain))
            {
                throw new ExecutionError(
                    $"{nameof(Headless.StandaloneContext)}.{nameof(Headless.StandaloneContext.BlockChain)} was not set yet!");
            }

            Address signer = publicKey.ToAddress();
            long nonce = context.Parent!.GetArgument<long?>("nonce") ?? blockChain.GetNextTxNonce(signer);
            DateTimeOffset? timestamp = context.Parent!.GetArgument<DateTimeOffset?>("timestamp");
            UnsignedTx unsignedTransaction =
                new UnsignedTx(
                    new TxInvoice(
                        genesisHash: blockChain.Genesis.Hash,
                        timestamp: timestamp,
                        actions: new TxActionList(new[] { action })),
                    new TxSigningMetadata(publicKey: publicKey, nonce: nonce));

            return unsignedTransaction.SerializeUnsignedTx().ToArray();
        }
    }
}
