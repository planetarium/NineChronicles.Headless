#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using Bencodex;
using Bencodex.Types;
using GraphQL;
using GraphQL.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blockchain;
using Libplanet.Blocks;
using Libplanet.Explorer.GraphTypes;
using Microsoft.Extensions.Configuration;
using Libplanet.Tx;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Nekoyume.TableData;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;
using Libplanet.Blockchain.Renderers;
using Libplanet.Headless;
using Nekoyume.Model;
using NineChronicles.Headless.GraphTypes.States;
using Serilog;
using Nekoyume.Model.Arena;
using System.Text;
using Nekoyume.Extensions;
using Nekoyume.Model.BattleStatus.Arena;

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
                        null => standaloneContext.BlockChain?.GetDelayedRenderer()?.Tip?.Hash,
                    };

                    if (!(standaloneContext.BlockChain is { } chain))
                    {
                        return null;
                    }

                    return new StateContext(
                        chain.ToAccountStateGetter(blockHash),
                        chain.ToAccountBalanceGetter(blockHash),
                        blockHash != null ? chain[blockHash.Value].Index : chain.Tip.Index
                    );
                }
            );

            Field<ByteStringType>(
                name: "state",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<AddressType>> { Name = "address", Description = "The address of state to fetch from the chain." },
                    new QueryArgument<ByteStringType> { Name = "hash", Description = "The hash of the block used to fetch state from chain." }
                ),
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain<NCAction> blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    var address = context.GetArgument<Address>("address");
                    var blockHashByteArray = context.GetArgument<byte[]>("hash");
                    var blockHash = blockHashByteArray is null
                        ? blockChain.Tip.Hash
                        : new BlockHash(blockHashByteArray);

                    var state = blockChain.GetState(address, blockHash);

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

                    IEnumerable<Transaction<NCAction>> txs = digest.TxIds
                        .Select(b => new TxId(b.ToBuilder().ToArray()))
                        .Select(store.GetTransaction<NCAction>);
                    var filteredTransactions = txs.Where(tx =>
                        tx.CustomActions!.Count == 1 &&
                        tx.CustomActions.First().InnerAction is TransferAsset transferAsset &&
                        (!recipient.HasValue || transferAsset.Recipient == recipient) &&
                        transferAsset.Amount.Currency.Ticker == "NCG" &&
                        store.GetTxExecution(blockHash, tx.Id) is TxSuccess);

                    TransferNCGHistory ToTransferNCGHistory(TxSuccess txSuccess, string memo)
                    {
                        var rawTransferNcgHistories = txSuccess.FungibleAssetsDelta.Select(pair =>
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
                            ((TransferAsset)tx.CustomActions!.Single().InnerAction).Memo));

                    return histories;
                });

            Field<KeyStoreType>(
                name: "keyStore",
                resolve: context => standaloneContext.KeyStore
            ).AuthorizeWithLocalPolicyIf(useSecretToken);

            Field<NonNullGraphType<NodeStatusType>>(
                name: "nodeStatus",
                resolve: _ => new NodeStatusType(standaloneContext)
            );

            Field<NonNullGraphType<Libplanet.Explorer.Queries.ExplorerQuery<NCAction>>>(
                name: "chainQuery",
                resolve: context => new { }
            );

            Field<NonNullGraphType<ValidationQuery>>(
                name: "validation",
                description: "The validation method provider for Libplanet types.",
                resolve: context => new ValidationQuery(standaloneContext));

            Field<NonNullGraphType<ActivationStatusQuery>>(
                    name: "activationStatus",
                    description: "Check if the provided address is activated.",
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
                    if (!(standaloneContext.BlockChain is BlockChain<NCAction> blockChain))
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
                        (Dictionary)blockChain.GetState(GoldCurrencyState.Address)
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
                    if (!(standaloneContext.BlockChain is BlockChain<NCAction> blockChain))
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
                deprecationReason: "The root query is not the best place for getTx so it was moved. " +
                                   "Use transaction.getTx()",
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<TxIdType>>
                    { Name = "txId", Description = "transaction id." }
                ),
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain<NCAction> blockChain))
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
                    if (!(standaloneContext.BlockChain is BlockChain<NCAction> blockChain))
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

                        agentAddress = standaloneContext.NineChroniclesNodeService!.MinerPrivateKey!.ToAddress();
                    }
                    else
                    {
                        agentAddress = (Address)address;
                    }


                    BlockHash? offset = blockChain.GetDelayedRenderer()?.Tip?.Hash;
                    if (blockChain.GetState(agentAddress, offset) is Dictionary agentDict)
                    {
                        AgentState agentState = new AgentState(agentDict);
                        Address deriveAddress = MonsterCollectionState.DeriveAddress(agentAddress, agentState.MonsterCollectionRound);
                        Currency currency = new GoldCurrencyState(
                            (Dictionary)blockChain.GetState(Addresses.GoldCurrency, offset)
                            ).Currency;

                        FungibleAssetValue balance = blockChain.GetBalance(agentAddress, currency, offset);
                        if (blockChain.GetState(deriveAddress, offset) is Dictionary mcDict)
                        {
                            var rewardSheet = new MonsterCollectionRewardSheet();
                            var csv = blockChain.GetState(
                                Addresses.GetSheetAddress<MonsterCollectionRewardSheet>(),
                                offset
                            ).ToDotnetString();
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
                arguments: new QueryArguments(
                    new QueryArgument<NonNullGraphType<StringGraphType>>
                    {
                        Name = "invitationCode"
                    }
                ),
                resolve: context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain<NCAction> blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    string invitationCode = context.GetArgument<string>("invitationCode");
                    ActivationKey activationKey = ActivationKey.Decode(invitationCode);
                    if (blockChain.GetState(activationKey.PendingAddress) is Dictionary dictionary)
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
                    if (blockChain.GetState(activationKey.PendingAddress) is Dictionary dictionary)
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
                resolve: context => new ActionQuery(standaloneContext));

            Field<ListGraphType<Abstractions.ArenaEventBaseType>>(
                "arenaBattleData",
                description: "All of the information to playback an arena battle.",
                arguments: new QueryArguments(new QueryArgument<NonNullGraphType<TxIdType>>
                {
                    Name = "transactionId",
                    Description = "Transaction of the battle."
                }),
                resolve: context =>
                {
                    var transactionId = context.GetArgument<TxId>("transactionId");

                    if (!(standaloneContext.Store is { } store))
                    {
                        throw new InvalidOperationException("Store is not ready");
                    }
                    var transaction = store.GetTransaction<NCAction>(transactionId);
                    var action = transaction.CustomActions?.FirstOrDefault();
                    if (action == null)
                    {
                        throw new InvalidOperationException("Action is null.");
                    }
                    if (action.InnerAction.GetType() != typeof(BattleArena))
                    {
                        throw new InvalidOperationException("Wrong Transaction Type, please choose a BattleArena action");
                    }
                    var innerAction = action.InnerAction as BattleArena;
                    if (innerAction == null)
                    {
                        throw new InvalidOperationException("Inner action is null");
                    }
                    var blockHash = store.GetFirstTxIdBlockHashIndex(transactionId);

                    if (blockHash == null)
                    {
                        throw new InvalidOperationException("Block Hash is null");
                    }
                    var digest = store.GetBlockDigest(blockHash.Value);
                    if (digest == null)
                    {
                        throw new InvalidOperationException("Block Digest is null.");
                    }
                    if (!(standaloneContext.BlockChain is { } chain))
                    {
                        return null;
                    }
                    var header = digest.Value.GetHeader();
                    if (header == null)
                    {
                        throw new InvalidOperationException("Block Header is null.");
                    }
                    var preEvaluationHash = header.PreEvaluationHash;
                    if (transaction.Signature == null)
                    {
                        throw new InvalidOperationException("Transaction Signature is null.");
                    }
                    byte[] hashedSignature;
                    using (var hasher = System.Security.Cryptography.SHA1.Create())
                    {
                        hashedSignature = hasher.ComputeHash(transaction.Signature);
                    }
                    byte[] preEvaluationHashBytes = preEvaluationHash.ToBuilder().ToArray();
                    int seed =
                    (preEvaluationHashBytes.Length > 0
                        ? BitConverter.ToInt32(preEvaluationHashBytes, 0) : 0)
                    ^ BitConverter.ToInt32(hashedSignature, 0);

                    var random = new LocalRandom(seed);
                    var simulator = new Nekoyume.Arena.ArenaSimulator(random);

                    var previousHash = header.PreviousHash;
                    if (!(previousHash is BlockHash))
                    {
                        throw new InvalidOperationException("Previous BlockHash missing.");
                    }
                    var states = new StateContext(
                        chain.ToAccountStateGetter(previousHash),
                        chain.ToAccountBalanceGetter(previousHash),
                        chain[previousHash.Value].Index
                    );

                    var sheets = states.GetSheets(
                        containArenaSimulatorSheets: true,
                        sheetTypes: new[]
                        {
                            typeof(ArenaSheet),
                            typeof(ItemRequirementSheet),
                            typeof(EquipmentItemRecipeSheet),
                            typeof(EquipmentItemSubRecipeSheetV2),
                            typeof(EquipmentItemOptionSheet),
                            typeof(MaterialItemSheet),
                        });
                    var myAvatarAddress = innerAction.myAvatarAddress;
                    if (!states.TryGetAvatarStateV2(transaction.Signer, myAvatarAddress,
                    out var avatarState, out var _))
                    {
                        throw new FailedLoadStateException(
                            $"Aborted as the avatar state of the signer was failed to load.");
                    }
                    var myArenaAvatarStateAdr = ArenaAvatarState.DeriveAddress(myAvatarAddress);
                    var enemyAvatarAddress = innerAction.enemyAvatarAddress;

                    if (!states.TryGetArenaAvatarState(myArenaAvatarStateAdr, out var myArenaAvatarState))
                    {
                        throw new ArenaAvatarStateNotFoundException(
                            $"[{nameof(BattleArena)}] my avatar address : {myAvatarAddress}");
                    }

                    var enemyArenaAvatarStateAdr = ArenaAvatarState.DeriveAddress(enemyAvatarAddress);
                    if (!states.TryGetArenaAvatarState(enemyArenaAvatarStateAdr,
                            out var enemyArenaAvatarState))
                    {
                        throw new ArenaAvatarStateNotFoundException(
                            $"[{nameof(BattleArena)}] enemy avatar address : {enemyAvatarAddress}");
                    }

                    // update arena avatar state
                    myArenaAvatarState.UpdateEquipment(innerAction.equipments);
                    myArenaAvatarState.UpdateCostumes(innerAction.costumes);

                    // simulate
                    var enemyAvatarState = states.GetEnemyAvatarState(enemyAvatarAddress);
                    ArenaPlayerDigest ExtraMyArenaPlayerDigest = new ArenaPlayerDigest(avatarState, myArenaAvatarState);
                    ArenaPlayerDigest ExtraEnemyArenaPlayerDigest = new ArenaPlayerDigest(enemyAvatarState, enemyArenaAvatarState);
                    var arenaSheets = sheets.GetArenaSimulatorSheets();
                    var log = simulator.Simulate(ExtraMyArenaPlayerDigest, ExtraEnemyArenaPlayerDigest, arenaSheets);
                    return log.Events;

                }
            );

        }
    }
}
