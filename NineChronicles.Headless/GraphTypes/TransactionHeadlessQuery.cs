using System;
using System.Collections.Generic;
using GraphQL;
using GraphQL.Types;
using Libplanet.Blockchain;
using Libplanet.Action;
using Libplanet.Tx;
using Libplanet;
using Libplanet.Explorer.GraphTypes;
using Nekoyume.Action;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;
using Libplanet.Blocks;
using Libplanet.Crypto;
using Libplanet.Store;

namespace NineChronicles.Headless.GraphTypes
{
    class TransactionHeadlessQuery : ObjectGraphType
    {
        public TransactionHeadlessQuery(StandaloneContext standaloneContext)
        {
            Field<NonNullGraphType<LongGraphType>>("nextTxNonce")
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
                .Argument<TxId>("txId", false, "Target transaction ID to query")
                .Resolve(context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain<PolymorphicAction<ActionBase>> blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    var txId = context.GetArgument<TxId>("txId");
                    try
                    {
                        return blockChain.GetTransaction(txId);
                    }
                    catch (KeyNotFoundException)
                    {
                        return null;
                    }
                });

            Field<NonNullGraphType<StringGraphType>>("createUnsignedTx")
                .DeprecationReason("API update with action query. use unsignedTransaction")
                .Argument<string>("publicKey", false, "The base64-encoded public key for Transaction.")
                .Argument<string>("plainValue", false, "The base64-encoded plain value of action for Transaction.")
                .Argument<long>("nonce", true, "The nonce for Transaction.")
                .Resolve(context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain<PolymorphicAction<ActionBase>> blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    string plainValueString = context.GetArgument<string>("plainValue");
                    var plainValue = new Bencodex.Codec().Decode(System.Convert.FromBase64String(plainValueString));
#pragma warning disable 612
                    var action = new NCAction();
#pragma warning restore 612
                    action.LoadPlainValue(plainValue);

                    var publicKey = new PublicKey(Convert.FromBase64String(context.GetArgument<string>("publicKey")));
                    Address signer = publicKey.ToAddress();
                    long nonce = context.GetArgument<long?>("nonce") ?? blockChain.GetNextTxNonce(signer);
                    Transaction<NCAction> unsignedTransaction =
                        Transaction<NCAction>.CreateUnsigned(nonce, publicKey, blockChain.Genesis.Hash, new[] { action });
                    return Convert.ToBase64String(unsignedTransaction.Serialize(false));
                });

            Field<NonNullGraphType<StringGraphType>>("attachSignature")
                .DeprecationReason("Use signTransaction")
                .Argument<string>(
                    "unsignedTransaction",
                    false,
                    "The base64-encoded unsigned transaction to attach the given signature.")
                .Argument<string>(
                    "signature",
                    false,
                    "The base64-encoded signature of the given unsigned transaction.")
                .Resolve(context =>
                {
                    byte[] signature = Convert.FromBase64String(context.GetArgument<string>("signature"));
                    Transaction<NCAction> unsignedTransaction =
                        Transaction<NCAction>.Deserialize(
                            Convert.FromBase64String(context.GetArgument<string>("unsignedTransaction")),
                            false);
                    TxMetadata txMetadata = new TxMetadata(unsignedTransaction.PublicKey)
                    {
                        Nonce = unsignedTransaction.Nonce,
                        GenesisHash = unsignedTransaction.GenesisHash,
                        UpdatedAddresses = unsignedTransaction.UpdatedAddresses,
                        Timestamp = unsignedTransaction.Timestamp
                    };

                    Transaction<NCAction> signedTransaction = new Transaction<NCAction>(
                        txMetadata,
                        unsignedTransaction.CustomActions!,
                        signature);

                    return Convert.ToBase64String(signedTransaction.Serialize(true));
                });

            Field<NonNullGraphType<TxResultType>>("transactionResult")
                .Argument<TxId>("txId", false, "transaction id.")
                .Resolve(context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain<PolymorphicAction<ActionBase>> blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    if (!(standaloneContext.Store is IStore store))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.Store)} was not set yet!");
                    }

                    TxId txId = context.GetArgument<TxId>("txId");
                    if (!(store.GetFirstTxIdBlockHashIndex(txId) is { } txExecutedBlockHash))
                    {
                        return blockChain.GetStagedTransactionIds().Contains(txId)
                            ? new TxResult(TxStatus.STAGING, null, null, null, null, null, null, null)
                            : new TxResult(TxStatus.INVALID, null, null, null, null, null, null, null);
                    }

                    try
                    {
                        TxExecution execution = blockChain.GetTxExecution(txExecutedBlockHash, txId);
                        Block<PolymorphicAction<ActionBase>> txExecutedBlock = blockChain[txExecutedBlockHash];
                        return execution switch
                        {
                            TxSuccess txSuccess => new TxResult(TxStatus.SUCCESS, txExecutedBlock.Index,
                                txExecutedBlock.Hash.ToString(), null, null, txSuccess.UpdatedStates, txSuccess.FungibleAssetsDelta, txSuccess.UpdatedFungibleAssets),
                            TxFailure txFailure => new TxResult(TxStatus.FAILURE, txExecutedBlock.Index,
                                txExecutedBlock.Hash.ToString(), txFailure.ExceptionName, txFailure.ExceptionMetadata, null, null, null),
                            _ => throw new NotImplementedException(
                                $"{nameof(execution)} is not expected concrete class.")
                        };
                    }
                    catch (Exception)
                    {
                        return new TxResult(TxStatus.INVALID, null, null, null, null, null, null, null);
                    }
                });

            Field<NonNullGraphType<ByteStringType>>("unsignedTransaction")
                .Argument<string>(
                    "publicKey",
                    false,
                    "The hexadecimal string of public key for Transaction.")
                .Argument<string>(
                    "plainValue",
                    false,
                    "The hexadecimal string of plain value for Action.")
                .Argument<long?>(
                    "nonce",
                    true,
                    "The nonce for Transaction.")
                .Resolve(context =>
                {
                    if (!(standaloneContext.BlockChain is BlockChain<PolymorphicAction<ActionBase>> blockChain))
                    {
                        throw new ExecutionError(
                            $"{nameof(StandaloneContext)}.{nameof(StandaloneContext.BlockChain)} was not set yet!");
                    }

                    string plainValueString = context.GetArgument<string>("plainValue");
                    var plainValue = new Bencodex.Codec().Decode(ByteUtil.ParseHex(plainValueString));
#pragma warning disable 612
                    var action = new NCAction();
#pragma warning restore 612
                    action.LoadPlainValue(plainValue);

                    var publicKey = new PublicKey(ByteUtil.ParseHex(context.GetArgument<string>("publicKey")));
                    Address signer = publicKey.ToAddress();
                    long nonce = context.GetArgument<long?>("nonce") ?? blockChain.GetNextTxNonce(signer);
                    Transaction<NCAction> unsignedTransaction =
                        Transaction<NCAction>.CreateUnsigned(nonce, publicKey, blockChain.Genesis.Hash, new[] { action });
                    return unsignedTransaction.Serialize(false);
                });

            Field<NonNullGraphType<ByteStringType>>("signTransaction")
                .Argument<string>(
                    "unsignedTransaction",
                    false,
                    "The hexadecimal string of unsigned transaction to attach the given signature.")
                .Argument<string>(
                    "signature",
                    false,
                    "The hexadecimal string of signature of the given unsigned transaction.")
                .Resolve(context =>
                {
                    byte[] signature = ByteUtil.ParseHex(context.GetArgument<string>("signature"));
                    Transaction<NCAction> unsignedTransaction =
                        Transaction<NCAction>.Deserialize(
                            ByteUtil.ParseHex(context.GetArgument<string>("unsignedTransaction")),
                            false);
                    TxMetadata txMetadata = new TxMetadata(unsignedTransaction.PublicKey)
                    {
                        Nonce = unsignedTransaction.Nonce,
                        GenesisHash = unsignedTransaction.GenesisHash,
                        UpdatedAddresses = unsignedTransaction.UpdatedAddresses,
                        Timestamp = unsignedTransaction.Timestamp
                    };

                    Transaction<NCAction> signedTransaction = new Transaction<NCAction>(
                        txMetadata,
                        unsignedTransaction.CustomActions!,
                        signature);
                    signedTransaction.Validate();

                    return signedTransaction.Serialize(true);
                });
        }
    }
}
