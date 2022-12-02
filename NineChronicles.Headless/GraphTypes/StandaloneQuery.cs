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
using Libplanet.Headless;
using Nekoyume.Model;
using NineChronicles.Headless.GraphTypes.States;

namespace NineChronicles.Headless.GraphTypes
{
    public class StandaloneQuery : ObjectGraphType
    {
        public StandaloneQuery(StandaloneContext standaloneContext, IConfiguration configuration, ActionEvaluationPublisher publisher)
        {
            bool useSecretToken = configuration[GraphQLService.SecretTokenKey] is { };

            Field<NonNullGraphType<StateQuery>>("stateQuery")
                .Argument<byte[]?>("hash", true, "Offset block hash for query.")
                .Resolve(context =>
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
                        blockHash switch
                        {
                            BlockHash bh => chain[bh].Index,
                            null => chain.Tip.Index,
                        }
                    );
                });

            Field<ByteStringType>("state")
                .Argument<Address>("address", false, "The address of state to fetch from the chain.")
                .Argument<byte[]?>("hash", true, "The hash of the block used to fetch state from chain.")
                .Resolve(context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain<PolymorphicAction<ActionBase>> blockChain))
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
                });

            Field<NonNullGraphType<ListGraphType<NonNullGraphType<TransferNCGHistoryType>>>>(
                "transferNCGHistories")
                .Argument<NonNullGraphType<ByteStringType>>("blockHash")
                .Argument<Address>("recipient", true)
                .Resolve(context =>
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
                        tx.CustomActions.First().InnerAction is ITransferAsset transferAsset &&
                        (!recipient.HasValue || transferAsset.Recipient == recipient) &&
                        transferAsset.Amount.Currency.Ticker == "NCG" &&
                        store.GetTxExecution(blockHash, tx.Id) is TxSuccess);

                    TransferNCGHistory ToTransferNCGHistory(TxSuccess txSuccess, string? memo)
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
                            ((ITransferAsset)tx.CustomActions!.Single().InnerAction).Memo));

                    return histories;
                });

            Field<KeyStoreType>("keyStore")
                .DeprecationReason("Use `planet key` command instead.  https://www.npmjs.com/package/@planetarium/cli")
                .Resolve(context => standaloneContext.KeyStore)
                .AuthorizeWithLocalPolicyIf(useSecretToken);

            Field<NonNullGraphType<NodeStatusType>>("nodeStatus")
                .Resolve(_ => standaloneContext);

            Field<NonNullGraphType<Libplanet.Explorer.Queries.ExplorerQuery<NCAction>>>("chainQuery")
                .DeprecationReason("Use /graphql/explorer")
                .Resolve(_ => new { });

            Field<NonNullGraphType<ValidationQuery>>("validation")
                .Description("The validation method provider for Libplanet types.")
                .Resolve(context => new ValidationQuery(standaloneContext));

            Field<NonNullGraphType<ActivationStatusQuery>>("activationStatus")
                .Description("Check if the provided address is activated.")
                .Resolve(context => new ActivationStatusQuery(standaloneContext))
                .AuthorizeWithLocalPolicyIf(useSecretToken);

            Field<NonNullGraphType<PeerChainStateQuery>>("peerChainState")
                .Description("Get the peer's block chain state")
                .Resolve(context => new PeerChainStateQuery(standaloneContext));

            Field<NonNullGraphType<StringGraphType>>("goldBalance")
                .Argument<Address>("address", false, "Target address to query")
                .Argument<byte[]?>("hash", true, "Offset block hash for query.")
                .Resolve(context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain<PolymorphicAction<ActionBase>> blockChain))
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
                });

            Field<NonNullGraphType<LongGraphType>>("nextTxNonce")
                .DeprecationReason(
                    "The root query is not the best place for nextTxNonce so it was moved. " +
                        "Use transaction.nextTxNonce()")
                .Argument<Address>("address", false, "Target address to query")
                .Resolve(context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain<PolymorphicAction<ActionBase>> blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    Address address = context.GetArgument<Address>("address");
                    return blockChain.GetNextTxNonce(address);
                });

            Field<TransactionType<NCAction>>("getTx")
                .DeprecationReason(
                    "The root query is not the best place for getTx so it was moved. " +
                        "Use transaction.getTx()")
                .Argument<TxId>("txId", false, "transaction id.")
                .Resolve(context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain<PolymorphicAction<ActionBase>> blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    var txId = context.GetArgument<TxId>("txId");
                    return blockChain.GetTransaction(txId);
                });

            Field<AddressType>("minerAddress")
                .Description("Address of current node.")
                .Resolve(context =>
                {
                    if (standaloneContext.NineChroniclesNodeService?.MinerPrivateKey is null)
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.NineChroniclesNodeService)}.{nameof(StandaloneContext.NineChroniclesNodeService.MinerPrivateKey)} is null.");
                    }

                    return standaloneContext.NineChroniclesNodeService.MinerPrivateKey.ToAddress();
                });

            Field<MonsterCollectionStatusType>(nameof(MonsterCollectionStatus))
                .Argument<Address>("address", true, "agent address", a => a.DefaultValue = null)
                .Description("Get monster collection status by address.")
                .Resolve(context =>
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
#pragma warning disable S3247
                    if (blockChain.GetState(agentAddress, offset) is Dictionary agentDict)
#pragma warning restore S3247
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

            Field<NonNullGraphType<TransactionHeadlessQuery>>("transaction")
                .Description("Query for transaction.")
                .Resolve(_ => new TransactionHeadlessQuery(standaloneContext));

            Field<NonNullGraphType<BooleanGraphType>>("activated")
                .Argument<string>("invitationCode", false)
                .Resolve(context =>
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
                });

            Field<NonNullGraphType<StringGraphType>>("activationKeyNonce")
                .Argument<string>("invitationCode", false)
                .Resolve(context =>
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
                });

            Field<NonNullGraphType<RpcInformationQuery>>("rpcInformation")
                .Description("Query for rpc mode information.")
                .Resolve(_ => new RpcInformationQuery(publisher));

            Field<NonNullGraphType<ActionQuery>>("actionQuery")
                .Resolve(_ => new ActionQuery(standaloneContext));

            Field<NonNullGraphType<ActionTxQuery>>("actionTxQuery")
                .Argument<string>(
                    "publicKey",
                    false,
                    "The hexadecimal string of public key for Transaction.")
                .Argument<long?>(
                    "nonce",
                    true,
                    "The nonce for Transaction.")
                .Argument<DateTimeOffset?>(
                    "timestamp",
                    true,
                    "The time this transaction is created.")
                .Resolve(_ => new ActionTxQuery(standaloneContext));

            Field<NonNullGraphType<AddressQuery>>("addressQuery")
                .Description("Query to get derived address.")
                .Resolve(_ => new AddressQuery(standaloneContext));
        }
    }
}
