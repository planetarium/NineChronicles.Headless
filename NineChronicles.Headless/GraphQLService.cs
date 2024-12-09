using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using AspNetCoreRateLimit;
using GraphQL.Server;
using GraphQL.Utilities;
using Grpc.Core;
using Grpc.Net.Client;
using Libplanet.Crypto;
using Libplanet.Explorer.Schemas;
using Libplanet.Store.Remote.Server;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Server.Kestrel.Core;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.Middleware;
using NineChronicles.Headless.Properties;
using NineChronicles.Headless.Repositories.BlockChain;
using NineChronicles.Headless.Repositories.StateTrie;
using NineChronicles.Headless.Repositories.Transaction;
using NineChronicles.Headless.Repositories.WorldState;
using Serilog;

namespace NineChronicles.Headless
{
    public class GraphQLService
    {
        public const string LocalPolicyKey = "LocalPolicy";

        public const string JwtPolicyKey = "JwtPolicy";

        public const string NoCorsPolicyName = "AllowAllOrigins";

        public const string SecretTokenKey = "secret";

        public const string NoCorsKey = "noCors";

        public const string UseMagicOnionKey = "useMagicOnion";

        public const string UseRemoteKeyValueServiceKey = "useRemoteKeyValueService";

        public const string MagicOnionTargetKey = "magicOnionTarget";

        private StandaloneContext StandaloneContext { get; }
        private GraphQLNodeServiceProperties GraphQlNodeServiceProperties { get; }
        private IConfiguration Configuration { get; }
        private ActionEvaluationPublisher Publisher { get; }

        public GraphQLService(
            GraphQLNodeServiceProperties properties,
            StandaloneContext standaloneContext,
            IConfiguration configuration,
            ActionEvaluationPublisher publisher)
        {
            GraphQlNodeServiceProperties = properties;
            StandaloneContext = standaloneContext;
            Configuration = configuration;
            Publisher = publisher;
        }

        public IHostBuilder Configure(IHostBuilder hostBuilder)
        {
            var listenHost = GraphQlNodeServiceProperties.GraphQLListenHost;
            var listenPort = GraphQlNodeServiceProperties.GraphQLListenPort;

            return hostBuilder.ConfigureWebHostDefaults(builder =>
            {
                builder.UseStartup(x => new GraphQLStartup(x.Configuration, StandaloneContext, Publisher));
                builder.ConfigureAppConfiguration(
                    (context, builder) =>
                    {
                        var dictionary = new Dictionary<string, string>();
                        if (GraphQlNodeServiceProperties.SecretToken is { } secretToken)
                        {
                            dictionary[SecretTokenKey] = secretToken;
                        }

                        if (GraphQlNodeServiceProperties.NoCors)
                        {
                            dictionary[NoCorsKey] = string.Empty;
                        }

                        if (GraphQlNodeServiceProperties.UseMagicOnion)
                        {
                            dictionary[UseMagicOnionKey] = string.Empty;
                        }

                        if (GraphQlNodeServiceProperties.UseRemoteKeyValueService)
                        {
                            dictionary[UseRemoteKeyValueServiceKey] = string.Empty;
                        }

                        if (GraphQlNodeServiceProperties.HttpOptions is { } options)
                        {
                            dictionary[MagicOnionTargetKey] = options.Target;
                        }

                        builder.AddInMemoryCollection(dictionary!);
                    })
                    .ConfigureKestrel(options =>
                    {
                        options.ListenAnyIP((int)listenPort!, listenOptions =>
                        {
                            listenOptions.Protocols = HttpProtocols.Http1AndHttp2;
                        });
                    });
            });
        }

        internal class GraphQLStartup
        {
            public GraphQLStartup(
                IConfiguration configuration,
                StandaloneContext standaloneContext,
                ActionEvaluationPublisher publisher)
            {
                Configuration = configuration;
                StandaloneContext = standaloneContext;
                Publisher = publisher;
            }

            public IConfiguration Configuration { get; }
            public StandaloneContext StandaloneContext;
            public ActionEvaluationPublisher Publisher;

            public void ConfigureServices(IServiceCollection services)
            {
                if (Convert.ToBoolean(Configuration.GetSection("IpRateLimiting")["EnableEndpointRateLimiting"]))
                {
                    services.AddOptions();
                    services.AddMemoryCache();
                    services.Configure<CustomIpRateLimitOptions>(Configuration.GetSection("IpRateLimiting"));
                    services.AddInMemoryRateLimiting();
                    services.AddMvc(options => options.EnableEndpointRouting = false);
                    services.AddSingleton<IRateLimitConfiguration, RateLimitConfiguration>();
                }

                if (Convert.ToBoolean(Configuration.GetSection("MultiAccountManaging")["EnableManaging"]))
                {
                    services.Configure<MultiAccountManagerProperties>(Configuration.GetSection("MultiAccountManaging"));
                }

                var jwtOptions = Configuration.GetSection("Jwt");
                if (Convert.ToBoolean(jwtOptions["EnableJwtAuthentication"]))
                {
                    services.Configure<JwtOptions>(jwtOptions);
                    services.AddTransient<JwtAuthenticationMiddleware>();
                }

                if (!(Configuration[NoCorsKey] is null))
                {
                    services.AddCors(
                        options =>
                            options.AddPolicy(
                                NoCorsPolicyName,
                                builder =>
                                    builder.AllowAnyOrigin().AllowAnyMethod().AllowAnyHeader()));
                }

                services.AddTransient<LocalAuthenticationMiddleware>();

                // Repositories
                services.AddSingleton<IWorldStateRepository, WorldStateRepository>();
                services.AddSingleton<IBlockChainRepository, BlockChainRepository>();
                services.AddSingleton<ITransactionRepository, TransactionRepository>();
                services.AddSingleton<IStateTrieRepository, StateTrieRepository>();

                services.AddHealthChecks();

                services.AddControllers();
                services.AddGraphQL(
                        (options, provider) =>
                        {
                            options.EnableMetrics = true;
                            options.UnhandledExceptionDelegate = context =>
                            {
                                Log.Error(context.Exception.ToString());
                                Log.Error(context.ErrorMessage);

                                context.Exception.Data["exception"] = context.Exception.GetType().ToString();
                                context.Exception.Data["message"] = context.Exception.Message;
                                context.Exception.Data["innerException"] = context.Exception.InnerException?.GetType().ToString();
                                context.Exception.Data["stackTrace"] = context.Exception.StackTrace;
                            };
                        })
                    .AddSystemTextJson()
                    .AddWebSockets()
                    .AddDataLoader()
                    .AddGraphTypes(typeof(StandaloneSchema))
                    .AddLibplanetExplorer()
                    .AddGraphQLAuthorization(
                        options =>
                        {
                            options.AddPolicy(
                                LocalPolicyKey,
                                p =>
                                    p.RequireClaim(
                                        "role",
                                        "Admin"));

                            // FIXME: Use ConfigurationException after bumping to .NET 8 or later.
                            if (Convert.ToBoolean(Configuration.GetSection("Jwt")["EnableJwtAuthentication"]))
                            {
                                options.AddPolicy(
                                    JwtPolicyKey,
                                    p =>
                                        p.RequireClaim("iss",
                                            jwtOptions["Issuer"] ?? throw new ArgumentException("jwtOptions[\"Issuer\"] is null.")));
                            }
                        });

                services.AddGraphTypes();
            }

            public void Configure(IApplicationBuilder app, IWebHostEnvironment env)
            {
                if (env.IsDevelopment())
                {
                    app.UseDeveloperExceptionPage();
                }

                // Capture requests
                app.UseMiddleware<HttpCaptureMiddleware>();

                app.UseRouting();
                app.UseAuthorization();
                if (Convert.ToBoolean(Configuration.GetSection("IpRateLimiting")["EnableEndpointRateLimiting"]))
                {
                    app.UseMiddleware<CustomRateLimitMiddleware>();
                    app.UseMiddleware<IpBanMiddleware>();
                    app.UseMvc();
                }

                if (Convert.ToBoolean(Configuration.GetSection("MultiAccountManaging")["EnableManaging"]))
                {
                    ConcurrentDictionary<string, HashSet<Address>> ipSignerList = new();
                    app.UseMiddleware<HttpMultiAccountManagementMiddleware>(
                        StandaloneContext,
                        ipSignerList,
                        Publisher);
                }


                app.UseMiddleware<LocalAuthenticationMiddleware>();
                if (Convert.ToBoolean(Configuration.GetSection("Jwt")["EnableJwtAuthentication"]))
                {
                    app.UseMiddleware<JwtAuthenticationMiddleware>();
                }

                if (Configuration[NoCorsKey] is null)
                {
                    app.UseCors();
                }
                else
                {
                    app.UseCors("AllowAllOrigins");
                }

                app.UseEndpoints(endpoints =>
                {
                    endpoints.MapControllers();

                    if (Configuration[UseRemoteKeyValueServiceKey] is not null)
                    {
                        endpoints.MapGrpcService<RemoteKeyValueService>();
                    }

                    if (!(Configuration[UseMagicOnionKey] is null))
                    {
                        endpoints.MapMagicOnionService();

                        if (Configuration[MagicOnionTargetKey] is { } magicOnionTarget)
                        {
                            var options = new GrpcChannelOptions
                            {
                                Credentials = ChannelCredentials.Insecure,
                                MaxReceiveMessageSize = null,
                            };

                            endpoints.MapMagicOnionHttpGateway("_",
                                app.ApplicationServices.GetService<MagicOnion.Server.MagicOnionServiceDefinition>()!
                                    .MethodHandlers, GrpcChannel.ForAddress($"http://{magicOnionTarget}", options));
                            endpoints.MapMagicOnionSwagger("swagger",
                                app.ApplicationServices.GetService<MagicOnion.Server.MagicOnionServiceDefinition>()!
                                    .MethodHandlers, "/_/");
                        }
                    }

                    endpoints.MapHealthChecks("/health-check");
                });

                // WebSocket으로 운영합니다.
                app.UseWebSockets();
                app.UseGraphQLWebSockets<StandaloneSchema>("/graphql");
                app.UseGraphQL<StandaloneSchema>("/graphql");
                app.UseGraphQL<LibplanetExplorerSchema>("/graphql/explorer");

                // Prints
                app.UseMiddleware<GraphQLSchemaMiddleware<StandaloneSchema>>("/schema.graphql");

                app.UseOpenTelemetryPrometheusScrapingEndpoint();

                // /ui/playground 옵션을 통해서 Playground를 사용할 수 있습니다.
                app.UseGraphQLPlayground();
            }
        }
    }
}
