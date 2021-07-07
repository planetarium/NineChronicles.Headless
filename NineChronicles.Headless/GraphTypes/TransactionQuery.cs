using GraphQL;
using GraphQL.Types;
using NineChronicles.Headless;
using NineChronicles.Headless.GraphTypes;
using Libplanet.Blockchain;
using Libplanet.Action;
using Libplanet.Tx;
using Libplanet;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;
using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace NineChronicles.Headless.GraphTypes
{
    class TransactionHeadlessQuery : ObjectGraphType
    {
        public TransactionHeadlessQuery(StandaloneContext standaloneContext)
        {
            Field<NonNullGraphType<LongGraphType>>(
                name: "nextTxNonce",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>> { Name = "address", Description = "Target address to query" }
                ),
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain<PolymorphicAction<ActionBase>> blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    Address address = context.GetArgument<Address>("address");
                    return blockChain.GetNextTxNonce(address);
                }
            );

            Field<TransactionType<NCAction>>(
                name: "getTx",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<TxIdType>>
                    { Name = "txId", Description = "transaction id." }
                ),
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain<PolymorphicAction<ActionBase>> blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    var txId = context.GetArgument<TxId>("txId");
                    return blockChain.GetTransaction(txId);
                }
            );

            Field<NonNullGraphType<StringGraphType>>(
                name: "createUnsignedTx",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    { Name = "plainValue", Description = "The plain value for Transaction." }
                ),
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain<PolymorphicAction<ActionBase>> blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    string plainValue = context.GetArgument<string>("plainValue");
                    var value = new Bencodex.Codec().Decode(System.Convert.FromBase64String(plainValue));
#pragma warning disable 612
                    var action = new NCAction();
#pragma warning restore 612
                    action.LoadPlainValue(value);

// nonce, signer, public key, genesis hash, timestamp
                    return action.ToString();
                });
        }
    }
}
