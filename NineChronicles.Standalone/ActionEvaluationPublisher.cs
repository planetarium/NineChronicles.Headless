using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Lib9c.Renderer;
using Libplanet;
using MagicOnion.Client;
using Microsoft.Extensions.Hosting;
using Nekoyume;
using Nekoyume.Action;
using Nekoyume.Shared.Hubs;
using Serilog;
using NineChroniclesActionType = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Standalone
{
    public class ActionEvaluationPublisher : BackgroundService
    {
        private readonly string _host;
        private readonly int _port;
        private readonly BlockRenderer _blockRenderer;
        private readonly ActionRenderer _actionRenderer;
        private readonly ExceptionRenderer _exceptionRenderer;
        private readonly NodeStatusRenderer _nodeStatusRenderer;
        private IActionEvaluationHub _client;
        private readonly List<Address> _addressesToSubscribe;

        public ActionEvaluationPublisher(
            BlockRenderer blockRenderer,
            ActionRenderer actionRenderer,
            ExceptionRenderer exceptionRenderer,
            NodeStatusRenderer nodeStatusRenderer,
            string host,
            int port
        )
        {
            _blockRenderer = blockRenderer;
            _actionRenderer = actionRenderer;
            _exceptionRenderer = exceptionRenderer;
            _nodeStatusRenderer = nodeStatusRenderer;
            _host = host;
            _port = port;
            // FIXME: _addressesToSubscribe must contains an address of current AgentState
            _addressesToSubscribe = Addresses.GetAll();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await Task.Delay(1000, stoppingToken);
            _client = StreamingHubClient.Connect<IActionEvaluationHub, IActionEvaluationHubReceiver>(
                new Channel(_host, _port, ChannelCredentials.Insecure),
                null
            );
            await _client.JoinAsync();

            _blockRenderer.EveryBlock().Subscribe(
                async pair =>
                    await _client.BroadcastRenderBlockAsync(
                        pair.OldTip.Header.Serialize(),
                        pair.NewTip.Header.Serialize()
                    ),
                stoppingToken
            );

            _blockRenderer.EveryReorg().Subscribe(
                async ev =>
                    await _client.ReportReorgAsync(
                        ev.OldTip.Serialize(),
                        ev.NewTip.Serialize(),
                        ev.Branchpoint.Serialize()
                    ),
                stoppingToken
            );

            _blockRenderer.EveryReorgEnd().Subscribe(
                async ev =>
                    await _client.ReportReorgEndAsync(
                        ev.OldTip.Serialize(),
                        ev.NewTip.Serialize(),
                        ev.Branchpoint.Serialize()
                    ),
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
                    var (code, message) = tuple;
                    await _client.ReportExceptionAsync((int)code, message);
                },
                stoppingToken
            );
            
            _nodeStatusRenderer.EveryChangedStatus().Subscribe(
                async isPreloadStarted =>
                {
                    if (isPreloadStarted)
                    {
                        await _client.PreloadStartAsync();
                    }
                    else
                    {
                        await _client.PreloadEndAsync();
                    }
                },
                stoppingToken
            );
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await _client?.DisposeAsync();
            await base.StopAsync(cancellationToken);
        }

        private bool ContainsAddressToBroadcast(ActionBase.ActionEvaluation<ActionBase> ev)
        {
            var updatedAddresses = ev.OutputStates.UpdatedAddresses;
            for (var i = _addressesToSubscribe.Count - 1; i >= 0; i--)
            {
                var address = _addressesToSubscribe[i];
                if (updatedAddresses.Contains(address))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
