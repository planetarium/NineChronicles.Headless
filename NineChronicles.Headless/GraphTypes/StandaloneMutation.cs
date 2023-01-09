using Bencodex.Types;
using GraphQL;
using GraphQL.Server.Authorization.AspNetCore;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action.Sys;
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
using System.Numerics;
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
                this.AuthorizeWith(GraphQLService.LocalPolicyKey);
            }

            Field<KeyStoreMutation>(
                name: "keyStore",
                deprecationReason: "Use `planet key` command instead.  https://www.npmjs.com/package/@planetarium/cli",
                resolve: context => standaloneContext.KeyStore);

            Field<ActivationStatusMutation>(
                name: "activationStatus",
                resolve: _ => new ActivationStatusMutation(nodeService));

            Field<ActionMutation>(
                name: "action",
                resolve: _ => new ActionMutation(nodeService));

            Field<NonNullGraphType<BooleanGraphType>>(
                name: "stageTx",
                description: "Add a new transaction to staging",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "payload",
                        Description = "The base64-encoded bytes for new transaction."
                    }
                ),
                resolve: context =>
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
                }
            );

            Field<NonNullGraphType<TxIdType>>(
                name: "stageTxV2",
                deprecationReason: "API update with action query. use stageTransaction mutation",
                description: "Add a new transaction to staging and return TxId",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "payload",
                        Description = "The base64-encoded bytes for new transaction."
                    }
                ),
                resolve: context =>
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
                }
            );

            Field<TxIdType>(
                name: "transfer",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Description = "A hex-encoded value for address of recipient.",
                        Name = "recipient",
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Description = "A string value of the value to be transferred.",
                        Name = "amount",
                    },
                    new QueryArgument<NonNullGraphType<LongGraphType>>
                    {
                        Description = "A sender's transaction counter. You can get it through nextTxNonce().",
                        Name = "txNonce",
                    },
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Description = "A hex-encoded value for address of currency to be transferred. The default is the NCG's address.",
                        // Convert address type to hex string for graphdocs
                        DefaultValue = GoldCurrencyState.Address.ToHex(),
                        Name = "currencyAddress"
                    },
                    new QueryArgument<StringGraphType>
                    {
                        Description = "A 80-max length string to note.",
                        Name = "memo",
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
                }
            );

            Field<TxIdType>(
                deprecationReason: "Incorrect remittance may occur when using transferGold() to the same address consecutively. Use transfer() instead.",
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
                }
            );

            Field<TxIdType>(
                name: "setValidator",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Description = "A public key of validator to be set.",
                        Name = "publicKey",
                    },
                    new QueryArgument<NonNullGraphType<BigIntGraphType>>
                    {
                        Description = "The target power of the validator to be set.",
                        Name = "power",
                    },
                    new QueryArgument<NonNullGraphType<LongGraphType>>
                    {
                        Description = "The transaction counter. You can get it through nextTxNonce().",
                        Name = "txNonce",
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
                    PublicKey publicKey = new PublicKey(ByteUtil.ParseHex(context.GetArgument<string>("publicKey")));
                    BigInteger power = context.GetArgument<BigInteger>("power");
                    Transaction<NCAction> tx = Transaction<NCAction>.Create(
                        context.GetArgument<long>("txNonce"),
                        privateKey,
                        blockChain.Genesis.Hash,
                        new SetValidator(publicKey, power)
                    );
                    blockChain.StageTransaction(tx);
                    return tx.Id;
                }
            );

            Field<NonNullGraphType<TxIdType>>(
                name: "stageTransaction",
                description: "Add a new transaction to staging and return TxId",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "payload",
                        Description = "The hexadecimal string of the transaction to stage."
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
                }
            );
        }
    }
}
