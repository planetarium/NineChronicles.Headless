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
        public BlockChainService(
            BlockChain<NCAction> blockChain,
            Swarm<NCAction> swarm,
            RpcContext context,
            LibplanetNodeServiceProperties<NCAction> libplanetNodeServiceProperties,
            ActionEvaluationPublisher actionEvaluationPublisher
        )
        {
            _blockChain = blockChain;
            _delayedRenderer = blockChain.GetDelayedRenderer();
            _swarm = swarm;
            _context = context;
            _codec = new Codec();
            _libplanetNodeServiceProperties = libplanetNodeServiceProperties;
            _publisher = actionEvaluationPublisher;
        }

        public UnaryResult<bool> PutTransaction(byte[] txBytes)
        {
            try
            {
                Transaction<PolymorphicAction<ActionBase>> tx =
                    Transaction<PolymorphicAction<ActionBase>>.Deserialize(txBytes);

                try
                {
                    tx.Validate();
                    Log.Debug("PutTransaction: (nonce: {nonce}, id: {id})", tx.Nonce, tx.Id);
                    Log.Debug("StagedTransactions: {txIds}", string.Join(", ", _blockChain.GetStagedTransactionIds()));
                    _blockChain.StageTransaction(tx);
                    _swarm.BroadcastTxs(new[] {tx});

                    return UnaryResult(true);
                }
                catch (InvalidTxException ite)
                {
                    Log.Error(ite, $"{nameof(InvalidTxException)} occurred during {nameof(PutTransaction)}(). {{e}}", ite);
                    return UnaryResult(false);
                }
            }
            catch (Exception e)
            {
                Log.Error(e, $"Unexpected exception occurred during {nameof(PutTransaction)}(). {{e}}", e);
                throw;
            }
        }

        public UnaryResult<byte[]> GetState(byte[] addressBytes)
        {
            var address = new Address(addressBytes);
            IValue state = _blockChain.GetState(address, _delayedRenderer?.Tip?.Hash);
            // FIXME: Null과 null 구분해서 반환해야 할 듯
            byte[] encoded = _codec.Encode(state ?? new Null());
            return UnaryResult(encoded);
        }

        public UnaryResult<Dictionary<byte[], byte[]>> GetAvatarStates(IEnumerable<byte[]> addressBytesList)
        {
            var accountStateGetter = _blockChain.ToAccountStateGetter(_delayedRenderer?.Tip?.Hash);
            var result = new ConcurrentDictionary<byte[], byte[]>();
            Parallel.ForEach(addressBytesList, addressBytes =>
            {
                var avatarAddress = new Address(addressBytes);
                var avatarState = accountStateGetter.GetAvatarState(avatarAddress);
                result[addressBytes] = _codec.Encode(avatarState.Serialize());
            });

            return UnaryResult(result.ToDictionary(kv => kv.Key, kv => kv.Value));
        }

        public UnaryResult<Dictionary<byte[], byte[]>> GetStateBulk(IEnumerable<byte[]> addressBytesList)
        {
            BlockHash? hash = _delayedRenderer?.Tip?.Hash;
            var result = new ConcurrentDictionary<byte[], byte[]>();
            Parallel.ForEach(addressBytesList, addressBytes =>
            {
                var address = new Address(addressBytes);
                if (_blockChain.GetState(address, hash) is { } value)
                {
                    result[addressBytes] = _codec.Encode(value);

                }
            });

            return UnaryResult(result.ToDictionary(kv => kv.Key, kv => kv.Value));
        }

        public UnaryResult<byte[]> GetBalance(byte[] addressBytes, byte[] currencyBytes)
        {
            var address = new Address(addressBytes);
            var serializedCurrency = (Bencodex.Types.Dictionary)_codec.Decode(currencyBytes);
            Currency currency = CurrencyExtensions.Deserialize(serializedCurrency);
            FungibleAssetValue balance = _blockChain.GetBalance(address, currency);
            byte[] encoded = _codec.Encode(
              new Bencodex.Types.List(
                new IValue[] 
                {
                  balance.Currency.Serialize(),
                  (Integer) balance.RawValue,
                }
              )
            );
            return UnaryResult(encoded);
        }

        public UnaryResult<byte[]> GetTip()
        {
            Bencodex.Types.Dictionary headerDict = _blockChain.Tip.MarshalBlock();
            byte[] headerBytes = Codec.Encode(headerDict);
            return UnaryResult(headerBytes);
        }

        public UnaryResult<long> GetNextTxNonce(byte[] addressBytes)
        {
            var address = new Address(addressBytes);
            var nonce = _blockChain.GetNextTxNonce(address);
            Log.Debug("GetNextTxNonce: {nonce}", nonce);
            return UnaryResult(nonce);
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
            }
            Log.Debug(
                "Subscribed addresses: {addresses}",
                string.Join(", ", _context.AddressesToSubscribe));
            return UnaryResult(true);
        }

        public UnaryResult<bool> IsTransactionStaged(byte[] txidBytes)
        {
            var id = new TxId(txidBytes);
            var isStaged = _blockChain.GetStagedTransactionIds().Contains(id);
            Log.Debug(
                "Transaction {id} is {1}.",
                id,
                isStaged ? "staged" : "not staged");
            return UnaryResult(isStaged);
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

            return UnaryResult(true);
        }

        public UnaryResult<bool> AddClient(byte[] addressBytes)
        {
            var address = new Address(addressBytes);
            _publisher.AddClient(address).Wait();
            return UnaryResult(true);
        }

        public UnaryResult<bool> RemoveClient(byte[] addressBytes)
        {
            var address = new Address(addressBytes);
            _publisher.RemoveClient(address).Wait();
            return UnaryResult(true);
        }
    }
}
