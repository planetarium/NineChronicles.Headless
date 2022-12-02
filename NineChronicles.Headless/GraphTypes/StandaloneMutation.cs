using Bencodex.Types;
using GraphQL;
using GraphQL.Server.Authorization.AspNetCore;
using GraphQL.Types;
using Libplanet;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Tx;
using Microsoft.Extensions.Configuration;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Serilog;
using System;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.GraphTypes
{
    public class StandaloneMutation : ObjectGraphType
    {
        public StandaloneMutation(
            StandaloneContext standaloneContext,
            NineChroniclesNodeService nodeService,
            IConfiguration configuration
        )
        {
            if (configuration[GraphQLService.SecretTokenKey] is { })
            {
                this.AuthorizeWithPolicy(GraphQLService.LocalPolicyKey);
            }

            Field<KeyStoreMutation>("keyStore")
                .DeprecationReason(
                    "Use `planet key` command instead.  https://www.npmjs.com/package/@planetarium/cli")
                .Resolve(context => standaloneContext.KeyStore);

            Field<ActivationStatusMutation>("activationStatus")
                .Resolve(_ => new ActivationStatusMutation(nodeService));

            Field<ActionMutation>("action")
                .Resolve(_ => new ActionMutation(nodeService));

            Field<NonNullGraphType<BooleanGraphType>>("stageTx")
                .DeprecationReason("API update with action query. use stageTransaction mutation")
                .Description("Add a new transaction to staging")
                .Argument<string>(
                    "payload",
                    false,
                    "The base64-encoded bytes for new transaction.")
                .Resolve(context =>
                {
                    try
                    {
                        byte[] bytes = Convert.FromBase64String(context.GetArgument<string>("payload"));
                        Transaction<NCAction> tx = Transaction<NCAction>.Deserialize(bytes);
                        NineChroniclesNodeService? service = standaloneContext.NineChroniclesNodeService;
                        BlockChain<NCAction>? blockChain = service?.Swarm.BlockChain;

                        if (blockChain is null)
                        {
                            throw new InvalidOperationException($"{nameof(blockChain)} is null.");
                        }

                        if (blockChain.Policy.ValidateNextBlockTx(blockChain, tx) is null)
                        {
                            blockChain.StageTransaction(tx);

                            if (service?.Swarm is { } swarm && swarm.Running)
                            {
                                swarm.BroadcastTxs(new[] { tx });
                            }
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
                });

            Field<NonNullGraphType<TxIdType>>("stageTxV2")
                .DeprecationReason("API update with action query. use stageTransaction mutation")
                .Description("Add a new transaction to staging and return TxId")
                .Argument<string>(
                    "payload",
                    false,
                    "The base64-encoded bytes for new transaction.")
                .Resolve(context =>
                {
                    try
                    {
                        byte[] bytes = Convert.FromBase64String(context.GetArgument<string>("payload"));
                        Transaction<NCAction> tx = Transaction<NCAction>.Deserialize(bytes);
                        NineChroniclesNodeService? service = standaloneContext.NineChroniclesNodeService;
                        BlockChain<NCAction>? blockChain = service?.Swarm.BlockChain;

                        if (blockChain is null)
                        {
                            throw new InvalidOperationException($"{nameof(blockChain)} is null.");
                        }

                        Exception? validationExc = blockChain.Policy.ValidateNextBlockTx(blockChain, tx);
                        if (validationExc is null)
                        {
                            blockChain.StageTransaction(tx);

                            if (service?.Swarm is { } swarm && swarm.Running)
                            {
                                swarm.BroadcastTxs(new[] { tx });
                            }

                            return tx.Id;
                        }

                        throw new ExecutionError(
                            $"The given transaction is invalid. (due to: {validationExc.Message})",
                            validationExc
                        );
                    }
                    catch (Exception e)
                    {
                        throw new ExecutionError("An unexpected exception occurred.", e);
                    }
                });

            Field<TxIdType>("transfer")
                .Argument<Address>(
                    "recipient",
                    false,
                    "A hex-encoded value for address of recipient.")
                .Argument<string>(
                    "amount",
                    false,
                    "A string value of the value to be transferred.")
                .Argument<long>(
                    "txNonce",
                    false,
                    "A sender's transaction counter. You can get it through nextTxNonce().")
                .Argument<string>(
                    "currencyAddress",
                    false,
                    "A hex-encoded value for address of currency to be transferred. The default is the NCG's address.",
                    // Convert address type to hex string for graphdocs
                    arg => arg.DefaultValue = GoldCurrencyState.Address.ToHex())
                .Argument<string?>(
                    "memo",
                    true,
                    "A 80-max length string to note.")
                .Resolve(context =>
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
                        (Dictionary)blockChain.GetState(new Address(context.GetArgument<string>("currencyAddress")))
                    ).Currency;
                    FungibleAssetValue amount =
                        FungibleAssetValue.Parse(currency, context.GetArgument<string>("amount"));

                    Address recipient = context.GetArgument<Address>("recipient");
                    string? memo = context.GetArgument<string?>("memo");
                    Transaction<NCAction> tx = Transaction<NCAction>.Create(
                        context.GetArgument<long>("txNonce"),
                        privateKey,
                        blockChain.Genesis.Hash,
                        new NCAction[]
                        {
                            new TransferAsset(
                                privateKey.ToAddress(),
                                recipient,
                                amount,
                                memo
                            ),
                        }
                    );
                    blockChain.StageTransaction(tx);
                    return tx.Id;
                });

            Field<TxIdType>("transferGold")
                .DeprecationReason(
                    "Incorrect remittance may occur when using transferGold() to the same address consecutively. Use transfer() instead.")
                .Argument<Address>("recipient", false)
                .Argument<string>("amount", false)
                .Resolve(context =>
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
                        (Dictionary)blockChain.GetState(GoldCurrencyState.Address)
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
                });

            Field<NonNullGraphType<TxIdType>>("stageTransaction")
                .Description("Add a new transaction to staging and return TxId")
                .Argument<string>(
                    "payload",
                    false,
                    "The hexadecimal string of the transaction to stage.")
                .Resolve(context =>
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

                        Exception? validationExc = blockChain.Policy.ValidateNextBlockTx(blockChain, tx);
                        if (validationExc is null)
                        {
                            blockChain.StageTransaction(tx);

                            if (service?.Swarm is { } swarm && swarm.Running)
                            {
                                swarm.BroadcastTxs(new[] { tx });
                            }

                            return tx.Id;
                        }

                        throw new ExecutionError(
                            $"The given transaction is invalid. (due to: {validationExc.Message})",
                            validationExc
                        );
                    }
                    catch (Exception e)
                    {
                        throw new ExecutionError("An unexpected exception occurred.", e);
                    }
                });
        }
    }
}
