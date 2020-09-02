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
using Libplanet.Blockchain;
using MagicOnion.Client;
using Microsoft.Extensions.Hosting;
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
        private IActionEvaluationHub _client;

        public ActionEvaluationPublisher(
            BlockRenderer blockRenderer,
            ActionRenderer actionRenderer,
            string host,
            int port
        )
        {
            _blockRenderer = blockRenderer;
            _actionRenderer = actionRenderer;
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
                async pair => await _client.UpdateTipAsync(pair.NewTip.Index),
                stoppingToken
            );

            _blockRenderer.EveryReorg().Subscribe(
                async ev =>
                    await _client.ReportReorgAsync(
                        ev.Branchpoint.Hash.ToByteArray(),
                        ev.OldTip.Hash.ToByteArray(),
                        ev.NewTip.Hash.ToByteArray()
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
                        await _client.BroadcastAsync(c.ToArray());
                    }
                    catch (SerializationException se)
                    {
                        // FIXME add logger as property
                        Log.Error(se, "Skip broadcasting since given action isn't serializable.");
                    }
                    catch (Exception e)
                    {
                        // FIXME add logger as property
                        Log.Error(e, "Skip broadcasting due to unexpected exception");
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
    }
}
