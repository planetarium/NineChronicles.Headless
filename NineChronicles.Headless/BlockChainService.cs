#nullable disable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using Humanizer.Bytes;
using Libplanet.Action.State;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Headless.Hosting;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;
using MagicOnion;
using MagicOnion.Server;
using Microsoft.Extensions.Caching.Memory;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Delegation;
using Nekoyume.Model.Guild;
using Nekoyume.Model.State;
using Nekoyume.Module;
using Nekoyume.Module.Guild;
using Nekoyume.Shared.Services;
using NineChronicles.Headless.Repositories.BlockChain;
using NineChronicles.Headless.Repositories.Swarm;
using NineChronicles.Headless.Repositories.Transaction;
using NineChronicles.Headless.Repositories.WorldState;
using NineChronicles.Headless.Utils;
using Serilog;
using static NineChronicles.Headless.NCActionUtils;
using NodeExceptionType = Libplanet.Headless.NodeExceptionType;
using Transaction = Libplanet.Types.Tx.Transaction;

namespace NineChronicles.Headless
{
    public class BlockChainService : ServiceBase<IBlockChainService>, IBlockChainService
    {
        private IBlockChainRepository _blockChainRepository;
        private ITransactionRepository _transactionRepository;
        private IWorldStateRepository _worldStateRepository;
        private ISwarmRepository _swarmRepository;
        private RpcContext _context;
        private Codec _codec;
        private LibplanetNodeServiceProperties _libplanetNodeServiceProperties;
        private ActionEvaluationPublisher _publisher;
        private MemoryCache _memoryCache;

        public BlockChainService(
            IBlockChainRepository blockChainRepository,
            ITransactionRepository transactionRepository,
            IWorldStateRepository worldStateRepository,
            ISwarmRepository swarmRepository,
            RpcContext context,
            LibplanetNodeServiceProperties libplanetNodeServiceProperties,
            ActionEvaluationPublisher actionEvaluationPublisher,
            StateMemoryCache cache
            )
        {
            _blockChainRepository = blockChainRepository;
            _transactionRepository = transactionRepository;
            _worldStateRepository = worldStateRepository;
            _swarmRepository = swarmRepository;
            _context = context;
            _codec = new Codec();
            _libplanetNodeServiceProperties = libplanetNodeServiceProperties;
            _publisher = actionEvaluationPublisher;
            _memoryCache = cache.SheetCache;
        }

        public UnaryResult<bool> PutTransaction(byte[] txBytes)
        {
            try
            {
                Transaction tx =
                    Transaction.Deserialize(txBytes);

                var actionName = ToAction(tx.Actions[0]) is { } action
                    ? $"{action}"
                    : "NoAction";
                var txId = tx.Id.ToString();
                var stage = true;

                try
                {
                    Log.Debug("PutTransaction: (nonce: {nonce}, id: {id})", tx.Nonce, tx.Id);
                    Log.Debug("StagedTransactions: {txIds}", string.Join(", ", _blockChainRepository.GetStagedTransactionIds()));
#pragma warning disable CS8632
                    Exception? validationExc = _blockChainRepository.ValidateNextBlockTx(tx);
#pragma warning restore CS8632
                    if (validationExc is null)
                    {
                        stage = _blockChainRepository.StageTransaction(tx);
                        _swarmRepository.BroadcastTxs(new[] { tx });
                    }
                    else
                    {
                        Log.Debug("Skip StageTransaction({TxId}) reason: {Msg}", tx.Id, validationExc.Message);
                    }

                    return new UnaryResult<bool>(stage);
                }
                catch (InvalidTxException ite)
                {
                    Log.Error(ite, $"{nameof(InvalidTxException)} occurred during {nameof(PutTransaction)}(). {{e}}", ite);
                    return new UnaryResult<bool>(false);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, $"Unexpected exception occurred during {nameof(PutTransaction)}(). {{e}}", e);
                throw;
            }
        }

        public UnaryResult<byte[]> GetStateByBlockHash(
            byte[] blockHashBytes,
            byte[] accountAddressBytes,
            byte[] addressBytes)
        {
            var hash = new BlockHash(blockHashBytes);
            var accountAddress = new Address(accountAddressBytes);
            var address = new Address(addressBytes);
            IValue state = _worldStateRepository
                .GetWorldState(hash)
                .GetAccountState(accountAddress)
                .GetState(address);
            // FIXME: Null과 null 구분해서 반환해야 할 듯
            byte[] encoded = _codec.Encode(state ?? Null.Value);
            return new UnaryResult<byte[]>(encoded);
        }

        public UnaryResult<byte[]> GetStateByStateRootHash(
            byte[] stateRootHashBytes,
            byte[] accountAddressBytes,
            byte[] addressBytes)
        {
            var stateRootHash = new HashDigest<SHA256>(stateRootHashBytes);
            var accountAddress = new Address(accountAddressBytes);
            var address = new Address(addressBytes);
            IValue state = _worldStateRepository
                .GetWorldState(stateRootHash)
                .GetAccountState(accountAddress)
                .GetState(address);
            byte[] encoded = _codec.Encode(state ?? Null.Value);
            return new UnaryResult<byte[]>(encoded);
        }

        public async UnaryResult<Dictionary<byte[], byte[]>> GetAgentStatesByBlockHash(
            byte[] blockHashBytes,
            IEnumerable<byte[]> addressBytesList)
        {
            var hash = new BlockHash(blockHashBytes);
            var worldState = _worldStateRepository.GetWorldState(hash);
            var result = new ConcurrentDictionary<byte[], byte[]>();
            var taskList = addressBytesList.Select(addressByte => Task.Run(() =>
            {
                var value = worldState.GetResolvedState(new Address(addressByte), Addresses.Agent);
                result.TryAdd(addressByte, _codec.Encode(value ?? Null.Value));
            }));

            await Task.WhenAll(taskList);
            return result.ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public async UnaryResult<Dictionary<byte[], byte[]>> GetAgentStatesByStateRootHash(
            byte[] stateRootHashBytes,
            IEnumerable<byte[]> addressBytesList)
        {
            var stateRootHash = new HashDigest<SHA256>(stateRootHashBytes);
            var worldState = _worldStateRepository.GetWorldState(stateRootHash);
            var result = new ConcurrentDictionary<byte[], byte[]>();
            var taskList = addressBytesList.Select(addressByte => Task.Run(() =>
            {
                var value = worldState.GetResolvedState(new Address(addressByte), Addresses.Agent);
                result.TryAdd(addressByte, _codec.Encode(value ?? Null.Value));
            }));

            await Task.WhenAll(taskList);
            return result.ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public async UnaryResult<Dictionary<byte[], byte[]>> GetAvatarStatesByBlockHash(
            byte[] blockHashBytes,
            IEnumerable<byte[]> addressBytesList)
        {
            var hash = new BlockHash(blockHashBytes);
            var worldState = _worldStateRepository.GetWorldState(hash);
            var result = new ConcurrentDictionary<byte[], byte[]>();
            var addresses = addressBytesList.Select(a => new Address(a)).ToList();
            var taskList = addresses.Select(address => Task.Run(() =>
            {
                var value = GetFullAvatarStateRaw(worldState, address);
                result.TryAdd(address.ToByteArray(), _codec.Encode(value ?? Null.Value));
            }));

            await Task.WhenAll(taskList);
            return result.ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public async UnaryResult<Dictionary<byte[], byte[]>> GetAvatarStatesByStateRootHash(
            byte[] stateRootHashBytes,
            IEnumerable<byte[]> addressBytesList)
        {
            var addresses = addressBytesList.Select(a => new Address(a)).ToList();
            var stateRootHash = new HashDigest<SHA256>(stateRootHashBytes);
            var worldState = _worldStateRepository.GetWorldState(stateRootHash);
            var result = new ConcurrentDictionary<byte[], byte[]>();
            var taskList = addresses.Select(address => Task.Run(() =>
            {
                var value = GetFullAvatarStateRaw(worldState, address);
                result.TryAdd(address.ToByteArray(), _codec.Encode(value ?? Null.Value));
            }));

            await Task.WhenAll(taskList);
            return result.ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public UnaryResult<Dictionary<byte[], byte[]>> GetBulkStateByBlockHash(
            byte[] blockHashBytes,
            byte[] accountAddressBytes,
            IEnumerable<byte[]> addressBytesList)
        {
            var blockHash = new BlockHash(blockHashBytes);
            var accountAddress = new Address(accountAddressBytes);
            List<Address> addresses = addressBytesList.Select(b => new Address(b)).ToList();

            var result = new Dictionary<byte[], byte[]>();
            IReadOnlyList<IValue> values = _worldStateRepository
                .GetWorldState(blockHash)
                .GetAccountState(accountAddress)
                .GetStates(addresses);
            for (int i = 0; i < addresses.Count; i++)
            {
                result.TryAdd(addresses[i].ToByteArray(), _codec.Encode(values[i] ?? Null.Value));
            }

            return new UnaryResult<Dictionary<byte[], byte[]>>(result);
        }

        public UnaryResult<Dictionary<byte[], byte[]>> GetBulkStateByStateRootHash(
            byte[] stateRootHashBytes,
            byte[] accountAddressBytes,
            IEnumerable<byte[]> addressBytesList)
        {
            var stateRootHash = new HashDigest<SHA256>(stateRootHashBytes);
            var accountAddress = new Address(accountAddressBytes);
            List<Address> addresses = addressBytesList.Select(b => new Address(b)).ToList();

            var result = new Dictionary<byte[], byte[]>();
            IReadOnlyList<IValue> values = _worldStateRepository
                .GetWorldState(stateRootHash)
                .GetAccountState(accountAddress)
                .GetStates(addresses);
            for (int i = 0; i < addresses.Count; i++)
            {
                result.TryAdd(addresses[i].ToByteArray(), _codec.Encode(values[i] ?? Null.Value));
            }

            return new UnaryResult<Dictionary<byte[], byte[]>>(result);
        }

        public UnaryResult<Dictionary<byte[], byte[]>> GetSheets(
            byte[] stateRootHashBytes,
            IEnumerable<byte[]> addressBytesList)
        {
            var started = DateTime.UtcNow;
            var sw = new Stopwatch();
            sw.Start();
            var result = new Dictionary<byte[], byte[]>();
            List<Address> addresses = new List<Address>();
            foreach (var b in addressBytesList)
            {
                var address = new Address(b);
                if (_memoryCache.TryGetSheet(address.ToString(), out byte[] cached))
                {
                    result.TryAdd(b, cached);
                }
                else
                {
                    addresses.Add(address);
                }
            }
            sw.Stop();
            Log.Information("[GetSheets]Get sheet from cache count: {CachedCount}, not Cached: {CacheMissedCount}, Elapsed: {Elapsed}", result.Count, addresses.Count, sw.Elapsed);
            sw.Restart();
            if (addresses.Any())
            {
                var stateRootHash = new HashDigest<SHA256>(stateRootHashBytes);
                IReadOnlyList<IValue> values = _worldStateRepository.GetWorldState(stateRootHash).GetLegacyStates(addresses);
                sw.Stop();
                Log.Information("[GetSheets]Get sheet from state: {Count}, Elapsed: {Elapsed}", addresses.Count, sw.Elapsed);
                sw.Restart();
                for (int i = 0; i < addresses.Count; i++)
                {
                    var address = addresses[i];
                    var value = values[i] ?? Null.Value;
                    var compressed = _memoryCache.SetSheet(address.ToString(), value, TimeSpan.FromMinutes(1));
                    result.TryAdd(address.ToByteArray(), compressed);
                }
            }
            Log.Information("[GetSheets]Total: {Elapsed}", DateTime.UtcNow - started);
            return new UnaryResult<Dictionary<byte[], byte[]>>(result);
        }

        public UnaryResult<byte[]> GetBalanceByBlockHash(
            byte[] blockHashBytes,
            byte[] addressBytes,
            byte[] currencyBytes)
        {
            var blockHash = new BlockHash(blockHashBytes);
            var address = new Address(addressBytes);
            var serializedCurrency = (Bencodex.Types.Dictionary)_codec.Decode(currencyBytes);
            Currency currency = CurrencyExtensions.Deserialize(serializedCurrency);
            FungibleAssetValue balance = _worldStateRepository
                .GetWorldState(blockHash)
                .GetBalance(address, currency);
            byte[] encoded = _codec.Encode(
              new Bencodex.Types.List(
                new IValue[]
                {
                  balance.Currency.Serialize(),
                  (Integer) balance.RawValue,
                }
              )
            );
            return new UnaryResult<byte[]>(encoded);
        }

        public UnaryResult<byte[]> GetBalanceByStateRootHash(
            byte[] stateRootHashBytes,
            byte[] addressBytes,
            byte[] currencyBytes)
        {
            var stateRootHash = new HashDigest<SHA256>(stateRootHashBytes);
            var address = new Address(addressBytes);
            var serializedCurrency = (Bencodex.Types.Dictionary)_codec.Decode(currencyBytes);
            Currency currency = CurrencyExtensions.Deserialize(serializedCurrency);
            FungibleAssetValue balance = _worldStateRepository
                .GetWorldState(stateRootHash)
                .GetBalance(address, currency);
            byte[] encoded = _codec.Encode(
              new Bencodex.Types.List(
                new IValue[]
                {
                  balance.Currency.Serialize(),
                  (Integer) balance.RawValue,
                }
              )
            );
            return new UnaryResult<byte[]>(encoded);
        }

        public UnaryResult<byte[]> GetUnbondClaimableHeightByStateRootHash(
            byte[] stateRootHashBytes, byte[] addressBytes)
        {
            var stateRootHash = new HashDigest<SHA256>(stateRootHashBytes);
            var address = new Address(addressBytes);
            var worldState = _worldStateRepository.GetWorldState(stateRootHash);
            var result = GetUnbondClaimableHeight(worldState, address);

            byte[] encoded = _codec.Encode(result);
            return new UnaryResult<byte[]>(encoded);
        }

        public UnaryResult<byte[]> GetClaimableRewardsByStateRootHash(
            byte[] stateRootHashBytes, byte[] addressBytes)
        {
            var stateRootHash = new HashDigest<SHA256>(stateRootHashBytes);
            var address = new Address(addressBytes);
            var worldState = _worldStateRepository.GetWorldState(stateRootHash);
            var result = GetClaimableRewards(worldState, address);

            byte[] encoded = _codec.Encode(result);
            return new UnaryResult<byte[]>(encoded);
        }

        public UnaryResult<byte[]> GetDelegationInfoByStateRootHash(
            byte[] stateRootHashBytes, byte[] addressBytes)
        {
            var stateRootHash = new HashDigest<SHA256>(stateRootHashBytes);
            var address = new Address(addressBytes);
            var worldState = _worldStateRepository.GetWorldState(stateRootHash);
            var result = GetDelegationInfo(worldState, address);

            byte[] encoded = _codec.Encode(result);
            return new UnaryResult<byte[]>(encoded);
        }

        public UnaryResult<byte[]> GetStakedByStateRootHash(
            byte[] stateRootHashBytes, byte[] addressBytes)
        {
            var stateRootHash = new HashDigest<SHA256>(stateRootHashBytes);
            var address = new Address(addressBytes);
            var worldState = _worldStateRepository.GetWorldState(stateRootHash);
            var result = GetStaked(worldState, address);

            byte[] encoded = _codec.Encode(result);
            return new UnaryResult<byte[]>(encoded);
        }

        public UnaryResult<byte[]> GetTip()
        {
            Bencodex.Types.Dictionary headerDict = _blockChainRepository.Tip.MarshalBlock();
            byte[] headerBytes = _codec.Encode(headerDict);
            return new UnaryResult<byte[]>(headerBytes);
        }

        public UnaryResult<byte[]> GetBlockHash(long blockIndex)
        {
            try
            {
                return new UnaryResult<byte[]>(_codec.Encode(_blockChainRepository.GetBlock(blockIndex).Hash.Bencoded));
            }
            catch (ArgumentOutOfRangeException)
            {
                return new UnaryResult<byte[]>(_codec.Encode(Null.Value));
            }
        }

        public UnaryResult<long> GetNextTxNonce(byte[] addressBytes)
        {
            var address = new Address(addressBytes);
            var nonce = _transactionRepository.GetNextTxNonce(address);
            Log.Debug("GetNextTxNonce: {nonce}", nonce);
            return new UnaryResult<long>(nonce);
        }

        public UnaryResult<bool> SetAddressesToSubscribe(byte[] addressBytes, IEnumerable<byte[]> addressesBytes)
        {
            if (_context.RpcRemoteSever)
            {
                _publisher.UpdateSubscribeAddresses(addressBytes, addressesBytes);
            }
            else
            {
                _context.AddressesToSubscribe =
                    addressesBytes.Select(ba => new Address(ba)).ToImmutableHashSet();
                Log.Debug(
                    "Subscribed addresses: {addresses}",
                    string.Join(", ", _context.AddressesToSubscribe));
            }
            return new UnaryResult<bool>(true);
        }

        public UnaryResult<bool> IsTransactionStaged(byte[] txidBytes)
        {
            var id = new TxId(txidBytes);
            var isStaged = _blockChainRepository.GetStagedTransactionIds().Contains(id);
            Log.Debug(
                "Transaction {id} is {1}.",
                id,
                isStaged ? "staged" : "not staged");
            return new UnaryResult<bool>(isStaged);
        }

        public UnaryResult<bool> ReportException(string code, string message)
        {
            Log.Debug(
                $"Reported exception from Unity player. " +
                $"(code: {code}, message: {message})"
            );

            switch (code)
            {
                case "26":
                case "27":
                    NodeExceptionType exceptionType = NodeExceptionType.ActionTimeout;
                    _libplanetNodeServiceProperties.NodeExceptionOccurred(exceptionType, message);
                    break;
            }

            return new UnaryResult<bool>(true);
        }

        public UnaryResult<bool> AddClient(byte[] addressBytes)
        {
            var address = new Address(addressBytes);
            _publisher.AddClient(address).Wait();
            return new UnaryResult<bool>(true);
        }

        public UnaryResult<bool> RemoveClient(byte[] addressBytes)
        {
            var address = new Address(addressBytes);
            _publisher.RemoveClient(address).Wait();
            return new UnaryResult<bool>(true);
        }

        private static IValue GetUnbondClaimableHeight(IWorldState worldState, Address address)
        {
            var repository = new GuildRepository(new World(worldState), new HallowActionContext { });
            try
            {
                var delegator = repository.GetDelegator(address);
                var expireHeights = delegator.UnbondingRefs
                    .Select(r => UnbondingFactory.GetUnbondingFromRef(r, repository))
                    .Where(u => u is UnbondLockIn)
                    .Select(u => u.LowestExpireHeight);

                if (expireHeights.Any())
                {
                    return new Integer(expireHeights.Min());
                }
                else
                {
                    return new Integer(-1L);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to get unbond claimable height. {e}", e);
                return null;
            }
        }

        private static IValue GetClaimableRewards(IWorldState worldState, Address address)
        {
            try
            {
                var repository = new GuildRepository(new World(worldState), new HallowActionContext { });
                var guildAddress = repository.GetGuildParticipant(address).GuildAddress;
                var validatorAddress = repository.GetGuild(guildAddress).ValidatorAddress;
                var guildDelegatee = repository.GetDelegatee(validatorAddress);
                var bond = repository.GetBond(guildDelegatee, address);
                var share = bond.Share;

                if (share.IsZero
                    || bond.LastDistributeHeight is null
                    || repository.GetCurrentRewardBase(guildDelegatee) is not RewardBase rewardBase)
                {
                    return List.Empty;
                }

                var lastRewardBase = repository.GetRewardBase(guildDelegatee, bond.LastDistributeHeight.Value);
                var rewards = guildDelegatee.CalculateRewards(share, rewardBase, lastRewardBase);

                return new List(rewards.Select(r => r.Serialize()));
            }
            catch (FailedLoadStateException)
            {
                return List.Empty;
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to get claimable rewards. {e}", e);
                return null;
            }
        }

        private static IValue GetDelegationInfo(IWorldState worldState, Address address)
        {
            try
            {
                var delegationInfo = new World(worldState).GetDelegationInfo(address);
                return List.Empty
                    .Add(delegationInfo.Share)
                    .Add(delegationInfo.TotalShare)
                    .Add(delegationInfo.TotalDelegated.Serialize());
            }
            catch (InvalidOperationException)
            {
                return List.Empty;
            }
            catch (Exception e)
            {
                Log.Error(e, "Failed to get delegation info. {e}", e);
                return null;
            }
        }

        private static IValue GetStaked(IWorldState worldState, Address address)
            => new World(worldState).GetStaked(address).Serialize();

        // Returning value is a list of [ Avatar, Inventory, QuestList, WorldInformation ]
        private static IValue GetFullAvatarStateRaw(IWorldState worldState, Address address)
        {
            var serializedAvatarRaw = worldState.GetAccountState(Addresses.Avatar).GetState(address);
            if (serializedAvatarRaw is not List)
            {
                Log.Warning(
                    "Avatar state ({AvatarAddress}) should be " +
                    "List but: {Raw}",
                    address.ToHex(),
                    serializedAvatarRaw);
                return null;
            }

            var serializedInventoryRaw =
                worldState.GetAccountState(Addresses.Inventory).GetState(address);
            var serializedQuestListRaw =
                worldState.GetAccountState(Addresses.QuestList).GetState(address);
            var serializedWorldInformationRaw =
                worldState.GetAccountState(Addresses.WorldInformation).GetState(address);

            return new List(
                serializedAvatarRaw,
                serializedInventoryRaw!,
                serializedQuestListRaw!,
                serializedWorldInformationRaw!);
        }
    }
}
