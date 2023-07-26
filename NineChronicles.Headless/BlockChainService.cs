#nullable disable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using Libplanet.Blockchain;
using Libplanet.Crypto;
using Libplanet.Headless.Hosting;
using Libplanet.Net;
using Libplanet.Types.Assets;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;
using MagicOnion;
using MagicOnion.Server;
using Nekoyume;
using Nekoyume.Shared.Services;
using Serilog;
using Nekoyume.Model.State;
using Sentry;
using static NineChronicles.Headless.NCActionUtils;
using NodeExceptionType = Libplanet.Headless.NodeExceptionType;
using Transaction = Libplanet.Types.Tx.Transaction;

namespace NineChronicles.Headless
{
    public class BlockChainService : ServiceBase<IBlockChainService>, IBlockChainService
    {
        private static readonly Codec Codec = new Codec();
        private BlockChain _blockChain;
        private Swarm _swarm;
        private RpcContext _context;
        private Codec _codec;
        private LibplanetNodeServiceProperties _libplanetNodeServiceProperties;
        private ActionEvaluationPublisher _publisher;
        private ConcurrentDictionary<string, Sentry.ITransaction> _sentryTraces;

        public BlockChainService(
            BlockChain blockChain,
            Swarm swarm,
            RpcContext context,
            LibplanetNodeServiceProperties libplanetNodeServiceProperties,
            ActionEvaluationPublisher actionEvaluationPublisher,
            ConcurrentDictionary<string, Sentry.ITransaction> sentryTraces)
        {
            _blockChain = blockChain;
            _swarm = swarm;
            _context = context;
            _codec = new Codec();
            _libplanetNodeServiceProperties = libplanetNodeServiceProperties;
            _publisher = actionEvaluationPublisher;
            _sentryTraces = sentryTraces;
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
                var sentryTrace = SentrySdk.StartTransaction(
                    actionName,
                    "PutTransaction");
                sentryTrace.SetTag("TxId", txId);
                var span = sentryTrace.StartChild(
                    "BroadcastTX",
                    $"Broadcast Transaction {txId}");

                try
                {
                    Log.Debug("PutTransaction: (nonce: {nonce}, id: {id})", tx.Nonce, tx.Id);
                    Log.Debug("StagedTransactions: {txIds}", string.Join(", ", _blockChain.GetStagedTransactionIds()));
#pragma warning disable CS8632
                    Exception? validationExc = _blockChain.Policy.ValidateNextBlockTx(_blockChain, tx);
#pragma warning restore CS8632
                    if (validationExc is null)
                    {
                        _blockChain.StageTransaction(tx);
                        _swarm.BroadcastTxs(new[] { tx });
                    }
                    else
                    {
                        Log.Debug("Skip StageTransaction({TxId}) reason: {Msg}", tx.Id, validationExc.Message);
                    }

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
            IValue state = _blockChain.GetStates(new[] { address }, hash)[0];
            // FIXME: Null과 null 구분해서 반환해야 할 듯
            byte[] encoded = _codec.Encode(state ?? new Null());
            return new UnaryResult<byte[]>(encoded);
        }

        public async UnaryResult<Dictionary<byte[], byte[]>> GetAvatarStates(IEnumerable<byte[]> addressBytesList, byte[] blockHashBytes)
        {
            var hash = new BlockHash(blockHashBytes);
            var accountState = _blockChain.GetBlockState(hash);
            var result = new ConcurrentDictionary<byte[], byte[]>();
            var addresses = addressBytesList.Select(a => new Address(a)).ToList();
            var rawAvatarStates = accountState.GetRawAvatarStates(addresses);
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
