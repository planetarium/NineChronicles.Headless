using Grpc.Core;
using MagicOnion.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NineChronicles.Headless.Properties;
using System.Net;

namespace NineChronicles.Headless
{
    public static class HostBuilderExtensions
    {
        public static IHostBuilder UseNineChroniclesRPC(
            this IHostBuilder builder, 
            RpcNodeServiceProperties properties
        )
        {
            var context = new RpcContext();
            return builder
                .UseMagicOnion(
                    new ServerPort(properties.RpcListenHost, properties.RpcListenPort, ServerCredentials.Insecure)
                )
                .ConfigureServices(services =>
                {
                    services.AddSingleton(_ => context);
                    services.AddHostedService(provider =>
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
                });
        }
    }
}
