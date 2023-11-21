#nullable enable
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Reactive.Subjects;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using Grpc.Core;
using Grpc.Net.Client;
using Lib9c.Renderers;
using Libplanet.Action.State;
using Libplanet.Blockchain;
using Libplanet.Common;
using Libplanet.Crypto;
using Libplanet.Types.Blocks;
using Libplanet.Types.Tx;
using MagicOnion.Client;
using MessagePack;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Shared.Hubs;
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
        private readonly IBlockChainStates _blockChainStates;

        private readonly ConcurrentDictionary<Address, Client> _clients = new();
        private readonly ConcurrentDictionary<Address, string> _clientsByDevice = new();
        private readonly ConcurrentDictionary<string, HashSet<string>> _clientsByIp = new();
        private readonly ConcurrentDictionary<List<string>, List<string>> _clientsIpsList = new();
        private readonly IMemoryCache _cache;
        private MemoryCache _memoryCache;

        private RpcContext _context;
        private ConcurrentDictionary<string, Sentry.ITransaction> _sentryTraces;

        public ActionEvaluationPublisher(
            BlockRenderer blockRenderer,
            ActionRenderer actionRenderer,
            ExceptionRenderer exceptionRenderer,
            NodeStatusRenderer nodeStatusRenderer,
            IBlockChainStates blockChainStates,
            string host,
            int port,
            RpcContext context,
            ConcurrentDictionary<string, Sentry.ITransaction> sentryTraces,
            StateMemoryCache cache)
        {
            _blockRenderer = blockRenderer;
            _actionRenderer = actionRenderer;
            _exceptionRenderer = exceptionRenderer;
            _nodeStatusRenderer = nodeStatusRenderer;
            _blockChainStates = blockChainStates;
            _host = host;
            _port = port;
            _context = context;
            _sentryTraces = sentryTraces;
            var memoryCacheOptions = new MemoryCacheOptions();
            var options = Options.Create(memoryCacheOptions);
            _cache = new MemoryCache(options);
            _memoryCache = cache.SheetCache;

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
            meter.CreateObservableGauge(
                "ninechronicles_clients_count_by_ips",
                () => new[]
                {
                    new Measurement<int>(
                        GetClientsCountByIp(10),
                        new KeyValuePair<string, object?>("account-type", "multi")),
                    new Measurement<int>(
                        GetClientsCountByIp(0),
                        new KeyValuePair<string, object?>("account-type", "all")),
                },
                description: "Number of RPC clients connected grouped by ips.");

            ActionEvaluationHub.OnClientDisconnected += RemoveClient;
            _actionRenderer.EveryRender<PatchTableSheet>().Subscribe(ev =>
            {
                if (ev.Exception is null)
                {
                    var action = ev.Action;
                    var sheetAddress = Addresses.GetSheetAddress(action.TableName);
                    _memoryCache.Set(sheetAddress.ToString(), (Text)action.TableCsv);
                }
            });
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
            Client client = await Client.CreateAsync(channel, _blockChainStates, clientAddress, _context, _sentryTraces);
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

        public void AddClientAndIp(string ipAddress, string clientAddress)
        {
            if (!_clientsByIp.ContainsKey(ipAddress))
            {
                _clientsByIp[ipAddress] = new HashSet<string>();
            }

            _clientsByIp[ipAddress].Add(clientAddress);
        }

        public int GetClientsCountByIp(int minimum)
        {
            var finder = new IdGroupFinder(_cache);
            var groups = finder.FindGroups(_clientsByIp);
            return groups.Where(group => group.IDs.Count >= minimum)
                .Sum(group => group.IDs.Count);
        }

        public ConcurrentDictionary<List<string>, List<string>> GetClientsByIp(int minimum)
        {
            var finder = new IdGroupFinder(_cache);
            var groups = finder.FindGroups(_clientsByIp);
            ConcurrentDictionary<List<string>, List<string>> clientsIpList = new();
            foreach (var group in groups)
            {
                if (group.IDs.Count >= minimum)
                {
                    clientsIpList.TryAdd(group.IPs.ToList(), group.IDs.ToList());
                }
            }

            return new ConcurrentDictionary<List<string>, List<string>>(
                clientsIpList.OrderByDescending(x => x.Value.Count));
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
            catch (Exception e)
            {
                Log.Error(e, "[{ClientAddress}] Error while removing client.", clientAddressHex);
            }
        }

        private sealed class IdGroupFinder
        {
            private Dictionary<string, List<string>> adjacencyList = new();
            private HashSet<string> visited = new();
            private readonly IMemoryCache _memoryCache;

            public IdGroupFinder(IMemoryCache memoryCache)
            {
                _memoryCache = memoryCache;
            }

            public List<(HashSet<string> IPs, HashSet<string> IDs)> FindGroups(ConcurrentDictionary<string, HashSet<string>> dict)
            {
                // Create a serialized version of the input for caching purposes
                var serializedInput = "key";

                // Check cache
                if (_memoryCache.TryGetValue(serializedInput, out List<(HashSet<string> IPs, HashSet<string> IDs)> cachedResult))
                {
                    return cachedResult;
                }

                // Step 1: Construct the adjacency list
                foreach (var kvp in dict)
                {
                    var ip = kvp.Key;
                    if (!adjacencyList.ContainsKey(ip))
                    {
                        adjacencyList[ip] = new List<string>();
                    }

                    foreach (var id in kvp.Value)
                    {
                        adjacencyList[ip].Add(id);

                        if (!adjacencyList.ContainsKey(id))
                        {
                            adjacencyList[id] = new List<string>();
                        }
                        adjacencyList[id].Add(ip);
                    }
                }

                // Step 2: DFS to find connected components
                var groups = new List<(HashSet<string> IPs, HashSet<string> IDs)>();
                foreach (var node in adjacencyList.Keys)
                {
                    if (!visited.Contains(node))
                    {
                        var ips = new HashSet<string>();
                        var ids = new HashSet<string>();
                        DFS(node, ips, ids, dict);
                        groups.Add((ips, ids));
                    }
                }

                // Cache the result before returning. Here we set a sliding expiration of 1 hour.
                var cacheEntryOptions = new MemoryCacheEntryOptions
                {
                    AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(10)
                };
                _memoryCache.Set(serializedInput, groups, cacheEntryOptions);

                return groups;
            }

            private void DFS(string node, HashSet<string> ips, HashSet<string> ids, ConcurrentDictionary<string, HashSet<string>> dict)
            {
                if (visited.Contains(node))
                {
                    return;
                }

                visited.Add(node);

                // if node is an IP
                if (dict.ContainsKey(node))
                {
                    ips.Add(node);
                }
                else
                {
                    ids.Add(node);
                }

                foreach (var neighbor in adjacencyList[node])
                {
                    if (!visited.Contains(neighbor))
                    {
                        DFS(neighbor, ips, ids, dict);
                    }
                }
            }
        }

        private sealed class Client : IAsyncDisposable
        {
            private readonly IActionEvaluationHub _hub;
            private readonly IBlockChainStates _blockChainStates;
            private readonly RpcContext _context;
            private readonly Address _clientAddress;

            private IDisposable? _blockSubscribe;
            private IDisposable? _actionEveryRenderSubscribe;
            private IDisposable? _everyExceptionSubscribe;
            private IDisposable? _nodeStatusSubscribe;

            private Subject<NCActionEvaluation> _NCActionRenderSubject { get; }
                = new Subject<NCActionEvaluation>();

            public ImmutableHashSet<Address> TargetAddresses { get; set; }

            public readonly ConcurrentDictionary<string, Sentry.ITransaction> SentryTraces;

            private Client(
                IActionEvaluationHub hub,
                IBlockChainStates blockChainStates,
                Address clientAddress,
                RpcContext context,
                ConcurrentDictionary<string, Sentry.ITransaction> sentryTraces)
            {
                _hub = hub;
                _blockChainStates = blockChainStates;
                _clientAddress = clientAddress;
                _context = context;
                TargetAddresses = ImmutableHashSet<Address>.Empty;
                SentryTraces = sentryTraces;
            }

            public static async Task<Client> CreateAsync(
                GrpcChannel channel,
                IBlockChainStates blockChainStates,
                Address clientAddress,
                RpcContext context,
                ConcurrentDictionary<string, Sentry.ITransaction> sentryTraces)
            {
                IActionEvaluationHub hub = await StreamingHubClient.ConnectAsync<IActionEvaluationHub, IActionEvaluationHubReceiver>(
                    channel,
                    null!
                );
                await hub.JoinAsync(clientAddress.ToHex());

                return new Client(hub, blockChainStates, clientAddress, context, sentryTraces);
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
                    .SubscribeOn(NewThreadScheduler.Default)
                    .ObserveOn(NewThreadScheduler.Default)
                    .Subscribe(
                        async ev =>
                        {
                            try
                            {
                                Stopwatch stopwatch = new Stopwatch();
                                stopwatch.Start();
                                ActionBase? pa = ev.Action is RewardGold
                                    ? null
                                    : ev.Action;
                                var extra = new Dictionary<string, IValue>();
                                var encodeElapsedMilliseconds = stopwatch.ElapsedMilliseconds;

                                var eval = new NCActionEvaluation(pa, ev.Signer, ev.BlockIndex, ev.OutputState, ev.Exception, ev.PreviousState, ev.RandomSeed, extra);
                                var encoded = MessagePackSerializer.Serialize(eval);
                                var c = new MemoryStream();
                                await using (var df = new DeflateStream(c, CompressionLevel.Fastest))
                                {
                                    df.Write(encoded, 0, encoded.Length);
                                }

                                var compressed = c.ToArray();
                                Log.Verbose(
                                    "[{ClientAddress}] #{BlockIndex} Broadcasting render since the given action {Action}. eval size: {Size}",
                                    _clientAddress,
                                    ev.BlockIndex,
                                    ev.Action.GetType(),
                                    compressed.LongLength
                                );

                                await _hub.BroadcastRenderAsync(compressed);
                                stopwatch.Stop();

                                var broadcastElapsedMilliseconds = stopwatch.ElapsedMilliseconds - encodeElapsedMilliseconds;
                                Log
                                    .ForContext("tag", "Metric")
                                    .ForContext("subtag", "ActionEvaluationPublisherElapse")
                                    .Verbose(
                                        "[{ClientAddress}], #{BlockIndex}, {Action}," +
                                        " {EncodeElapsedMilliseconds}, {BroadcastElapsedMilliseconds}, {TotalElapsedMilliseconds}",
                                        _clientAddress,
                                        ev.BlockIndex,
                                        ev.Action.GetType(),
                                        encodeElapsedMilliseconds,
                                        broadcastElapsedMilliseconds,
                                        encodeElapsedMilliseconds + broadcastElapsedMilliseconds);
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
        }
    }
}
