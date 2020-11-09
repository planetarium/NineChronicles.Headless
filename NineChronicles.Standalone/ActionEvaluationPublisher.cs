using System;
using System.IO;
using System.IO.Compression;
using System.Reactive;
using System.Reactive.Linq;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Threading;
using System.Threading.Tasks;
using Grpc.Core;
using Lib9c.Renderer;
using Libplanet.Blockchain;
using MagicOnion.Client;
using Microsoft.Extensions.Hosting;
using Nekoyume.Action;
using Nekoyume.Shared.Hubs;
using NineChronicles.RPC.Shared.Exceptions;
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
        private IActionEvaluationHub _client;

        public ActionEvaluationPublisher(
            BlockRenderer blockRenderer,
            ActionRenderer actionRenderer,
            ExceptionRenderer exceptionRenderer,
            string host,
            int port
        )
        {
            _blockRenderer = blockRenderer;
            _actionRenderer = actionRenderer;
            _exceptionRenderer = exceptionRenderer;
            _host = host;
            _port = port;
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

            _actionRenderer.EveryRender<ActionBase>().Subscribe(
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

            _actionRenderer.EveryUnrender<ActionBase>().Subscribe(
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
        }

        public override async Task StopAsync(CancellationToken cancellationToken)
        {
            await _client?.DisposeAsync();
            await base.StopAsync(cancellationToken);
        }
    }
}
