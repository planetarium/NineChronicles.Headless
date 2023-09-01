using System.Collections.Concurrent;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NineChronicles.Headless.Properties;
using System.Net;
using Lib9c.Formatters;
using Libplanet.Action;
using Libplanet.Headless.Hosting;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Nekoyume.Action;
using NineChronicles.Headless.Middleware;
using NineChronicles.Headless.Services;
using Sentry;

namespace NineChronicles.Headless
{
    public static class HostBuilderExtensions
    {
        public static IHostBuilder UseNineChroniclesNode(
            this IHostBuilder builder,
            NineChroniclesNodeServiceProperties properties,
            StandaloneContext context
        )
        {
            NineChroniclesNodeService service =
                NineChroniclesNodeService.Create(properties, context);
            var rpcContext = new RpcContext
            {
                RpcRemoteSever = false
            };
            return builder.ConfigureServices(services =>
            {
                services.AddHostedService(provider => service);
                services.AddSingleton(provider => service);
                services.AddSingleton(provider => service.Swarm);
                services.AddSingleton(provider => service.BlockChain);
                services.AddSingleton(provider => service.Store);

                if (properties.StateServiceManagerService is { } stateServiceManagerServiceOptions)
                {
                    var stateServiceManagerService = new StateServiceManagerService(stateServiceManagerServiceOptions);
                    services.AddSingleton(provider => stateServiceManagerService);
                }

                if (properties.Libplanet is { } libplanetNodeServiceProperties)
                {
                    services.AddSingleton<LibplanetNodeServiceProperties>(provider => libplanetNodeServiceProperties);
                }
                services.AddSingleton(provider =>
                {
                    return new ActionEvaluationPublisher(
                        context.NineChroniclesNodeService!.BlockRenderer,
                        context.NineChroniclesNodeService!.ActionRenderer,
                        context.NineChroniclesNodeService!.ExceptionRenderer,
                        context.NineChroniclesNodeService!.NodeStatusRenderer,
                        IPAddress.Loopback.ToString(),
                        0,
                        rpcContext,
                        provider.GetRequiredService<ConcurrentDictionary<string, ITransaction>>()
                    );
                });
            });
        }

        public static IHostBuilder UseNineChroniclesRPC(
            this IHostBuilder builder,
            RpcNodeServiceProperties properties,
            ActionEvaluationPublisher actionEvaluationPublisher
        )
        {
            var context = new RpcContext
            {
                RpcRemoteSever = properties.RpcRemoteServer
            };

            return builder
                .ConfigureServices(services =>
                {
                    services.AddSingleton(_ => context);
                    services.AddGrpc(options =>
                    {
                        options.MaxReceiveMessageSize = null;
                        options.Interceptors.Add<GrpcCaptureMiddleware>(actionEvaluationPublisher);
                    });
                    services.AddMagicOnion();
                    services.AddSingleton(_ => actionEvaluationPublisher);
                    var resolver = MessagePack.Resolvers.CompositeResolver.Create(
                        NineChroniclesResolver.Instance,
                        StandardResolver.Instance
                    );
                    var options = MessagePackSerializerOptions.Standard.WithResolver(resolver);
                    MessagePackSerializer.DefaultOptions = options;
                })
                .ConfigureWebHostDefaults(hostBuilder =>
                {
                    hostBuilder.ConfigureKestrel(options =>
                    {
                        options.ListenAnyIP(properties.RpcListenPort, listenOptions =>
                        {
                            listenOptions.Protocols = HttpProtocols.Http2;
                        });
                    });
                });
        }
    }
}
