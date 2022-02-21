using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NineChronicles.Headless.Properties;
using System.Net;
using Grpc.Core;
using Grpc.Net.Client;
using Lib9c.Formatters;
using MagicOnion.Server;
using MessagePack;
using MessagePack.Resolvers;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;

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
                services.AddSingleton(provider => properties.Libplanet);
                services.AddSingleton(provider =>
                {
                    return new ActionEvaluationPublisher(
                        context.NineChroniclesNodeService!.BlockRenderer,
                        context.NineChroniclesNodeService!.ActionRenderer,
                        context.NineChroniclesNodeService!.ExceptionRenderer,
                        context.NineChroniclesNodeService!.NodeStatusRenderer,
                        IPAddress.Loopback.ToString(),
                        0,
                        rpcContext
                    );
                });
            });
        }

        public static IHostBuilder UseNineChroniclesRPC(
            this IHostBuilder builder, 
            RpcNodeServiceProperties properties
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
                    });
                    services.AddMagicOnion();
                    services.AddSingleton(provider =>
                    {
                        StandaloneContext? ctx = provider.GetRequiredService<StandaloneContext>();
                        return new ActionEvaluationPublisher(
                            ctx.NineChroniclesNodeService!.BlockRenderer,
                            ctx.NineChroniclesNodeService!.ActionRenderer,
                            ctx.NineChroniclesNodeService!.ExceptionRenderer,
                            ctx.NineChroniclesNodeService!.NodeStatusRenderer,
                            IPAddress.Loopback.ToString(),
                            properties.RpcListenPort,
                            context
                        );
                    });
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
                        if (properties.HttpOptions is { } httpOptions)
                        {
                            options.ListenAnyIP(httpOptions.Port, listenOptions =>
                            {
                                listenOptions.Protocols = HttpProtocols.Http1;
                            });   
                        }

                        options.ListenAnyIP(properties.RpcListenPort, listenOptions =>
                        {
                            listenOptions.Protocols = HttpProtocols.Http2;
                        });
                    });

                    if (properties.HttpOptions is { })
                    {
                        hostBuilder.Configure(app =>
                        {
                            app.UseRouting();

                            app.UseEndpoints(endpoints =>
                            {
                                var options = new GrpcChannelOptions
                                {
                                    Credentials = ChannelCredentials.Insecure,
                                    MaxReceiveMessageSize = null,
                                };

                                endpoints.MapMagicOnionHttpGateway("_",
                                    app.ApplicationServices.GetService<MagicOnion.Server.MagicOnionServiceDefinition>()
                                        .MethodHandlers, GrpcChannel.ForAddress($"http://{properties.RpcListenHost}:{properties.RpcListenPort}", options));
                                endpoints.MapMagicOnionSwagger("swagger",
                                    app.ApplicationServices.GetService<MagicOnion.Server.MagicOnionServiceDefinition>()
                                        .MethodHandlers, "/_/");

                                endpoints.MapMagicOnionService();
                            });
                        });   
                    }
                });
        }
    }
}
