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
using Bencodex.Types;
using Grpc.Core;
using Lib9c.Renderer;
using Libplanet;
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

            _blockRenderer.EveryBlock().Subscribe(
                async pair =>
                {
                    try
                    {
                        await client.BroadcastRenderBlockAsync(
                            pair.OldTip.Header.Serialize(),
                            pair.NewTip.Header.Serialize()
                        );
                    }
                    catch (Exception e)
                    {
                        // FIXME add logger as property
                        Log.Error(e, "Skip broadcasting blcok render due to the unexpected exception");
                    }
                }
            );
        
            _blockRenderer.EveryReorg().Subscribe(
                async ev =>
                {
                    try
                    {
                        await client.ReportReorgAsync(
                            ev.OldTip.Serialize(),
                            ev.NewTip.Serialize(),
                            ev.Branchpoint.Serialize()
                        );
                    }
                    catch (Exception e)
                    {
                        // FIXME add logger as property
                        Log.Error(e, "Skip broadcasting reorg due to the unexpected exception");
                    }
                }
            );
        
            _blockRenderer.EveryReorgEnd().Subscribe(
                async ev =>
                {
                    try
                    {
                        await client.ReportReorgEndAsync(
                            ev.OldTip.Serialize(),
                            ev.NewTip.Serialize(),
                            ev.Branchpoint.Serialize()
                        );
                    }
                    catch (Exception e)
                    {
                        // FIXME add logger as property
                        Log.Error(e, "Skip broadcasting reorg end due to the unexpected exception");
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

        private bool ContainsAddressToBroadcast(ActionBase.ActionEvaluation<ActionBase> ev,
            ImmutableHashSet<Address> immutableHashSet)
        {
            var updatedAddresses =
                ev.OutputStates.UpdatedAddresses.Union(ev.OutputStates.UpdatedFungibleAssets.Keys);
            return immutableHashSet.Any(address =>
                ev.Signer.Equals(address) || updatedAddresses.Contains(address));
        }

        public void UpdateSubscribeAddresses(byte[] addressBytes, IEnumerable<byte[]> addressesBytes)
        {
            var address = new Address(addressBytes);
            var client = _clients[address].hub;
            var addresses = addressesBytes.Select(a => new Address(a)).ToImmutableHashSet();
            _actionRenderer.EveryRender<ActionBase>()
                .Where(ev => ContainsAddressToBroadcast(ev, addresses))
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
                            await client.BroadcastRenderAsync(c.ToArray());
                        }
                        catch (SerializationException se)
                        {
                            // FIXME add logger as property
                            Log.Error(se, "Skip broadcasting render since the given action isn't serializable.");
                        }
                        catch (Exception e)
                        {
                            // FIXME add logger as property
                            Log.Error(e, "Skip broadcasting render due to the unexpected exception");
                        }
                    }
                );

            _actionRenderer.EveryUnrender<ActionBase>()
                .Where(ev => ContainsAddressToBroadcast(ev, addresses))
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
    }
}
