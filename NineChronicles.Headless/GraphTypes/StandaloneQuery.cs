#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Bencodex;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Lib9c;
using Libplanet.Blockchain;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Explorer.GraphTypes;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;
using Microsoft.Extensions.Configuration;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using Nekoyume.Model;
using Nekoyume.Module;
using NineChronicles.Headless.GraphTypes.States;
using static NineChronicles.Headless.NCActionUtils;
using Transaction = Libplanet.Types.Tx.Transaction;

namespace NineChronicles.Headless.GraphTypes
{
    public class StandaloneQuery : ObjectGraphType
    {
        public StandaloneQuery(StandaloneContext standaloneContext, IConfiguration configuration, ActionEvaluationPublisher publisher)
        {
            bool useSecretToken = configuration[GraphQLService.SecretTokenKey] is { };

            Field<NonNullGraphType<StateQuery>>(name: "stateQuery", arguments: new QueryArguments(
                new QueryArgument<ByteStringType>
                {
                    Name = "hash",
                    Description = "Offset block hash for query.",
                }),
                resolve: context =>
                {
                    BlockHash? blockHash = context.GetArgument<byte[]>("hash") switch
                    {
                        byte[] bytes => new BlockHash(bytes),
                        null => standaloneContext.BlockChain?.Tip?.Hash,
                    };

                    if (!(standaloneContext.BlockChain is { } chain))
                    {
                        return null;
                    }

                    return new StateContext(
                        chain.GetWorldState(blockHash),
                        blockHash switch
                        {
                            BlockHash bh => chain[bh].Index,
                            null => chain.Tip!.Index,
                        }
                    );
                }
            );

            Field<ByteStringType>(
                name: "state",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>> { Name = "address", Description = "The address of state to fetch from the chain." },
                    new QueryArgument<NonNullGraphType<AddressType>> { Name = "accountAddress", Description = "The address of account which the state is included." },
                    new QueryArgument<ByteStringType> { Name = "hash", Description = "The hash of the block used to fetch state from chain." }
                ),
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    var address = context.GetArgument<Address>("address");
                    var accountAddress = context.GetArgument<Address>("accountAddress");
                    var blockHashByteArray = context.GetArgument<byte[]>("hash");
                    var blockHash = blockHashByteArray is null
                        ? blockChain.Tip.Hash
                        : new BlockHash(blockHashByteArray);

                    var state = blockChain.GetStates(new[] { address }, accountAddress, blockHash)[0];

                    return new Codec().Encode(state);
                }
            );

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<TransferNCGHistoryType>>>>(
                "transferNCGHistories",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<ByteStringType>>
                    {
                        Name = "blockHash"
                    },
                    new QueryArgument<AddressType>
                    {
                        Name = "recipient"
                    }
                ), resolve: context =>
                {
                    BlockHash blockHash = new BlockHash(context.GetArgument<byte[]>("blockHash"));

                    if (!(standaloneContext.Store is { } store))
                    {
                        throw new InvalidOperationException();
                    }

                    if (!(store.GetBlockDigest(blockHash) is { } digest))
                    {
                        throw new ArgumentException("blockHash");
                    }

                    var recipient = context.GetArgument<Address?>("recipient");

                    IEnumerable<Transaction> txs = digest.TxIds
                        .Select(b => new TxId(b.ToBuilder().ToArray()))
                        .Select(store.GetTransaction);
                    var filteredTransactions = txs.Where(tx =>
                        tx.Actions!.Count == 1 &&
                        ToAction(tx.Actions.First()) is ITransferAsset transferAsset &&
                        (!recipient.HasValue || transferAsset.Recipient == recipient) &&
                        transferAsset.Amount.Currency.Ticker == "NCG" &&
                        store.GetTxExecution(blockHash, tx.Id) is TxSuccess);

                    TransferNCGHistory ToTransferNCGHistory(TxSuccess txSuccess, string? memo)
                    {
                        var rawTransferNcgHistories = txSuccess.UpdatedFungibleAssets
                            .Where(pair => pair.Value.Values.Any(fav => fav.Currency.Ticker == "NCG"))
                            .Select(pair =>
                                (pair.Key, pair.Value.Values.First(fav => fav.Currency.Ticker == "NCG")))
                            .ToArray();
                        var ((senderAddress, _), (recipientAddress, amount)) =
                            rawTransferNcgHistories[0].Item2.RawValue > rawTransferNcgHistories[1].Item2.RawValue
                                ? (rawTransferNcgHistories[1], rawTransferNcgHistories[0])
                                : (rawTransferNcgHistories[0], rawTransferNcgHistories[1]);
                        return new TransferNCGHistory(
                            txSuccess.BlockHash,
                            txSuccess.TxId,
                            senderAddress,
                            recipientAddress,
                            amount,
                            memo);
                    }

                    var histories = filteredTransactions.Select(tx =>
                        ToTransferNCGHistory((TxSuccess)store.GetTxExecution(blockHash, tx.Id),
                            ((ITransferAsset)ToAction(tx.Actions!.Single())).Memo));

                    return histories;
                });

            Field<KeyStoreType>(
                name: "keyStore",
                deprecationReason: "Use `planet key` command instead.  https://www.npmjs.com/package/@planetarium/cli",
                resolve: context => standaloneContext.KeyStore
            ).AuthorizeWithLocalPolicyIf(useSecretToken);

            Field<NonNullGraphType<NodeStatusType>>(
                name: "nodeStatus",
                resolve: _ => new NodeStatusType(standaloneContext)
            );

            Field<NonNullGraphType<Libplanet.Explorer.Queries.ExplorerQuery>>(
                name: "chainQuery",
                deprecationReason: "Use /graphql/explorer",
                resolve: context => new { }
            );

            Field<NonNullGraphType<ValidationQuery>>(
                name: "validation",
                description: "The validation method provider for Libplanet types.",
                resolve: context => new ValidationQuery(standaloneContext));

            Field<NonNullGraphType<ActivationStatusQuery>>(
                    name: "activationStatus",
                    description: "Check if the provided address is activated.",
                    deprecationReason: "Since NCIP-15, it doesn't care account activation.",
                    resolve: context => new ActivationStatusQuery(standaloneContext))
                .AuthorizeWithLocalPolicyIf(useSecretToken);

            Field<NonNullGraphType<PeerChainStateQuery>>(
                name: "peerChainState",
                description: "Get the peer's block chain state",
                resolve: context => new PeerChainStateQuery(standaloneContext));

            Field<NonNullGraphType<StringGraphType>>(
                name: "goldBalance",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>> { Name = "address", Description = "Target address to query" },
                    new QueryArgument<ByteStringType> { Name = "hash", Description = "Offset block hash for query." }
                ),
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    Address address = context.GetArgument<Address>("address");
                    byte[] blockHashByteArray = context.GetArgument<byte[]>("hash");
                    var blockHash = blockHashByteArray is null
                        ? blockChain.Tip.Hash
                        : new BlockHash(blockHashByteArray);
                    Currency currency = new GoldCurrencyState(
                        (Dictionary)LegacyModule.GetState(
                            blockChain.GetWorldState(),
                            GoldCurrencyState.Address)
                    ).Currency;

                    return blockChain.GetBalance(
                        address,
                        currency,
                        blockHash
                    ).GetQuantityString();
                }
            );

            Field<NonNullGraphType<LongGraphType>>(
                name: "nextTxNonce",
                deprecationReason: "The root query is not the best place for nextTxNonce so it was moved. " +
                                   "Use transaction.nextTxNonce()",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>> { Name = "address", Description = "Target address to query" }
                ),
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    Address address = context.GetArgument<Address>("address");
                    return blockChain.GetNextTxNonce(address);
                }
            );

            Field<TransactionType>(
                name: "getTx",
                deprecationReason: "The root query is not the best place for getTx so it was moved. " +
                                   "Use transaction.getTx()",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<TxIdType>>
                    { Name = "txId", Description = "transaction id." }
                ),
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    var txId = context.GetArgument<TxId>("txId");
                    return blockChain.GetTransaction(txId);
                }
            );

            Field<AddressType>(
                name: "minerAddress",
                description: "Address of current node.",
                resolve: context =>
                {
                    if (standaloneContext.NineChroniclesNodeService?.MinerPrivateKey is null)
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.NineChroniclesNodeService)}.{nameof(StandaloneContext.NineChroniclesNodeService.MinerPrivateKey)} is null.");
                    }

                    return standaloneContext.NineChroniclesNodeService.MinerPrivateKey.ToAddress();
                });

            Field<NonNullGraphType<TransactionHeadlessQuery>>(
                name: "transaction",
                description: "Query for transaction.",
                resolve: context => new TransactionHeadlessQuery(standaloneContext)
            );

            Field<NonNullGraphType<RpcInformationQuery>>(
                name: "rpcInformation",
                description: "Query for rpc mode information.",
                resolve: context => new RpcInformationQuery(publisher)
            );

            Field<NonNullGraphType<ActionQuery>>(
                name: "actionQuery",
                description: "Query to create action transaction.",
                resolve: context => new ActionQuery(standaloneContext));

            Field<NonNullGraphType<ActionTxQuery>>(
                name: "actionTxQuery",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "publicKey",
                        Description = "The hexadecimal string of public key for Transaction.",
                    },
                    new QueryArgument<LongGraphType>
                    {
                        Name = "nonce",
                        Description = "The nonce for Transaction.",
                    },
                    new QueryArgument<DateTimeOffsetGraphType>
                    {
                        Name = "timestamp",
                        Description = "The time this transaction is created.",
                    },
                    new QueryArgument<FungibleAssetValueInputType>
                    {
                        Name = "maxGasPrice",
                        DefaultValue = 1 * Currencies.Mead
                    }
                ),
                resolve: context => new ActionTxQuery(standaloneContext));

            Field<NonNullGraphType<AddressQuery>>(
                name: "addressQuery",
                description: "Query to get derived address.",
                resolve: context => new AddressQuery(standaloneContext));
        }
    }
}
