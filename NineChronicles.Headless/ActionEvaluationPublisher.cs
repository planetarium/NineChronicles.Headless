using System;
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
        private IActionEvaluationHub? _client;
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

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(1000, stoppingToken);
            _client = StreamingHubClient.Connect<IActionEvaluationHub, IActionEvaluationHubReceiver>(
                new Channel(_host, _port, ChannelCredentials.Insecure),
                null!
            );
            await _client.JoinAsync();

            _blockRenderer.EveryBlock().Subscribe(
                async pair =>
                {
                    try
                    {
                        await _client.BroadcastRenderBlockAsync(
                            pair.OldTip.Header.Serialize(),
                            pair.NewTip.Header.Serialize()
                        );
                    }
                    catch (Exception e)
                    {
                        // FIXME add logger as property
                        Log.Error(e, "Skip broadcasting blcok render due to the unexpected exception");
                    }

                },
                stoppingToken
            );

            _blockRenderer.EveryReorg().Subscribe(
                async ev =>
                {
                    try
                    {
                        await _client.ReportReorgAsync(
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
                },
                stoppingToken
            );

            _blockRenderer.EveryReorgEnd().Subscribe(
                async ev =>
                {
                    try
                    {
                        await _client.ReportReorgEndAsync(
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
                },
                stoppingToken
            );

            _actionRenderer.EveryRender<ActionBase>()
                .Where(ContainsAddressToBroadcast)
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
                        await _client.BroadcastRenderAsync(c.ToArray());
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
                },
                stoppingToken
            );

            _actionRenderer.EveryUnrender<ActionBase>()
                .Where(ContainsAddressToBroadcast)
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
                        await _client.BroadcastUnrenderAsync(c.ToArray());
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
                },
                stoppingToken
            );
            
            _exceptionRenderer.EveryException().Subscribe(
                async tuple =>
                {
                    try
                    {
                        (RPC.Shared.Exceptions.RPCException code, string message) = tuple;
                        await _client.ReportExceptionAsync((int)code, message);
                    }
                    catch (Exception e)
                    {
                        // FIXME add logger as property
                        Log.Error(e, "Skip broadcasting exception due to the unexpected exception");
                    }
                },
                stoppingToken
            );
            
            _nodeStatusRenderer.EveryChangedStatus().Subscribe(
                async isPreloadStarted =>
                {
                    try
                    {
                        if (isPreloadStarted)
                        {
                            await _client.PreloadStartAsync();
                        }
                        else
                        {
                            await _client.PreloadEndAsync();
                        }
                    }
                    catch (Exception e)
                    {
                        // FIXME add logger as property
                        Log.Error(e, "Skip broadcasting status change due to the unexpected exception");
                    }
                },
                stoppingToken
            );
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            if (!(_client is null))
            {
                await _client.DisposeAsync();
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
    }
}
