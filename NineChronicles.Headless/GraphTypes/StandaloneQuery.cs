#nullable enable
using System;
using System.Collections.Generic;
using System.Diagnostics;
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
using Libplanet.KeyStore;
using NineChronicles.Headless.Repositories.BlockChain;
using NineChronicles.Headless.Repositories.StateTrie;
using NineChronicles.Headless.Repositories.Transaction;
using NineChronicles.Headless.Repositories.WorldState;
using static NineChronicles.Headless.NCActionUtils;
using Block = NineChronicles.Headless.Domain.Model.BlockChain.Block;

namespace NineChronicles.Headless.GraphTypes
{
    public class StandaloneQuery : ObjectGraphType
    {
        private static readonly ActivitySource ActivitySource = new ActivitySource("NineChronicles.Headless.GraphTypes.StandaloneQuery");

        public StandaloneQuery(StandaloneContext standaloneContext, IKeyStore keyStore, IConfiguration configuration, StateMemoryCache stateMemoryCache, IWorldStateRepository worldStateRepository, IBlockChainRepository blockChainRepository, ITransactionRepository transactionRepository, IStateTrieRepository stateTrieRepository)
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
                    using var activity = ActivitySource.StartActivity("stateQuery");
                    Block block = (context.GetArgument<byte[]?>("hash"), context.GetArgument<long?>("index")) switch
                    {
                        ({ } bytes, null) => blockChainRepository.GetBlock(new BlockHash(bytes)),
                        (null, { } index) => blockChainRepository.GetBlock(index),
                        (not null, not null) => throw new ArgumentException("Only one of 'hash' and 'index' must be given."),
                        (null, null) => blockChainRepository.GetTip(),
                    };
                    activity?.AddTag("BlockHash", block.Hash.ToString());

                    return new StateContext(
                        worldStateRepository.GetWorldState(block.StateRootHash),
                        block.Index,
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
                    using var activity = ActivitySource.StartActivity("diffs");
                    var baseIndex = context.GetArgument<long>("baseIndex");
                    var changedIndex = context.GetArgument<long>("changedIndex");

                    var blockInterval = Math.Abs(changedIndex - baseIndex);
                    if (blockInterval >= 10 || blockInterval == 0)
                    {
                        throw new ExecutionError(
                            "Interval between baseIndex and changedIndex should not be greater than 10 or zero"
                        );
                    }

                    var baseBlockStateRootHash = blockChainRepository.GetBlock(baseIndex).StateRootHash.ToString();
                    var changedBlockStateRootHash = blockChainRepository.GetBlock(changedIndex).StateRootHash.ToString();

                    var baseStateRootHash = HashDigest<SHA256>.FromString(baseBlockStateRootHash);
                    var targetStateRootHash = HashDigest<SHA256>.FromString(
                        changedBlockStateRootHash
                    );

                    return stateTrieRepository.CompareStateTrie(baseStateRootHash, targetStateRootHash);
                }
            );

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<StateDiffType>>>>(
                name: "accountDiffs",
                description: "This field allows you to query the diffs based accountAddress between two blocks." +
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
                    },
                    new QueryArgument<NonNullGraphType<AddressType>>
                    {
                        Name = "accountAddress",
                        Description = "The target accountAddress."
                    }
                ),
                resolve: context =>
                {
                    using var activity = ActivitySource.StartActivity("accountDiffs");
                    var baseIndex = context.GetArgument<long>("baseIndex");
                    var changedIndex = context.GetArgument<long>("changedIndex");
                    var accountAddress = context.GetArgument<Address>("accountAddress");

                    var blockInterval = Math.Abs(changedIndex - baseIndex);
                    if (blockInterval >= 30 || blockInterval == 0)
                    {
                        throw new ExecutionError(
                            "Interval between baseIndex and changedIndex should not be greater than 30 or zero"
                        );
                    }

                    var baseBlockStateRootHash = blockChainRepository.GetBlock(baseIndex).StateRootHash.ToString();
                    var changedBlockStateRootHash = blockChainRepository.GetBlock(changedIndex).StateRootHash.ToString();

                    var baseStateRootHash = HashDigest<SHA256>.FromString(baseBlockStateRootHash);
                    var targetStateRootHash = HashDigest<SHA256>.FromString(
                        changedBlockStateRootHash
                    );

                    return stateTrieRepository.CompareStateAccountTrie(baseStateRootHash, targetStateRootHash, accountAddress);
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
                    using var activity = ActivitySource.StartActivity("state");
                    var block = (context.GetArgument<byte[]?>("hash"), context.GetArgument<long?>("index")) switch
                    {
                        (not null, not null) => throw new ArgumentException(
                            "Only one of 'hash' and 'index' must be given."),
                        (null, { } index) => blockChainRepository.GetBlock(index),
                        ({ } bytes, null) => blockChainRepository.GetBlock(new BlockHash(bytes)),
                        (null, null) => blockChainRepository.GetTip(),
                    };
                    var accountAddress = context.GetArgument<Address>("accountAddress");
                    var address = context.GetArgument<Address>("address");

                    activity?
                        .AddTag("BlockHash", block.Hash.ToString())
                        .AddTag("Address", address.ToString());
                    var state = worldStateRepository
                        .GetWorldState(block.StateRootHash)
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
                    using var activity = ActivitySource.StartActivity("transferNCGHistories");
                    BlockHash blockHash = new BlockHash(context.GetArgument<byte[]>("blockHash"));

                    activity?.AddTag("BlockHash", blockHash.ToString());

                    var block = blockChainRepository.GetBlock(blockHash);

                    var recipient = context.GetArgument<Address?>("recipient");

                    var filtered = block.Transactions
                        .Where(tx => tx.Actions.Count == 1)
                        .Select(tx =>
                        (
                            transactionRepository.GetTxExecution(blockHash, tx.Id) ??
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
                resolve: context => keyStore
            ).AuthorizeWithLocalPolicyIf(useSecretToken);

            Field<NonNullGraphType<NodeStatusType>>(
                name: "nodeStatus",
                resolve: _ =>
                {
                    using var activity = ActivitySource.StartActivity("nodeStatus");
                    return standaloneContext.NodeStatus;
                });

            Field<NonNullGraphType<Libplanet.Explorer.Queries.ExplorerQuery>>(
                name: "chainQuery",
                deprecationReason: "Use /graphql/explorer",
                resolve: _ => new object()
            );

            Field<NonNullGraphType<ValidationQuery>>(
                name: "validation",
                description: "The validation method provider for Libplanet types.",
                resolve: _ => new object()
            );

            Field<NonNullGraphType<ActivationStatusQuery>>(
                    name: "activationStatus",
                    description: "Check if the provided address is activated.",
                    deprecationReason: "Since NCIP-15, it doesn't care account activation.",
                    resolve: _ => new object())
                .AuthorizeWithLocalPolicyIf(useSecretToken);

            Field<NonNullGraphType<PeerChainStateQuery>>(
                name: "peerChainState",
                description: "Get the peer's block chain state",
                resolve: _ => new object());

            Field<NonNullGraphType<StringGraphType>>(
                name: "goldBalance",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>> { Name = "address", Description = "Target address to query" },
                    new QueryArgument<ByteStringType> { Name = "hash", Description = "Offset block hash for query." }
                ),
                resolve: context =>
                {
                    using var activity = ActivitySource.StartActivity("goldBalance");
                    Address address = context.GetArgument<Address>("address");
                    byte[] blockHashByteArray = context.GetArgument<byte[]>("hash");
                    var block = blockHashByteArray is null
                        ? blockChainRepository.GetTip()
                        : blockChainRepository.GetBlock(new BlockHash(blockHashByteArray));
                    var worldState = worldStateRepository.GetWorldState(block.StateRootHash);
                    Currency currency = new GoldCurrencyState(
                        (Dictionary)worldState
                            .GetLegacyState(GoldCurrencyState.Address)
                    ).Currency;

                    activity?
                        .AddTag("BlockHash", block.Hash.ToString())
                        .AddTag("Address", address.ToString());
                    return worldState.GetBalance(
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
                    using var activity = ActivitySource.StartActivity("nextTxNonce");

                    Address address = context.GetArgument<Address>("address");
                    activity?.AddTag("Address", address.ToString());
                    return transactionRepository.GetNextTxNonce(address);
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
                    var txId = context.GetArgument<TxId>("txId");
                    return transactionRepository.GetTransaction(txId);
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
                    using var activity = ActivitySource.StartActivity(nameof(MonsterCollectionStatus));
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

                    HashDigest<SHA256> offset = blockChainRepository.GetTip().StateRootHash;
                    activity?
                        .AddTag("BlockHash", offset.ToString())
                        .AddTag("Address", address.ToString());
                    IWorldState worldState = worldStateRepository.GetWorldState(offset);
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
                resolve: context =>
                {
                    using var activity = ActivitySource.StartActivity("transaction");
                    return new object();
                });

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

                    var worldState = worldStateRepository.GetWorldState(blockChainRepository.GetTip().StateRootHash);
                    string invitationCode = context.GetArgument<string>("invitationCode");
                    ActivationKey activationKey = ActivationKey.Decode(invitationCode);
                    if (worldState.GetLegacyState(activationKey.PendingAddress) is Dictionary dictionary)
                    {
                        var pending = new PendingActivationState(dictionary);
                        var signature = activationKey.PrivateKey.Sign(pending.Nonce);
                        if (pending.Verify(signature))
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

                    var worldState = worldStateRepository.GetWorldState(blockChainRepository.GetTip().StateRootHash);
                    if (worldState.GetLegacyState(activationKey.PendingAddress) is Dictionary dictionary)
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
                resolve: _ => new object()
            );

            Field<NonNullGraphType<ActionQuery>>(
                name: "actionQuery",
                description: "Query to create action transaction.",
                resolve: context =>
                {
                    using var activity = ActivitySource.StartActivity("actionQuery");
                    return new object();
                });

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
                resolve: context =>
                {
                    using var activity = ActivitySource.StartActivity("actionTxQuery");
                    return new object();
                });

            Field<NonNullGraphType<AddressQuery>>(
                name: "addressQuery",
                description: "Query to get derived address.",
                resolve: context =>
                {
                    using var activity = ActivitySource.StartActivity("addressQuery");
                    return new object();
                });
        }
    }
}
