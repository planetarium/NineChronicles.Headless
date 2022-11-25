using System;
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
        public ActionTxQuery(StandaloneContext standaloneContext, BlockChain<NCAction> blockChain) : base(standaloneContext, blockChain)
        {
        }

        internal override byte[] Encode(IResolveFieldContext context, NCAction action)
        {
            var publicKey = new PublicKey(ByteUtil.ParseHex(context.Parent!.GetArgument<string>("publicKey")));
            Address signer = publicKey.ToAddress();
            long nonce = context.Parent!.GetArgument<long?>("nonce") ?? _blockChain.GetNextTxNonce(signer);
            DateTimeOffset? timestamp = context.Parent!.GetArgument<DateTimeOffset?>("timestamp");
            Transaction<NCAction> unsignedTransaction =
                Transaction<NCAction>.CreateUnsigned(
                    nonce: nonce,
                    publicKey: publicKey,
                    genesisHash: _blockChain.Genesis.Hash,
                    customActions: new[] { action },
                    timestamp: timestamp
                );

            return unsignedTransaction.Serialize(false);
        }
    }
}
