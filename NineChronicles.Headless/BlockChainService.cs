#nullable disable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using Libplanet;
using Libplanet.Action;
using Libplanet.Assets;
using Libplanet.Blocks;
using Libplanet.Blockchain;
using Libplanet.Blockchain.Renderers;
using Libplanet.Net;
using Libplanet.Headless.Hosting;
using Libplanet.Tx;
using MagicOnion;
using MagicOnion.Server;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Shared.Services;
using Serilog;
using NCAction = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;
using NodeExceptionType = Libplanet.Headless.NodeExceptionType;
using Libplanet.Headless;
using Microsoft.AspNetCore.RateLimiting;
using Nekoyume.Model.State;
using Sentry;

namespace NineChronicles.Headless
{
    public class BlockChainService : ServiceBase<IBlockChainService>, IBlockChainService
    {
        private static readonly Codec Codec = new Codec();
        private BlockChain<NCAction> _blockChain;
        private Swarm<NCAction> _swarm;
        private RpcContext _context;
        private Codec _codec;
        private LibplanetNodeServiceProperties<NCAction> _libplanetNodeServiceProperties;
        private DelayedRenderer<NCAction> _delayedRenderer;
        private ActionEvaluationPublisher _publisher;
        private ConcurrentDictionary<string, Sentry.ITransaction> _sentryTraces;

        public BlockChainService(
            BlockChain<NCAction> blockChain,
            Swarm<NCAction> swarm,
            RpcContext context,
            LibplanetNodeServiceProperties<NCAction> libplanetNodeServiceProperties,
            ActionEvaluationPublisher actionEvaluationPublisher,
            ConcurrentDictionary<string, Sentry.ITransaction> sentryTraces)
        {
            _blockChain = blockChain;
            _delayedRenderer = blockChain.GetDelayedRenderer();
            _swarm = swarm;
            _context = context;
            _codec = new Codec();
            _libplanetNodeServiceProperties = libplanetNodeServiceProperties;
            _publisher = actionEvaluationPublisher;
            _sentryTraces = sentryTraces;
        }

        [EnableRateLimiting("GrpcRateLimiter")]
        public UnaryResult<bool> PutTransaction(byte[] txBytes)
        {
            try
            {
                Transaction<PolymorphicAction<ActionBase>> tx =
                    Transaction<PolymorphicAction<ActionBase>>.Deserialize(txBytes);

                var actionName = tx.CustomActions[0]?.GetInnerActionTypeName() ?? "NoAction";
                var txId = tx.Id.ToString();
                var sentryTrace = SentrySdk.StartTransaction(
                    actionName,
                    "PutTransaction");
                sentryTrace.SetTag("TxId", txId);
                var span = sentryTrace.StartChild(
                    "BroadcastTX",
                    $"Broadcast Transaction {txId}");

                try
                {
                    tx.Validate();
                    Log.Debug("PutTransaction: (nonce: {nonce}, id: {id})", tx.Nonce, tx.Id);
                    Log.Debug("StagedTransactions: {txIds}", string.Join(", ", _blockChain.GetStagedTransactionIds()));
                    _blockChain.StageTransaction(tx);
                    _swarm.BroadcastTxs(new[] { tx });

                    span.Finish();
                    sentryTrace.StartChild(
                        "ExecuteAction",
                        $"Execute Action {actionName} from tx {txId}");
                    _sentryTraces.TryAdd(txId, sentryTrace);
                    return new UnaryResult<bool>(true);
                }
                catch (InvalidTxException ite)
                {
                    Log.Error(ite, $"{nameof(InvalidTxException)} occurred during {nameof(PutTransaction)}(). {{e}}", ite);
                    sentryTrace.Finish(ite);
                    return new UnaryResult<bool>(false);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, $"Unexpected exception occurred during {nameof(PutTransaction)}(). {{e}}", e);
                throw;
            }
        }

        public UnaryResult<byte[]> GetState(byte[] addressBytes, byte[] blockHashBytes)
        {
            var address = new Address(addressBytes);
            var hash = new BlockHash(blockHashBytes);
            IValue state = _blockChain.GetState(address, hash);
            // FIXME: Null과 null 구분해서 반환해야 할 듯
            byte[] encoded = _codec.Encode(state ?? new Null());
            return new UnaryResult<byte[]>(encoded);
        }

        public async UnaryResult<Dictionary<byte[], byte[]>> GetAvatarStates(IEnumerable<byte[]> addressBytesList, byte[] blockHashBytes)
        {
            var hash = new BlockHash(blockHashBytes);
            var accountStateGetter = _blockChain.ToAccountStateGetter(hash);
            var result = new ConcurrentDictionary<byte[], byte[]>();
            var addresses = addressBytesList.Select(a => new Address(a)).ToList();
            var rawAvatarStates = accountStateGetter.GetRawAvatarStates(addresses);
            var taskList = rawAvatarStates
                .Select(pair => Task.Run(() =>
                {
                    result.TryAdd(pair.Key.ToByteArray(), _codec.Encode(pair.Value));
                }))
                .ToList();

            await Task.WhenAll(taskList);
            return result.ToDictionary(kv => kv.Key, kv => kv.Value);
        }

        public UnaryResult<Dictionary<byte[], byte[]>> GetStateBulk(IEnumerable<byte[]> addressBytesList, byte[] blockHashBytes)
        {
            var hash = new BlockHash(blockHashBytes);
            var result = new Dictionary<byte[], byte[]>();
            Address[] addresses = addressBytesList.Select(b => new Address(b)).ToArray();
            IReadOnlyList<IValue> values = _blockChain.GetStates(addresses, hash);
            for (int i = 0; i < addresses.Length; i++)
            {
                result.TryAdd(addresses[i].ToByteArray(), _codec.Encode(values[i] ?? new Null()));
            }

            return new UnaryResult<Dictionary<byte[], byte[]>>(result);
        }

        public UnaryResult<byte[]> GetBalance(byte[] addressBytes, byte[] currencyBytes, byte[] blockHashBytes)
        {
            var address = new Address(addressBytes);
            var serializedCurrency = (Bencodex.Types.Dictionary)_codec.Decode(currencyBytes);
            Currency currency = CurrencyExtensions.Deserialize(serializedCurrency);
            var hash = new BlockHash(blockHashBytes);
            FungibleAssetValue balance = _blockChain.GetBalance(address, currency, hash);
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

        public UnaryResult<byte[]> GetTip()
        {
            Bencodex.Types.Dictionary headerDict = _blockChain.Tip.MarshalBlock();
            byte[] headerBytes = Codec.Encode(headerDict);
            return new UnaryResult<byte[]>(headerBytes);
        }

        public UnaryResult<long> GetNextTxNonce(byte[] addressBytes)
        {
            var address = new Address(addressBytes);
            var nonce = _blockChain.GetNextTxNonce(address);
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
            var isStaged = _blockChain.GetStagedTransactionIds().Contains(id);
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
    }
}
