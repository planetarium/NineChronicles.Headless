#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.Metrics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using Grpc.Core;
using Grpc.Net.Client;
using Lib9c.Abstractions;
using Lib9c.Renderers;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;
using MagicOnion.Client;
using MessagePack;
using Microsoft.Extensions.Hosting;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Nekoyume.Shared.Hubs;
using Sentry;
using Serilog;

namespace NineChronicles.Headless
{
    public class ActionEvaluationPublisher : BackgroundService
    {
        private static readonly Codec Codec = new Codec();
        private readonly string _host;
        private readonly int _port;
        private readonly BlockRenderer _blockRenderer;
        private readonly ActionRenderer _actionRenderer;
        private readonly ExceptionRenderer _exceptionRenderer;
        private readonly NodeStatusRenderer _nodeStatusRenderer;

        private readonly ConcurrentDictionary<Address, Client> _clients = new ConcurrentDictionary<Address, Client>();
        private readonly ConcurrentDictionary<Address, string> _clientsByDevice = new ConcurrentDictionary<Address, string>();

        private RpcContext _context;
        private ConcurrentDictionary<string, Sentry.ITransaction> _sentryTraces;

        public ActionEvaluationPublisher(
            BlockRenderer blockRenderer,
            ActionRenderer actionRenderer,
            ExceptionRenderer exceptionRenderer,
            NodeStatusRenderer nodeStatusRenderer,
            string host,
            int port,
            RpcContext context,
            ConcurrentDictionary<string, Sentry.ITransaction> sentryTraces)
        {
            _blockRenderer = blockRenderer;
            _actionRenderer = actionRenderer;
            _exceptionRenderer = exceptionRenderer;
            _nodeStatusRenderer = nodeStatusRenderer;
            _host = host;
            _port = port;
            _context = context;
            _sentryTraces = sentryTraces;

            var meter = new Meter("NineChronicles");
            meter.CreateObservableGauge(
                "ninechronicles_rpc_clients_count",
                () => this.GetClients().Count,
                description: "Number of RPC clients connected.");
            meter.CreateObservableGauge(
                "ninechronicles_rpc_clients_count_by_device",
                () => new[]
                {
                    new Measurement<int>(this.GetClientsCountByDevice("mobile"), new[] { new KeyValuePair<string, object?>("device", "mobile") }),
                    new Measurement<int>(this.GetClientsCountByDevice("pc"), new[] { new KeyValuePair<string, object?>("device", "pc") }),
                    new Measurement<int>(this.GetClientsCountByDevice("other"), new[] { new KeyValuePair<string, object?>("device", "other") }),
                },
                description: "Number of RPC clients connected by device.");

            ActionEvaluationHub.OnClientDisconnected += RemoveClient;
        }

        protected override Task ExecuteAsync(CancellationToken stoppingToken)
        {
            return Task.CompletedTask;
        }

        public async Task AddClient(Address clientAddress)
        {
            var options = new GrpcChannelOptions
            {
                Credentials = ChannelCredentials.Insecure,
                MaxReceiveMessageSize = null
            };

            GrpcChannel channel = GrpcChannel.ForAddress($"http://{_host}:{_port}", options);
            Client client = await Client.CreateAsync(channel, clientAddress, _context, _sentryTraces);
            if (_clients.TryAdd(clientAddress, client))
            {
                if (clientAddress == default)
                {
                    Log.Warning("[{ClientAddress}] AddClient set default address", clientAddress);
                }

                Log.Information("[{ClientAddress}] AddClient", clientAddress);
                client.Subscribe(
                    _blockRenderer,
                    _actionRenderer,
                    _exceptionRenderer,
                    _nodeStatusRenderer
                );
            }
            else
            {
                await client.DisposeAsync();
            }
        }

        public void AddClientByDevice(Address clientAddress, string device)
        {
            if (!_clientsByDevice.ContainsKey(clientAddress))
            {
                _clientsByDevice.TryAdd(clientAddress, device);
            }
        }

        private void RemoveClientByDevice(Address clientAddress)
        {
            if (_clientsByDevice.ContainsKey(clientAddress))
            {
                _clientsByDevice.TryRemove(clientAddress, out _);
            }
        }

        public int GetClientsCountByDevice(string device)
        {
            return _clientsByDevice.Values.Count(x => x == device);
        }

        public List<Address> GetClientsByDevice(string device)
        {
            return _clientsByDevice
                .Where(x => x.Value == device)
                .Select(x => x.Key)
                .ToList();
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (Client? client in _clients.Values)
            {
                if (client is { })
                {
                    await client.DisposeAsync();
                }
            }
            await base.StopAsync(cancellationToken);
        }

        public void UpdateSubscribeAddresses(byte[] addressBytes, IEnumerable<byte[]> addressesBytes)
        {
            var address = new Address(addressBytes);
            if (address == default)
            {
                Log.Warning("[{ClientAddress}] UpdateSubscribeAddresses set default address", address);
            }
            var addresses = addressesBytes.Select(a => new Address(a)).ToImmutableHashSet();
            if (_clients.TryGetValue(address, out Client? client) && client is { })
            {
                lock (client)
                {
                    client.TargetAddresses = addresses;
                }

                Log.Information("[{ClientAddress}] UpdateSubscribeAddresses: {Addresses}", address, string.Join(", ", addresses));
            }
            else
            {
                Log.Error("[{ClientAddress}] target address does not contain in clients", address);
            }
        }

        public async Task RemoveClient(Address clientAddress)
        {
            if (_clients.TryGetValue(clientAddress, out Client? client) && client is { })
            {
                Log.Information("[{ClientAddress}] RemoveClient", clientAddress);
                await client.LeaveAsync();
                await client.DisposeAsync();
                _clients.TryRemove(clientAddress, out _);
            }
        }

        public List<Address> GetClients()
        {
            return _clients.Keys.ToList();
        }

        private async void RemoveClient(string clientAddressHex)
        {
            try
            {
                var clientAddress = new Address(ByteUtil.ParseHex(clientAddressHex));
                Log.Information("[{ClientAddress}] Client Disconnected. RemoveClient", clientAddress);
                RemoveClientByDevice(clientAddress);
                await RemoveClient(clientAddress);
            }
            catch (Exception)
            {
                Log.Error("[{ClientAddress }] Client ");
            }
        }

        private sealed class Client : IAsyncDisposable
        {
            private readonly IActionEvaluationHub _hub;
            private readonly RpcContext _context;
            private readonly Address _clientAddress;

            private IDisposable? _blockSubscribe;
            private IDisposable? _actionEveryRenderSubscribe;
            private IDisposable? _everyExceptionSubscribe;
            private IDisposable? _nodeStatusSubscribe;

            public ImmutableHashSet<Address> TargetAddresses { get; set; }

            public readonly ConcurrentDictionary<string, Sentry.ITransaction> SentryTraces;

            private Client(
                IActionEvaluationHub hub,
                Address clientAddress,
                RpcContext context,
                ConcurrentDictionary<string, Sentry.ITransaction> sentryTraces)
            {
                _hub = hub;
                _clientAddress = clientAddress;
                _context = context;
                TargetAddresses = ImmutableHashSet<Address>.Empty;
                SentryTraces = sentryTraces;
            }

            public static async Task<Client> CreateAsync(
                GrpcChannel channel,
                Address clientAddress,
                RpcContext context,
                ConcurrentDictionary<string, Sentry.ITransaction> sentryTraces)
            {
                IActionEvaluationHub hub = await StreamingHubClient.ConnectAsync<IActionEvaluationHub, IActionEvaluationHubReceiver>(
                    channel,
                    null!
                );
                await hub.JoinAsync(clientAddress.ToHex());

                return new Client(hub, clientAddress, context, sentryTraces);
            }

            public void Subscribe(
                BlockRenderer blockRenderer,
                ActionRenderer actionRenderer,
                ExceptionRenderer exceptionRenderer,
                NodeStatusRenderer nodeStatusRenderer)
            {
                _blockSubscribe = blockRenderer.BlockSubject
                    .SubscribeOn(NewThreadScheduler.Default)
                    .ObserveOn(NewThreadScheduler.Default)
                    .Subscribe(
                        async pair =>
                        {
                            try
                            {
                                await _hub.BroadcastRenderBlockAsync(
                                    Codec.Encode(pair.OldTip.MarshalBlock()),
                                    Codec.Encode(pair.NewTip.MarshalBlock())
                                );
                            }
                            catch (Exception e)
                            {
                                // FIXME add logger as property
                                Log.Error(e, "Skip broadcasting block render due to the unexpected exception");
                            }
                        }
                    );

                _actionEveryRenderSubscribe = actionRenderer.EveryRender<ActionBase>()
                    .Where(ContainsAddressToBroadcast)
                    .SubscribeOn(NewThreadScheduler.Default)
                    .ObserveOn(NewThreadScheduler.Default)
                    .Subscribe(
                        async ev =>
                        {
                            try
                            {
                                ActionBase? pa = ev.Action is RewardGold
                                    ? null
                                    : ev.Action;
                                var extra = new Dictionary<string, IValue>();

                                var previousStates = ev.PreviousState;
                                if (pa is IBattleArenaV1 battleArena)
                                {
                                    var enemyAvatarAddress = battleArena.EnemyAvatarAddress;
                                    if (previousStates.GetState(enemyAvatarAddress) is { } eAvatar)
                                    {
                                        const string inventoryKey = "inventory";
                                        previousStates = previousStates.SetState(enemyAvatarAddress, eAvatar);
                                        if (previousStates.GetState(enemyAvatarAddress.Derive(inventoryKey)) is { } inventory)
                                        {
                                            previousStates = previousStates.SetState(
                                                enemyAvatarAddress.Derive(inventoryKey),
                                                inventory);
                                        }
                                    }

                                    var enemyItemSlotStateAddress =
                                        ItemSlotState.DeriveAddress(battleArena.EnemyAvatarAddress,
                                            Nekoyume.Model.EnumType.BattleType.Arena);
                                    if (previousStates.GetState(enemyItemSlotStateAddress) is { } eItemSlot)
                                    {
                                        previousStates = previousStates.SetState(enemyItemSlotStateAddress, eItemSlot);
                                    }

                                    var enemyRuneSlotStateAddress =
                                        RuneSlotState.DeriveAddress(battleArena.EnemyAvatarAddress,
                                            Nekoyume.Model.EnumType.BattleType.Arena);
                                    if (previousStates.GetState(enemyRuneSlotStateAddress) is { } eRuneSlot)
                                    {
                                        previousStates = previousStates.SetState(enemyRuneSlotStateAddress, eRuneSlot);
                                        var runeSlot = new RuneSlotState(eRuneSlot as List);
                                        var enemyRuneSlotInfos = runeSlot.GetEquippedRuneSlotInfos();
                                        var runeAddresses = enemyRuneSlotInfos.Select(info =>
                                            RuneState.DeriveAddress(battleArena.EnemyAvatarAddress, info.RuneId));
                                        foreach (var address in runeAddresses)
                                        {
                                            if (previousStates.GetState(address) is { } rune)
                                            {
                                                previousStates = previousStates.SetState(address, rune);
                                            }
                                        }
                                    }
                                }

                                var eval = new NCActionEvaluation(pa, ev.Signer, ev.BlockIndex, ev.OutputState, ev.Exception, previousStates, ev.RandomSeed, extra);
                                var encoded = MessagePackSerializer.Serialize(eval);
                                var c = new MemoryStream();
                                await using (var df = new DeflateStream(c, CompressionLevel.Fastest))
                                {
                                    df.Write(encoded, 0, encoded.Length);
                                }

                                var compressed = c.ToArray();
                                Log.Information(
                                    "[{ClientAddress}] #{BlockIndex} Broadcasting render since the given action {Action}. eval size: {Size}",
                                    _clientAddress,
                                    ev.BlockIndex,
                                    ev.Action.GetType(),
                                    compressed.LongLength
                                );

                                await _hub.BroadcastRenderAsync(compressed);
                            }
                            catch (SerializationException se)
                            {
                                // FIXME add logger as property
                                Log.Error(se, "[{ClientAddress}] Skip broadcasting render since the given action isn't serializable", _clientAddress);
                            }
                            catch (Exception e)
                            {
                                // FIXME add logger as property
                                Log.Error(e, "[{ClientAddress}] Skip broadcasting render due to the unexpected exception", _clientAddress);
                            }

                            if (ev.TxId is TxId txId && SentryTraces.TryRemove(txId.ToString() ?? "", out var sentryTrace))
                            {
                                var span = sentryTrace.GetLastActiveSpan();
                                span?.Finish();
                                sentryTrace.Finish();
                            }
                        }
                    );

                _everyExceptionSubscribe = exceptionRenderer.EveryException()
                    .SubscribeOn(NewThreadScheduler.Default)
                    .ObserveOn(NewThreadScheduler.Default)
                    .Subscribe(
                        async tuple =>
                        {
                            try
                            {
                                (RPC.Shared.Exceptions.RPCException code, string message) = tuple;
                                await _hub.ReportExceptionAsync((int)code, message);
                            }
                            catch (Exception e)
                            {
                                // FIXME add logger as property
                                Log.Error(e, "Skip broadcasting exception due to the unexpected exception");
                            }
                        }
                    );

                _nodeStatusSubscribe = nodeStatusRenderer.EveryChangedStatus()
                    .SubscribeOn(NewThreadScheduler.Default)
                    .ObserveOn(NewThreadScheduler.Default)
                    .Subscribe(
                        async preloadStarted =>
                        {
                            try
                            {
                                if (preloadStarted)
                                {
                                    await _hub.PreloadStartAsync();
                                }
                                else
                                {
                                    await _hub.PreloadEndAsync();
                                }
                            }
                            catch (Exception e)
                            {
                                // FIXME add logger as property
                                Log.Error(e, "Skip broadcasting status change due to the unexpected exception");
                            }
                        }
                    );
            }

            public Task LeaveAsync() => _hub.LeaveAsync();

            public async ValueTask DisposeAsync()
            {
                _blockSubscribe?.Dispose();
                _actionEveryRenderSubscribe?.Dispose();
                _everyExceptionSubscribe?.Dispose();
                _nodeStatusSubscribe?.Dispose();
                await _hub.DisposeAsync();
            }

            private bool ContainsAddressToBroadcast(ActionEvaluation<ActionBase> ev)
            {
                return _context.RpcRemoteSever
                    ? ContainsAddressToBroadcastRemoteClient(ev)
                    : ContainsAddressToBroadcastLocal(ev);
            }

            private bool ContainsAddressToBroadcastLocal(ActionEvaluation<ActionBase> ev)
            {
                var updatedAddresses = ev.OutputState.Delta.UpdatedAddresses;
                return _context.AddressesToSubscribe.Any(updatedAddresses.Add(ev.Signer).Contains);
            }

            private bool ContainsAddressToBroadcastRemoteClient(ActionEvaluation<ActionBase> ev)
            {
                var updatedAddresses = ev.OutputState.Delta.UpdatedAddresses;
                return TargetAddresses.Any(updatedAddresses.Add(ev.Signer).Contains);
            }
        }
    }
}
