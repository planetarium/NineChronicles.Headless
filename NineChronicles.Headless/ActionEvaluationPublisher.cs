using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using Bencodex;
using Bencodex.Types;
using Grpc.Core;
using Grpc.Net.Client;
using Lib9c.Renderer;
using Libplanet;
using Libplanet.Action;
using Libplanet.Blocks;
using MagicOnion.Client;
using MessagePack;
using Microsoft.Extensions.Hosting;
using Nekoyume.Action;
using Nekoyume.Model.State;
using Nekoyume.Shared.Hubs;
using Serilog;
using NineChroniclesActionType = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

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

        private readonly ConcurrentDictionary<Address, (IActionEvaluationHub hub, ImmutableHashSet<Address> addresses)> _clients =
            new ConcurrentDictionary<Address, (IActionEvaluationHub hub, ImmutableHashSet<Address> addresses)>();

        private RpcContext _context;

        public ActionEvaluationPublisher(
            BlockRenderer blockRenderer,
            ActionRenderer actionRenderer,
            ExceptionRenderer exceptionRenderer,
            NodeStatusRenderer nodeStatusRenderer,
            string host,
            int port,
            RpcContext context
        )
        {
            _blockRenderer = blockRenderer;
            _actionRenderer = actionRenderer;
            _exceptionRenderer = exceptionRenderer;
            _nodeStatusRenderer = nodeStatusRenderer;
            _host = host;
            _port = port;
            _context = context;

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

            var channel = GrpcChannel.ForAddress($"http://{_host}:{_port}", options);
            var client = await StreamingHubClient.ConnectAsync<IActionEvaluationHub, IActionEvaluationHubReceiver>(
                channel,
                null!
            );
            await client.JoinAsync(clientAddress.ToHex());
            if (_clients.TryAdd(clientAddress, (client, ImmutableHashSet<Address>.Empty)))
            {
                if (clientAddress == default)
                {
                    Log.Warning("[{ClientAddress}] AddClient set default address", clientAddress);
                }

                Log.Information("[{ClientAddress}] AddClient", clientAddress);
            }

            _blockRenderer.BlockSubject.Subscribe(
                async pair =>
                {
                    try
                    {
                        await client.BroadcastRenderBlockAsync(
                            Codec.Encode(pair.OldTip.MarshalBlock()),
                            Codec.Encode(pair.NewTip.MarshalBlock())
                        );
                    }
                    catch (Exception e)
                    {
                        // FIXME add logger as property
                        Log.Error(e, "Skip broadcasting blcok render due to the unexpected exception");
                    }
                }
            );

            _blockRenderer.ReorgSubject.Subscribe(
                async ev =>
                {
                    try
                    {
                        await client.ReportReorgAsync(
                            Codec.Encode(ev.OldTip.MarshalBlock()),
                            Codec.Encode(ev.NewTip.MarshalBlock()),
                            Codec.Encode(ev.Branchpoint.MarshalBlock())
                        );
                    }
                    catch (Exception e)
                    {
                        // FIXME add logger as property
                        Log.Error(e, "Skip broadcasting reorg due to the unexpected exception");
                    }
                }
            );

            _blockRenderer.ReorgEndSubject.Subscribe(
                async ev =>
                {
                    try
                    {
                        await client.ReportReorgEndAsync(
                            Codec.Encode(ev.OldTip.MarshalBlock()),
                            Codec.Encode(ev.NewTip.MarshalBlock()),
                            Codec.Encode(ev.Branchpoint.MarshalBlock())
                        );
                    }
                    catch (Exception e)
                    {
                        // FIXME add logger as property
                        Log.Error(e, "Skip broadcasting reorg end due to the unexpected exception");
                    }
                }
            );
            _actionRenderer.EveryRender<ActionBase>()
                .Where(ev => ContainsAddressToBroadcast(ev, clientAddress))
                .Subscribe(
                async ev =>
                {
                    try
                    {
                        NineChroniclesActionType? pa = null;
                        var extra = new Dictionary<string, IValue>();
                        if (!(ev.Action is RewardGold))
                        {
                            pa = new PolymorphicAction<ActionBase>(ev.Action);
                            if (ev.Action is RankingBattle rb)
                            {
                                if (rb.EnemyAvatarState is { } enemyAvatarState)
                                {
                                    extra[nameof(RankingBattle.EnemyAvatarState)] = enemyAvatarState.Serialize();
                                }
                                if (rb.EnemyArenaInfo is { } enemyArenaInfo)
                                {
                                    extra[nameof(RankingBattle.EnemyArenaInfo)] = enemyArenaInfo.Serialize();
                                }
                                if (rb.ArenaInfo is { } arenaInfo)
                                {
                                    extra[nameof(RankingBattle.ArenaInfo)] = arenaInfo.Serialize();
                                }
                            }

                            if (ev.Action is Buy buy)
                            {
                                extra[nameof(Buy.errors)] = new List(
                                    buy.errors
                                    .Select(tuple => new List(new[]
                                    {
                                        tuple.orderId.Serialize(),
                                        tuple.errorCode.Serialize()
                                    }))
                                    .Cast<IValue>()
                                );
                            }
                        }
                        var eval = new NCActionEvaluation(pa, ev.Signer, ev.BlockIndex, ev.OutputStates, ev.Exception, ev.PreviousStates, ev.RandomSeed, extra);
                        Log.Information("[{ClientAddress}] #{BlockIndex} Broadcasting render since the given action {Action}", clientAddress, ev.BlockIndex, ev.Action.GetType());
                        await client.BroadcastRenderAsync(MessagePackSerializer.Serialize(eval));
                    }
                    catch (SerializationException se)
                    {
                        // FIXME add logger as property
                        Log.Error(se, "[{ClientAddress}] Skip broadcasting render since the given action isn't serializable", clientAddress);
                    }
                    catch (Exception e)
                    {
                        // FIXME add logger as property
                        Log.Error(e, "[{ClientAddress}] Skip broadcasting render due to the unexpected exception", clientAddress);
                    }
                }
                );

            _actionRenderer.EveryUnrender<ActionBase>()
                .Where(ev => ContainsAddressToBroadcast(ev, clientAddress))
                .Subscribe(
                async ev =>
                {
                    PolymorphicAction<ActionBase>? pa = null;
                    if (!(ev.Action is RewardGold))
                    {
                        pa = new PolymorphicAction<ActionBase>(ev.Action);
                    }
                    try
                    {
                        var eval = new NCActionEvaluation(pa,
                            ev.Signer,
                            ev.BlockIndex,
                            ev.OutputStates,
                            ev.Exception,
                            ev.PreviousStates,
                            ev.RandomSeed,
                            new Dictionary<string, IValue>()
                        );
                        await client.BroadcastUnrenderAsync(MessagePackSerializer.Serialize(eval));
                    }
                    catch (SerializationException se)
                    {
                        // FIXME add logger as property
                        Log.Error(se, "Skip broadcasting unrender since the given action isn't serializable.");
                    }
                    catch (Exception e)
                    {
                        // FIXME add logger as property
                        Log.Error(e, "Skip broadcasting unrender due to the unexpected exception");
                    }
                }
                );
            _exceptionRenderer.EveryException().Subscribe(
                async tuple =>
                {
                    try
                    {
                        (RPC.Shared.Exceptions.RPCException code, string message) = tuple;
                        await client.ReportExceptionAsync((int)code, message);
                    }
                    catch (Exception e)
                    {
                        // FIXME add logger as property
                        Log.Error(e, "Skip broadcasting exception due to the unexpected exception");
                    }
                }
            );

            _nodeStatusRenderer.EveryChangedStatus().Subscribe(
                async isPreloadStarted =>
                {
                    try
                    {
                        if (isPreloadStarted)
                        {
                            await client.PreloadStartAsync();
                        }
                        else
                        {
                            await client.PreloadEndAsync();
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

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            foreach (var (client, _) in _clients.Values)
            {
                await client.DisposeAsync();
            }
            await base.StopAsync(cancellationToken);
        }

        private bool ContainsAddressToBroadcast(ActionBase.ActionEvaluation<ActionBase> ev)
        {
            var updatedAddresses =
                ev.OutputStates.UpdatedAddresses.Union(ev.OutputStates.UpdatedFungibleAssets.Keys);
            return _context.AddressesToSubscribe.Any(address =>
                ev.Signer.Equals(address) || updatedAddresses.Contains(address));
        }

        private bool ContainsAddressToBroadcast(ActionBase.ActionEvaluation<ActionBase> ev, Address clientAddress)
        {
            return _context.RpcRemoteSever
                ? ContainsAddressToBroadcastRemoteClient(ev, clientAddress)
                : ContainsAddressToBroadcast(ev);
        }

        private bool ContainsAddressToBroadcastRemoteClient(ActionBase.ActionEvaluation<ActionBase> ev,
            Address clientAddress)
        {
            var updatedAddresses =
                ev.OutputStates.UpdatedAddresses.Union(ev.OutputStates.UpdatedFungibleAssets.Keys);
            if (_clients.TryGetValue(clientAddress, out var tuple))
            {
                return tuple.addresses.Any(address =>
                    ev.Signer.Equals(address) || updatedAddresses.Contains(address));
            }
            return false;
        }

        public void UpdateSubscribeAddresses(byte[] addressBytes, IEnumerable<byte[]> addressesBytes)
        {
            var address = new Address(addressBytes);
            if (address == default)
            {
                Log.Warning("[{ClientAddress}] UpdateSubscribeAddresses set default address", address);
            }
            var addresses = addressesBytes.Select(a => new Address(a)).ToImmutableHashSet();
            if (_clients.TryGetValue(address, out var tuple) && _clients.TryUpdate(address, (tuple.hub, addresses), tuple))
            {
                Log.Information("[{ClientAddress}] UpdateSubscribeAddresses: {Addresses}", address, string.Join(", ", addresses));
            }
            else
            {
                Log.Error("[{ClientAddress}] target address does not contain in clients", address);
            }
        }

        public async Task RemoveClient(Address clientAddress)
        {
            if (_clients.TryGetValue(clientAddress, out var tuple))
            {
                Log.Information("[{ClientAddress}] RemoveClient", clientAddress);
                var client = tuple.hub;
                await client.LeaveAsync();
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
                await RemoveClient(clientAddress);
            }
            catch (Exception)
            {
                // pass
            }
        }
    }
}
