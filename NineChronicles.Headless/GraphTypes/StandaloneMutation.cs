using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Crypto;
using Libplanet.Store;
using Libplanet.Tx;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Serilog;
using System;
using GraphQL.Server.Authorization.AspNetCore;
using Libplanet.Explorer.GraphTypes;
using Microsoft.Extensions.Configuration;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.GraphTypes
{
    public class StandaloneMutation : ObjectGraphType
    {
        public StandaloneMutation(StandaloneContext standaloneContext, IConfiguration configuration)
        {
            if (configuration[GraphQLService.SecretTokenKey] is { })
            {
                this.AuthorizeWith(GraphQLService.LocalPolicyKey);   
            }

            Field<AuthenticationMutation>(
                "auth",
                resolve: context => new { });

            Field<ActivationStatusMutation>(
                    name: "activationStatus",
                    resolve: context => standaloneContext.NineChroniclesNodeService)
                .AuthorizeWith(GraphQLService.UserPolicyKey);

            Field<ActionMutation>(
                    name: "action",
                    resolve: context => standaloneContext.NineChroniclesNodeService)
                .AuthorizeWith(GraphQLService.UserPolicyKey);

            Field<NonNullGraphType<BooleanGraphType>>(
                name: "stageTx",
                description: "Add a new transaction to staging",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "payload",
                        Description = "Hex-encoded bytes for new transaction."
                    }
                ),
                resolve: context =>
                {
                    try
                    {
                        byte[] bytes = ByteUtil.ParseHex(context.GetArgument<string>("payload"));
                        Transaction<NCAction> tx = Transaction<NCAction>.Deserialize(bytes);
                        NineChroniclesNodeService? service = standaloneContext.NineChroniclesNodeService;
                        BlockChain<NCAction>? blockChain = service?.Swarm.BlockChain;

                        if (blockChain is null)
                        {
                            throw new InvalidOperationException($"{nameof(blockChain)} is null.");
                        }

                        if (blockChain.Policy.DoesTransactionFollowsPolicy(tx, blockChain))
                        {
                            blockChain.StageTransaction(tx);
                            return true;
                        }
                        else
                        {
                            context.Errors.Add(new ExecutionError("The given transaction is invalid."));
                            return false;
                        }
                    }
                    catch (Exception e)
                    {
                        context.Errors.Add(new ExecutionError("An unexpected exception occurred.", e));
                        return false;
                    }
                }
            ).AuthorizeWith(GraphQLService.UserPolicyKey);

            Field<TxIdType>(
                name: "transferGold",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "recipient",
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "amount"
                    }
                ),
                resolve: context =>
                {
                    if (!(standaloneContext.NineChroniclesNodeService is { } service))
                    {
                        throw new InvalidOperationException($"{nameof(NineChroniclesNodeService)} is null.");
                    }
    
                    PrivateKey? privateKey = service.MinerPrivateKey;
                    if (privateKey is null)
                    {
                        // FIXME We should cover this case on unittest.
                        var msg = "No private key was loaded.";
                        context.Errors.Add(new ExecutionError(msg));
                        Log.Error(msg);
                        return null;
                    }

                    BlockChain<NCAction> blockChain = service.BlockChain;
                    var currency = new GoldCurrencyState(
                        (Dictionary) blockChain.GetState(GoldCurrencyState.Address)
                    ).Currency;
                    FungibleAssetValue amount =
                        FungibleAssetValue.Parse(currency, context.GetArgument<string>("amount"));

                    Address recipient = context.GetArgument<Address>("recipient");

                    Transaction<NCAction> tx = blockChain.MakeTransaction(
                        privateKey,
                        new NCAction[]
                        {
                            new TransferAsset(
                                privateKey.ToAddress(),
                                recipient,
                                amount
                            ),
                        }
                    );
                    return tx.Id;
                }
            ).AuthorizeWith(GraphQLService.UserPolicyKey);
        }
    }
}
