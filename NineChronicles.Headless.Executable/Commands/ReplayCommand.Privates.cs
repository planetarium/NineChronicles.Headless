using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using Bencodex;
using Bencodex.Types;
using Cocona;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Action;
using Libplanet.Action.State;
using Libplanet.Types.Assets;
using Libplanet.Types.Consensus;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;
using RocksDbSharp;
using Serilog;
using Libplanet.Store.Trie;

namespace NineChronicles.Headless.Executable.Commands
{
    public partial class ReplayCommand : CoconaLiteConsoleAppBase
    {
        /// <summary>
        /// Almost duplicate https://github.com/planetarium/libplanet/blob/main/Libplanet/Action/ActionContext.cs.
        /// </summary>
        private sealed class ActionContext : IActionContext
        {
            public ActionContext(
                Address signer,
                TxId? txid,
                Address miner,
                long blockIndex,
                int blockProtocolVersion,
                IWorld previousState,
                int randomSeed,
                bool rehearsal = false)
            {
                Signer = signer;
                TxId = txid;
                Miner = miner;
                BlockIndex = blockIndex;
                BlockProtocolVersion = blockProtocolVersion;
                Rehearsal = rehearsal;
                PreviousState = previousState;
                RandomSeed = randomSeed;
            }

            public Address Signer { get; }

            public TxId? TxId { get; }

            public Address Miner { get; }

            public long BlockIndex { get; }

            public int BlockProtocolVersion { get; }

            public bool Rehearsal { get; }

            public IWorld PreviousState { get; }

            public int RandomSeed { get; }

            public bool BlockAction => TxId is null;

            public void UseGas(long gas)
            {
            }

            public long GasUsed() => 0;

            public long GasLimit() => 0;

            public IRandom GetRandom() => new Random(RandomSeed);
        }

        private sealed class Random : System.Random, IRandom
        {
            public Random(int seed)
                : base(seed)
            {
                Seed = seed;
            }

            public int Seed { get; private set; }
        }

        private sealed class LocalCacheBlockChainStates : IBlockChainStates
        {
            private readonly IBlockChainStates _source;
            private readonly RocksDb _rocksDb;

            public LocalCacheBlockChainStates(IBlockChainStates source, string cacheDirectory)
            {
                _source = source;
                var options = new DbOptions().SetCreateIfMissing();
                _rocksDb = RocksDb.Open(options, cacheDirectory);
            }
            public IWorldState GetWorldState(BlockHash? offset)
                => new LocalCacheWorldState(
                    _source.GetWorldState(offset),
                    _source.GetAccountState,
                    _rocksDb);

            public IWorldState GetWorldState(HashDigest<SHA256>? hash)
                => new LocalCacheWorldState(
                    _source.GetWorldState(hash),
                    _source.GetAccountState,
                    _rocksDb);

            public IAccountState GetAccountState(HashDigest<SHA256>? hash)
                => new LocalCacheAccountState(
                    _source.GetAccountState(hash),
                    _rocksDb);

            public IAccountState GetAccountState(Address address, BlockHash? offset)
                => new LocalCacheAccountState(
                    _source.GetAccountState(address, offset),
                    _rocksDb);

            public IValue? GetState(Address address, Address accountAddress, BlockHash? offset)
                => GetAccountState(accountAddress, offset).GetState(address);

            public IValue? GetState(Address address, HashDigest<SHA256>? stateRootHash)
                => GetAccountState(stateRootHash).GetState(address);

            public FungibleAssetValue GetBalance(Address address, Currency currency, HashDigest<SHA256>? stateRootHash)
                => GetAccountState(stateRootHash).GetBalance(address, currency);

            public FungibleAssetValue GetBalance(Address address, Currency currency, Address accountAddress, BlockHash? offset)
                => GetAccountState(accountAddress, offset).GetBalance(address, currency);

            public FungibleAssetValue GetTotalSupply(Currency currency, HashDigest<SHA256>? stateRootHash)
                => GetAccountState(stateRootHash).GetTotalSupply(currency);

            public FungibleAssetValue GetTotalSupply(Currency currency, Address accountAddress, BlockHash? offset)
                => GetAccountState(accountAddress, offset).GetTotalSupply(currency);

            public ValidatorSet GetValidatorSet(HashDigest<SHA256>? stateRootHash)
                => GetAccountState(stateRootHash).GetValidatorSet();

            public ValidatorSet GetValidatorSet(Address accountAddress, BlockHash? offset)
                => GetAccountState(accountAddress, offset).GetValidatorSet();
        }

        private sealed class LocalCacheWorldState : IWorldState
        {
            private static readonly Codec Codec = new Codec();
            private readonly IWorldState _worldState;
            private readonly Func<HashDigest<SHA256>?, IAccountState> _accountStateGetter;
            private readonly RocksDb _rocksDb;

            public LocalCacheWorldState(
                IWorldState worldState,
                Func<HashDigest<SHA256>?, IAccountState> accountStateGetter,
                RocksDb rocksDb)
            {
                _worldState = worldState;
                _accountStateGetter = accountStateGetter;
                _rocksDb = rocksDb;
            }

            public ITrie Trie => _worldState.Trie;

            public bool Legacy => _worldState.Legacy;

            public IAccount GetAccount(Address address)
            {
                var key = WithStateRootHash(address.ToByteArray());
                try
                {
                    return GetAccount(key);
                }
                catch (KeyNotFoundException)
                {
                    var account = _worldState.GetAccount(address);
                    SetAccount(key, account);
                    return account;
                }
            }

            public IAccount GetAccount(byte[] key)
            {
                if (_rocksDb.Get(key) is not { } bytes)
                {
                    throw new KeyNotFoundException();
                }

                return new Account(_accountStateGetter(
                    new HashDigest<SHA256>(((Binary)Codec.Decode(bytes)).ToImmutableArray())));
            }

            private void SetAccount(byte[] key, IAccount? account)
            {
                _rocksDb.Put(key, account is null ? new byte[] { 0x78 } : account.Trie.Hash.ToByteArray());
            }

            private byte[] WithStateRootHash(params byte[][] suffixes)
            {
                if (Trie.Hash is { } stateRootHash)
                {
                    var stream = new MemoryStream(HashDigest<SHA256>.Size + suffixes.Sum(s => s.Length));
                    stream.Write(stateRootHash.ToByteArray());
                    foreach (var suffix in suffixes)
                    {
                        stream.Write(suffix);
                    }

                    return stream.ToArray();
                }
                throw new InvalidOperationException();
            }
        }

        private sealed class LocalCacheAccountState : IAccountState
        {
            private static readonly Codec _codec = new Codec();
            private readonly IAccountState _accountState;
            private readonly RocksDb _rocksDb;

            public LocalCacheAccountState(
                IAccountState accountState,
                RocksDb rocksDb)
            {
                _accountState = accountState;
                _rocksDb = rocksDb;
            }

            public ITrie Trie => _accountState.Trie;

            public IValue? GetState(Address address)
            {
                var key = WithStateRootHash(address.ToByteArray());
                try
                {
                    return GetValue(key);
                }
                catch (KeyNotFoundException)
                {
                    var state = _accountState.GetState(address);
                    SetValue(key, state);
                    return state;
                }
            }

            public IReadOnlyList<IValue?> GetStates(IReadOnlyList<Address> addresses)
            {
                return addresses.Select(GetState).ToList();
            }

            public FungibleAssetValue GetBalance(Address address, Currency currency)
            {
                var key = WithStateRootHash(address.ToByteArray(), currency.Hash.ToByteArray());
                try
                {
                    var state = GetValue(key);
                    if (state is not Integer integer)
                    {
                        throw new InvalidOperationException();
                    }

                    return FungibleAssetValue.FromRawValue(currency, integer);
                }
                catch (KeyNotFoundException)
                {
                    var fav = _accountState.GetBalance(address, currency);
                    SetValue(key, (Integer)fav.RawValue);
                    return fav;
                }
            }

            public FungibleAssetValue GetTotalSupply(Currency currency)
            {
                var key = WithStateRootHash(currency.Hash.ToByteArray());
                try
                {
                    var state = GetValue(key);
                    if (state is not Integer integer)
                    {
                        throw new InvalidOperationException();
                    }

                    return FungibleAssetValue.FromRawValue(currency, integer);
                }
                catch (KeyNotFoundException)
                {
                    var fav = _accountState.GetTotalSupply(currency);
                    SetValue(key, (Integer)fav.RawValue);
                    return fav;
                }
            }

            public ValidatorSet GetValidatorSet()
            {
                var key = WithStateRootHash(new byte[] { 0x5f, 0x5f, 0x5f });
                try
                {
                    var state = GetValue(key);
                    return state is not null ? new ValidatorSet(state) : new ValidatorSet();
                }
                catch (KeyNotFoundException)
                {
                    var validatorSet = _accountState.GetValidatorSet();
                    SetValue(key, validatorSet.Bencoded);
                    return validatorSet;
                }
            }

            private IValue? GetValue(byte[] key)
            {
                if (_rocksDb.Get(key) is not { } bytes)
                {
                    throw new KeyNotFoundException();
                }

                return bytes[0] == 'x' ? null : _codec.Decode(bytes);
            }

            private void SetValue(byte[] key, IValue? value)
            {
                _rocksDb.Put(key, value is null ? new byte[] { 0x78 } : _codec.Encode(value));
            }

            private byte[] WithStateRootHash(params byte[][] suffixes)
            {
                if (Trie.Hash is { } stateRootHash)
                {
                    var stream = new MemoryStream(HashDigest<SHA256>.Size + suffixes.Sum(s => s.Length));
                    stream.Write(stateRootHash.ToByteArray());
                    foreach (var suffix in suffixes)
                    {
                        stream.Write(suffix);
                    }

                    return stream.ToArray();
                }
                throw new InvalidOperationException();
            }
        }

        /// <summary>
        /// Almost duplicate https://github.com/planetarium/libplanet/blob/main/Libplanet/Action/ActionEvaluator.cs#L286.
        /// </summary>
        private static IEnumerable<ActionEvaluation> EvaluateActions(
            HashDigest<SHA256> preEvaluationHash,
            long blockIndex,
            int blockProtocolVersion,
            TxId? txid,
            IWorld previousStates,
            Address miner,
            Address signer,
            byte[] signature,
            IImmutableList<IAction> actions,
            ILogger? logger = null)
        {
            ActionContext CreateActionContext(
                IWorld prevState,
                int randomSeed)
            {
                return new ActionContext(
                    signer: signer,
                    txid: txid,
                    miner: miner,
                    blockIndex: blockIndex,
                    blockProtocolVersion: blockProtocolVersion,
                    previousState: prevState,
                    randomSeed: randomSeed);
            }

            byte[] preEvaluationHashBytes = preEvaluationHash.ToByteArray();
            int seed = ActionEvaluator.GenerateRandomSeed(preEvaluationHashBytes, signature, 0);

            IWorld states = previousStates;
            foreach (IAction action in actions)
            {
                Exception? exc = null;
                IWorld nextStates = states;
                ActionContext context = CreateActionContext(nextStates, seed);

                try
                {
                    Stopwatch stopwatch = new Stopwatch();
                    stopwatch.Start();
                    nextStates = action.Execute(context);
                    logger?
                        .Information(
                            "Action {Action} took {DurationMs} ms to execute",
                            action,
                            stopwatch.ElapsedMilliseconds);
                }
                catch (OutOfMemoryException e)
                {
                    // Because OutOfMemory is thrown non-deterministically depending on the state
                    // of the node, we should throw without further handling.
                    var message =
                        "Action {Action} of tx {TxId} of block #{BlockIndex} with " +
                        "pre-evaluation hash {PreEvaluationHash} threw an exception " +
                        "during execution";
                    logger?.Error(
                        e,
                        message,
                        action,
                        txid,
                        blockIndex,
                        ByteUtil.Hex(preEvaluationHash.ByteArray));
                    throw;
                }
                catch (Exception e)
                {
                    var message =
                        "Action {Action} of tx {TxId} of block #{BlockIndex} with " +
                        "pre-evaluation hash {PreEvaluationHash} threw an exception " +
                        "during execution";
                    logger?.Error(
                        e,
                        message,
                        action,
                        txid,
                        blockIndex,
                        ByteUtil.Hex(preEvaluationHash.ByteArray));
                    var innerMessage =
                        $"The action {action} (block #{blockIndex}, " +
                        $"pre-evaluation hash {ByteUtil.Hex(preEvaluationHash.ByteArray)}, " +
                        $"tx {txid} threw an exception during execution.  " +
                        "See also this exception's InnerException property";
                    logger?.Error(
                        "{Message}\nInnerException: {ExcMessage}", innerMessage, e.Message);
                    exc = new UnexpectedlyTerminatedActionException(
                        innerMessage,
                        preEvaluationHash,
                        blockIndex,
                        txid,
                        null,
                        action,
                        e);
                }

                // As IActionContext.Random is stateful, we cannot reuse
                // the context which is once consumed by Execute().
                ActionContext equivalentContext = CreateActionContext(states, seed);

                yield return new ActionEvaluation(
                    action: action,
                    inputContext: equivalentContext,
                    outputState: nextStates,
                    exception: exc);

                if (exc is { })
                {
                    yield break;
                }

                states = nextStates;
                unchecked
                {
                    seed++;
                }
            }
        }
    }
}
