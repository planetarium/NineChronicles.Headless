#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Bencodex;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Lib9c;
using Libplanet.Action.State;
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
using NineChronicles.Headless.GraphTypes.Diff;
using System.Security.Cryptography;
using System.Text;
using static NineChronicles.Headless.NCActionUtils;
using Transaction = Libplanet.Types.Tx.Transaction;

namespace NineChronicles.Headless.GraphTypes
{
    public class StandaloneQuery : ObjectGraphType
    {
        public StandaloneQuery(StandaloneContext standaloneContext, IConfiguration configuration, ActionEvaluationPublisher publisher, StateMemoryCache stateMemoryCache)
        {
            bool useSecretToken = configuration[GraphQLService.SecretTokenKey] is { };
            if (Convert.ToBoolean(configuration.GetSection("Jwt")["EnableJwtAuthentication"]))
            {
                this.AuthorizeWith(GraphQLService.JwtPolicyKey);
            }

            Field<NonNullGraphType<StateQuery>>(name: "stateQuery", arguments: new QueryArguments(
                new QueryArgument<ByteStringType>
                {
                    Name = "hash",
                    Description = "Offset block hash for query.",
                },
                new QueryArgument<LongGraphType>
                {
                    Name = "index",
                    Description = "Offset block index for query."
                }),
                resolve: context =>
                {
                    BlockHash blockHash = (context.GetArgument<byte[]?>("hash"), context.GetArgument<long?>("index")) switch
                    {
                        ({ } bytes, null) => new BlockHash(bytes),
                        (null, { } index) => standaloneContext.BlockChain[index].Hash,
                        (not null, not null) => throw new ArgumentException("Only one of 'hash' and 'index' must be given."),
                        (null, null) => standaloneContext.BlockChain.Tip.Hash,
                    };

                    if (!(standaloneContext.BlockChain is { } chain))
                    {
                        return null;
                    }

                    if (!(blockHash is { } hash))
                    {
                        return null;
                    }

                    if (!(blockHash is { } hash))
                    {
                        return null;
                    }

                    return new StateContext(
                        chain.GetWorldState(hash),
                        chain[blockHash].Index,
                        stateMemoryCache
                    );
                }
            );

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<DiffGraphType>>>>(
                name: "diffs",
                description: "This field allows you to query the diffs between two blocks." +
                             " `baseIndex` is the reference block index, and changedIndex is the block index from which to check" +
                             " what changes have occurred relative to `baseIndex`." +
                             " Both indices must not be higher than the current block on the chain nor lower than the genesis block index (0)." +
                             " The difference between the two blocks must be greater than zero for a valid comparison and less than ten for performance reasons.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<LongGraphType>>
                    {
                        Name = "baseIndex",
                        Description = "The index of the reference block from which the state is retrieved."
                    },
                    new QueryArgument<NonNullGraphType<LongGraphType>>
                    {
                        Name = "changedIndex",
                        Description = "The index of the target block for comparison."
                    }
                ),
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!"
                        );
                    }

                    var baseIndex = context.GetArgument<long>("baseIndex");
                    var changedIndex = context.GetArgument<long>("changedIndex");

                    var blockInterval = Math.Abs(changedIndex - baseIndex);
                    if (blockInterval >= 10 || blockInterval == 0)
                    {
                        throw new ExecutionError(
                            "Interval between baseIndex and changedIndex should not be greater than 10 or zero"
                        );
                    }

                    var baseBlockStateRootHash = blockChain[baseIndex].StateRootHash.ToString();
                    var changedBlockStateRootHash = blockChain[changedIndex].StateRootHash.ToString();

                    var baseStateRootHash = HashDigest<SHA256>.FromString(baseBlockStateRootHash);
                    var targetStateRootHash = HashDigest<SHA256>.FromString(
                        changedBlockStateRootHash
                    );

                    var stateStore = standaloneContext.StateStore;
                    var baseTrieModel = stateStore.GetStateRoot(baseStateRootHash);
                    var targetTrieModel = stateStore.GetStateRoot(targetStateRootHash);

                    IDiffType[] diffs = baseTrieModel
                        .Diff(targetTrieModel)
                        .Select(x =>
                        {
                            if (x.TargetValue is not null)
                            {
                                var baseSubTrieModel = stateStore.GetStateRoot(new HashDigest<SHA256>((Binary)x.SourceValue));
                                var targetSubTrieModel = stateStore.GetStateRoot(new HashDigest<SHA256>((Binary)x.TargetValue));
                                var subDiff = baseSubTrieModel
                                    .Diff(targetSubTrieModel)
                                    .Select(diff =>
                                    {
                                        return new StateDiffType.Value(
                                            Encoding.Default.GetString(diff.Path.ByteArray.ToArray()),
                                            diff.SourceValue,
                                            diff.TargetValue);
                                    }).ToArray();
                                return (IDiffType)new RootStateDiffType.Value(
                                    Encoding.Default.GetString(x.Path.ByteArray.ToArray()),
                                    subDiff
                                );
                            }
                            else
                            {
                                return new StateDiffType.Value(
                                    Encoding.Default.GetString(x.Path.ByteArray.ToArray()),
                                    x.SourceValue,
                                    x.TargetValue
                                );
                            }
                        }).ToArray();

                    return diffs;
                }
            );

            Field<ByteStringType>(
                name: "state",
                arguments: new QueryArguments(
                    new QueryArgument<ByteStringType> { Name = "hash", Description = "The hash of the block used to fetch state from chain." },
                    new QueryArgument<LongGraphType> { Name = "index", Description = "The index of the block used to fetch state from chain." },
                    new QueryArgument<NonNullGraphType<AddressType>> { Name = "accountAddress", Description = "The address of account to fetch from the chain." },
                    new QueryArgument<NonNullGraphType<AddressType>> { Name = "address", Description = "The address of state to fetch from the account." }
                ),
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    var blockHash = (context.GetArgument<byte[]?>("hash"), context.GetArgument<long?>("index")) switch
                    {
                        (not null, not null) => throw new ArgumentException(
                            "Only one of 'hash' and 'index' must be given."),
                        (null, { } index) => blockChain[index].Hash,
                        ({ } bytes, null) => new BlockHash(bytes),
                        (null, null) => blockChain.Tip.Hash,
                    };
                    var accountAddress = context.GetArgument<Address>("accountAddress");
                    var address = context.GetArgument<Address>("address");

                    var state = blockChain
                        .GetWorldState(blockHash)
                        .GetAccountState(accountAddress)
                        .GetState(address);

                    if (state is null)
                    {
                        return null;
                    }

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

                    IEnumerable<Transaction> blockTxs = digest.TxIds
                        .Select(bytes => new TxId(bytes))
                        .Select(txid =>
                        {
                            return store.GetTransaction(txid) ??
                                throw new InvalidOperationException($"Transaction {txid} not found.");
                        });

                    var filtered = blockTxs
                        .Where(tx => tx.Actions.Count == 1)
                        .Select(tx =>
                        (
                            store.GetTxExecution(blockHash, tx.Id) ??
                                throw new InvalidOperationException($"TxExecution {tx.Id} not found."),
                            ToAction(tx.Actions[0])
                        ))
                        .Where(pair => pair.Item2 is ITransferAsset)
                        .Select(pair => (pair.Item1!, (ITransferAsset)pair.Item2))
                        .Where(pair => !pair.Item1.Fail &&
                            (!recipient.HasValue || pair.Item2.Recipient == recipient) &&
                            pair.Item2.Amount.Currency.Ticker == "NCG");

                    var histories = filtered.Select(pair =>
                        new TransferNCGHistory(
                            pair.Item1.BlockHash,
                            pair.Item1.TxId,
                            pair.Item2.Sender,
                            pair.Item2.Recipient,
                            pair.Item2.Amount,
                            pair.Item2.Memo));

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
                        (Dictionary)blockChain.GetWorldState(blockHash).GetLegacyState(GoldCurrencyState.Address)
                    ).Currency;

                    return blockChain.GetWorldState(blockHash).GetBalance(
                        address,
                        currency
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

                    return standaloneContext.NineChroniclesNodeService.MinerPrivateKey.Address;
                });

            Field<MonsterCollectionStatusType>(
                name: nameof(MonsterCollectionStatus),
                arguments: new QueryArguments(
                    new QueryArgument<AddressType>
                    {
                        Name = "address",
                        Description = "agent address.",
                        DefaultValue = null
                    }
                ),
                description: "Get monster collection status by address.",
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    Address? address = context.GetArgument<Address?>("address");
                    Address agentAddress;
                    if (address is null)
                    {
                        if (standaloneContext.NineChroniclesNodeService?.MinerPrivateKey is null)
                        {
                            throw new ExecutionError(
                                $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.NineChroniclesNodeService)}.{nameof(StandaloneContext.NineChroniclesNodeService.MinerPrivateKey)} is null.");
                        }

                        agentAddress = standaloneContext.NineChroniclesNodeService!.MinerPrivateKey!.Address;
                    }
                    else
                    {
                        agentAddress = (Address)address;
                    }


                    BlockHash offset = blockChain.Tip.Hash;
                    IWorldState worldState = blockChain.GetWorldState(offset);
#pragma warning disable S3247
                    if (worldState.GetAgentState(agentAddress) is { } agentState)
#pragma warning restore S3247
                    {
                        Address deriveAddress =
                            MonsterCollectionState.DeriveAddress(agentAddress, agentState.MonsterCollectionRound);
                        Currency currency = new GoldCurrencyState(
                            (Dictionary)worldState.GetLegacyState(Addresses.GoldCurrency)).Currency;

                        FungibleAssetValue balance = worldState.GetBalance(agentAddress, currency);
                        if (worldState.GetLegacyState(deriveAddress) is Dictionary mcDict)
                        {
                            var rewardSheet = new MonsterCollectionRewardSheet();
                            var csv = worldState.GetLegacyState(
                                Addresses.GetSheetAddress<MonsterCollectionRewardSheet>()).ToDotnetString();
                            rewardSheet.Set(csv);
                            var monsterCollectionState = new MonsterCollectionState(mcDict);
                            long tipIndex = blockChain.Tip.Index;
                            List<MonsterCollectionRewardSheet.RewardInfo> rewards =
                                monsterCollectionState.CalculateRewards(rewardSheet, tipIndex);
                            return new MonsterCollectionStatus(
                                balance,
                                rewards,
                                tipIndex,
                                monsterCollectionState.IsLocked(tipIndex)
                            );
                        }
                        throw new ExecutionError(
                            $"{nameof(MonsterCollectionState)} Address: {deriveAddress} is null.");
                    }

                    throw new ExecutionError(
                        $"{nameof(AgentState)} Address: {agentAddress} is null.");
                });

            Field<NonNullGraphType<TransactionHeadlessQuery>>(
                name: "transaction",
                description: "Query for transaction.",
                resolve: context => new TransactionHeadlessQuery(standaloneContext)
            );

            Field<NonNullGraphType<BooleanGraphType>>(
                name: "activated",
                deprecationReason: "Since NCIP-15, it doesn't care account activation.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "invitationCode"
                    }
                ),
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    string invitationCode = context.GetArgument<string>("invitationCode");
                    ActivationKey activationKey = ActivationKey.Decode(invitationCode);
                    if (blockChain.GetWorldState().GetLegacyState(activationKey.PendingAddress) is Dictionary dictionary)
                    {
                        var pending = new PendingActivationState(dictionary);
                        ActivateAccount action = activationKey.CreateActivateAccount(pending.Nonce);
                        if (pending.Verify(action))
                        {
                            return false;
                        }

                        throw new ExecutionError($"invitationCode is invalid.");
                    }

                    return true;
                }
            );

            Field<NonNullGraphType<StringGraphType>>(
                name: "activationKeyNonce",
                deprecationReason: "Since NCIP-15, it doesn't care account activation.",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "invitationCode"
                    }
                ),
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is { } blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    ActivationKey activationKey;
                    try
                    {
                        string invitationCode = context.GetArgument<string>("invitationCode");
                        invitationCode = invitationCode.TrimEnd();
                        activationKey = ActivationKey.Decode(invitationCode);
                    }
                    catch (Exception)
                    {
                        throw new ExecutionError("invitationCode format is invalid.");
                    }
                    if (blockChain.GetWorldState().GetLegacyState(activationKey.PendingAddress) is Dictionary dictionary)
                    {
                        var pending = new PendingActivationState(dictionary);
                        return ByteUtil.Hex(pending.Nonce);
                    }

                    throw new ExecutionError("invitationCode is invalid.");
                }
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
