using System;
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
using Lib9c.Renderer;
using Libplanet;
using Libplanet.Blocks;
using MagicOnion.Client;
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

        private readonly Dictionary<Address, (IActionEvaluationHub hub, ImmutableHashSet<Address> addresses)> _clients =
            new Dictionary<Address, (IActionEvaluationHub hub, ImmutableHashSet<Address> addresses)>();

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
            var client = StreamingHubClient.Connect<IActionEvaluationHub, IActionEvaluationHubReceiver>(
                new Channel(_host, _port, ChannelCredentials.Insecure),
                null!
            );
            await client.JoinAsync(clientAddress.ToHex());
            if (!_clients.ContainsKey(clientAddress))
            {
                _clients[clientAddress] = (client, ImmutableHashSet<Address>.Empty);
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
                    var formatter = new BinaryFormatter();
                    using var c = new MemoryStream();
                    using var df = new DeflateStream(c, System.IO.Compression.CompressionLevel.Fastest);

                    try
                    {
                        // FIXME Strip shop state from aev due to its size.
                        //       we should remove this code after resizing it.
                        ev.PreviousStates = ev.PreviousStates.SetState(ShopState.Address, new Null());
                        ev.OutputStates = ev.OutputStates.SetState(ShopState.Address, new Null());
                        formatter.Serialize(df, ev);
                        Log.Information("[{ClientAddress}] #{BlockIndex} Broadcasting render since the given action {Action}", clientAddress, ev.BlockIndex, ev.Action.GetType());
                        await client.BroadcastRenderAsync(c.ToArray());
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
                    var formatter = new BinaryFormatter();
                    using var c = new MemoryStream();
                    using var df = new DeflateStream(c, System.IO.Compression.CompressionLevel.Fastest);

                    try
                    {
                        // FIXME Strip shop state from aev due to its size.
                        //       we should remove this code after resizing it.
                        ev.PreviousStates = ev.PreviousStates.SetState(ShopState.Address, new Null());
                        ev.OutputStates = ev.OutputStates.SetState(ShopState.Address, new Null());
#pragma warning disable SYSLIB0011, CS0618 // FIXME
                        formatter.Serialize(df, ev);
#pragma warning restore SYSLIB0011, CS0618
                        await client.BroadcastUnrenderAsync(c.ToArray());
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
            return _clients[clientAddress].addresses.Any(address =>
                ev.Signer.Equals(address) || updatedAddresses.Contains(address));
        }

        public void UpdateSubscribeAddresses(byte[] addressBytes, IEnumerable<byte[]> addressesBytes)
        {
            var address = new Address(addressBytes);
            var addresses = addressesBytes.Select(a => new Address(a)).ToImmutableHashSet();
            _clients[address] = (_clients[address].hub, addresses);
        }

        public async Task RemoveClient(Address clientAddress)
        {
            if (_clients.ContainsKey(clientAddress))
            {
                var client = _clients[clientAddress].hub;
                await client.LeaveAsync();
                _clients.Remove(clientAddress);
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
                await RemoveClient(clientAddress);
            }
            catch (Exception)
            {
                // pass
            }
        }
    }
}
