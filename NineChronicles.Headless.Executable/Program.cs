using Cocona;
using Cocona.Lite;
using Destructurama;
using Libplanet;
using Libplanet.Crypto;
using Libplanet.Extensions.Cocona.Commands;
using Libplanet.KeyStore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.IO;
using NineChronicles.Headless.Properties;
using Org.BouncyCastle.Security;
using Sentry;
using Serilog;
using Serilog.Formatting.Compact;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Libplanet.Action;
// import necessary for sentry exception filters
using Libplanet.Blocks;
using Libplanet.Headless;
using Libplanet.Net.Transports;
using Nekoyume.Action;

namespace NineChronicles.Headless.Executable
{
    [HasSubCommands(typeof(AccountCommand), "account")]
    [HasSubCommands(typeof(ValidationCommand), "validation")]
    [HasSubCommands(typeof(ChainCommand), "chain")]
    [HasSubCommands(typeof(NineChronicles.Headless.Executable.Commands.KeyCommand), "key")]
    [HasSubCommands(typeof(ApvCommand), "apv")]
    [HasSubCommands(typeof(ActionCommand), "action")]
    [HasSubCommands(typeof(StateCommand), "state")]
    [HasSubCommands(typeof(NineChronicles.Headless.Executable.Commands.TxCommand), "tx")]
    [HasSubCommands(typeof(MarketCommand), "market")]
    [HasSubCommands(typeof(GenesisCommand), "genesis")]
    [HasSubCommands(typeof(ReplayCommand), "replay")]
    public class Program : CoconaLiteConsoleAppBase
    {
        static async Task Main(string[] args)
        {
            // https://docs.microsoft.com/ko-kr/aspnet/core/grpc/troubleshoot?view=aspnetcore-6.0#call-insecure-grpc-services-with-net-core-client
            AppContext.SetSwitch("System.Net.Http.SocketsHttpHandler.Http2UnencryptedSupport", true);
            await CoconaLiteApp.CreateHostBuilder()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IConsole, StandardConsole>();
                    services.AddSingleton<IKeyStore>(Web3KeyStore.DefaultKeyStore);
                })
                .RunAsync<Program>(args);
        }

        static void ConfigureSentryOptions(SentryOptions o)
        {
        }

        [PrimaryCommand]
        public async Task Run(
            [Option("app-protocol-version", new[] { 'V' },
                Description = "App protocol version token.")]
            string? appProtocolVersionToken = null,
            [Option('G',
                Description = "Genesis block path of blockchain. Blockchain is recognized by its genesis block.")]
            string? genesisBlockPath = null,
            [Option('H',
                Description = "Hostname of this node for another nodes to access. " +
                              "This is not listening host like 0.0.0.0")]
            string? host = null,
            [Option('P',
                Description = "Port of this node for another nodes to access.")]
            ushort? port = null,
            [Option("swarm-private-key",
                Description = "The private key used for signing messages and to specify your node. " +
                              "If you leave this null, a randomly generated value will be used.")]
            string? swarmPrivateKeyString = null,
            [Option(Description = "Disable block mining.")]
            bool? noMiner = null,
            [Option("miner-count", Description = "The number of miner task(thread).")]
            int? minerCount = null,
            [Option("miner-private-key",
                Description = "The private key used for mining blocks. " +
                              "Must not be null if you want to turn on mining with libplanet-node.")]
            string? minerPrivateKeyString = null,
            [Option("miner.block-interval",
                Description = "The miner's break time after mining a block. The unit is millisecond.")]
            int? minerBlockIntervalMilliseconds = null,
            [Option(Description = "The type of storage to store blockchain data. " +
                                  "If not provided, \"LiteDB\" will be used as default. " +
                                  "Available type: [\"rocksdb\", \"memory\"]")]
            string? storeType = null,
            [Option(Description = "Path of storage. " +
                                  "This value is required if you use persistent storage e.g. \"rocksdb\"")]
            string? storePath = null,
            [Option(Description = "Do not reduce storage. Enabling this option will use enormous disk spaces.")]
            bool? noReduceStore = null,
            [Option("ice-server", new[] { 'I', },
                Description = "ICE server to NAT traverse.")]
            string[]? iceServerStrings = null,
            [Option("peer", Description = "Seed peer list to communicate to another nodes.")]
            string[]? peerStrings = null,
            [Option("trusted-app-protocol-version-signer", new[] { 'T' },
                Description = "Trustworthy signers who claim new app protocol versions")]
            string[]? trustedAppProtocolVersionSigners = null,
            [Option(Description =
                "Use this option if you want to make unity clients to communicate with this server with RPC")]
            bool? rpcServer = null,
            [Option(Description = "RPC listen host")]
            string? rpcListenHost = null,
            [Option(Description = "RPC listen port")]
            int? rpcListenPort = null,
            [Option(Description = "Do a role as RPC remote server?" +
                                  " If you enable this option, multiple Unity clients can connect to your RPC server.")]
            bool? rpcRemoteServer = null,
            [Option(Description = "If you enable this option with \"rpcRemoteServer\" option at the same time, " +
                                  "RPC server will use HTTP/1, not gRPC.")]
            bool? rpcHttpServer = null,
            [Option("graphql-server",
                Description = "Use this option if you want to enable GraphQL server to enable querying data.")]
            bool? graphQLServer = null,
            [Option("graphql-host", Description = "GraphQL listen host")]
            string? graphQLHost = null,
            [Option("graphql-port", Description = "GraphQL listen port")]
            int? graphQLPort = null,
            [Option("graphql-secret-token-path",
                Description = "The path to write GraphQL secret token. " +
                              "If you want to protect this headless application, " +
                              "you should use this option and take it into headers.")]
            string? graphQLSecretTokenPath = null,
            [Option(Description = "Run without CORS policy.")]
            bool? noCors = null,
            [Option("confirmations",
                Description = "The number of required confirmations to recognize a block."
            )]
            int? confirmations = null,
            [Option("nonblock-renderer",
                Description = "Uses non-blocking renderer, which prevents the blockchain & " +
                              "swarm from waiting slow rendering. Turned off by default.")]
            bool? nonblockRenderer = null,
            [Option("nonblock-renderer-queue",
                Description = "The size of the queue used by the non-blocking renderer. " +
                              "Ignored if --nonblock-renderer is turned off.")]
            int? nonblockRendererQueue = null,
            [Option("strict-rendering", Description = "Flag to turn on validating action renderer.")]
            bool? strictRendering = null,
            [Option(Description = "Log action renders besides block renders. --rpc-server implies this.")]
            bool? logActionRenders = null,
            [Option("network-type", Description = "Network type.")]
            NetworkType? networkType = null,
            [Option(Description =
                "The lifetime of each transaction, which uses minute as its unit.")]
            int? txLifeTime = null,
            [Option(Description =
                "The grace period for new messages, which uses second as its unit.")]
            int? messageTimeout = null,
            [Option(Description = "The grace period for tip update, which uses second as its unit.")]
            int? tipTimeout = null,
            [Option(Description = "A number of block size that determines how far behind the demand " +
                                  "the tip of the chain will publish `NodeException` to GraphQL subscriptions.")]
            int? demandBuffer = null,
            [Option("static-peer",
                Description = "A list of peers that the node will continue to maintain.")]
            string[]? staticPeerStrings = null,
            [Option(Description = "Run node without preloading.")]
            bool? skipPreload = null,
            [Option(Description = "Minimum number of peers to broadcast message.")]
            int? minimumBroadcastTarget = null,
            [Option(Description = "Number of the peers can be stored in each bucket.")]
            int? bucketSize = null,
            [Option(Description = "Determines behavior when the chain's tip is stale. \"reboot\" and \"preload\" " +
                                  "is available and \"reboot\" option is selected by default.")]
            string? chainTipStaleBehaviorType = null,
            [Option(Description = "The number of maximum transactions can be included in stage per signer.")]
            int? txQuotaPerSigner = null,
            [Option(Description = "The maximum number of peers to poll blocks. int.MaxValue by default.")]
            int? maximumPollPeers = null,
            [Option("config", new[] { 'C' },
                Description = "Absolute path of \"appsettings.json\" file to provide headless configurations.")]
            string? configPath = "appsettings.json",
            [Option(Description = "Sentry DSN")]
            string? sentryDsn = "",
            [Option(Description = "Trace sample rate for sentry")]
            double? sentryTraceSampleRate = null,
            [Ignore] CancellationToken? cancellationToken = null
        )
        {
#if SENTRY || ! DEBUG
            try
            {
#endif
            var configurationBuilder = new ConfigurationBuilder();
            if (Uri.IsWellFormedUriString(configPath, UriKind.Absolute))
            {
                HttpClient client = new HttpClient();
                HttpResponseMessage resp = await client.GetAsync(configPath);
                resp.EnsureSuccessStatusCode();
                Stream body = await resp.Content.ReadAsStreamAsync();
                configurationBuilder.AddJsonStream(body)
                    .AddEnvironmentVariables();
            }
            else
            {
                configurationBuilder.AddJsonFile(configPath)
                    .AddEnvironmentVariables();
            }

            // Setup logger.
            var configuration = configurationBuilder.Build();
            var loggerConf = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .WriteTo.File(
                    new RenderedCompactJsonFormatter(),
                    path: Environment.GetEnvironmentVariable("JSON_LOG_PATH") ?? "remote-headless_9c-network_remote-headless.json",
                    retainedFileCountLimit: 5,
                    rollOnFileSizeLimit: true,
                    fileSizeLimitBytes: 524_288_000)
                .Destructure.UsingAttributes();
            var headlessConfig = new Configuration();
            configuration.Bind("Headless", headlessConfig);
            headlessConfig.Overwrite(
                appProtocolVersionToken, trustedAppProtocolVersionSigners, genesisBlockPath, host, port,
                swarmPrivateKeyString, storeType, storePath, noReduceStore, noMiner, minerCount,
                minerPrivateKeyString, minerBlockIntervalMilliseconds, networkType, iceServerStrings, peerStrings, rpcServer, rpcListenHost,
                rpcListenPort, rpcRemoteServer, rpcHttpServer, graphQLServer, graphQLHost, graphQLPort,
                graphQLSecretTokenPath, noCors, nonblockRenderer, nonblockRendererQueue, strictRendering,
                logActionRenders, confirmations,
                txLifeTime, messageTimeout, tipTimeout, demandBuffer, staticPeerStrings, skipPreload,
                minimumBroadcastTarget, bucketSize, chainTipStaleBehaviorType, txQuotaPerSigner, maximumPollPeers,
                sentryDsn, sentryTraceSampleRate
            );

#if SENTRY || ! DEBUG
            loggerConf = loggerConf
                .WriteTo.Sentry(o =>
                {
                    o.InitializeSdk = false;
                });

            using var _ = SentrySdk.Init(o =>
            {
                o.SendDefaultPii = true;
                o.Dsn = headlessConfig.SentryDsn;
                // TODO: We need to specify `o.Release` after deciding the version scheme.
                // https://docs.sentry.io/workflow/releases/?platform=csharp
                //o.Debug = true;
                o.Release = Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                    ?.InformationalVersion ?? "Unknown";
                o.SampleRate = 0.01f;
                o.TracesSampleRate = headlessConfig.SentryTraceSampleRate;
                o.AddExceptionFilterForType<TimeoutException>();
                o.AddExceptionFilterForType<IOException>();
                o.AddExceptionFilterForType<CommunicationFailException>();
                o.AddExceptionFilterForType<InvalidBlockIndexException>();
            });

            // Set global tag
            SentrySdk.ConfigureScope(scope =>
            {
                scope.SetTag("host", headlessConfig.Host ?? "no-host");
            });
#endif

            // Clean-up previous temporary log files.
            if (Directory.Exists("_logs"))
            {
                Directory.Delete("_logs", true);
            }

            Log.Logger = loggerConf.CreateLogger();

            if (!headlessConfig.NoMiner && headlessConfig.MinerPrivateKeyString is null)
            {
                throw new CommandExitedException(
                    "--miner-private-key must be present to turn on mining at libplanet node.",
                    -1
                );
            }

            try
            {
                IHostBuilder hostBuilder = Host.CreateDefaultBuilder();

                var standaloneContext = new StandaloneContext
                {
                    KeyStore = Web3KeyStore.DefaultKeyStore,
                };

                if (headlessConfig.GraphQLServer)
                {
                    string? secretToken = null;
                    if (headlessConfig.GraphQLSecretTokenPath is { })
                    {
                        var buffer = new byte[40];
                        new SecureRandom().NextBytes(buffer);
                        secretToken = Convert.ToBase64String(buffer);
                        await File.WriteAllTextAsync(headlessConfig.GraphQLSecretTokenPath, secretToken);
                    }

                    var graphQLNodeServiceProperties = new GraphQLNodeServiceProperties
                    {
                        GraphQLServer = headlessConfig.GraphQLServer,
                        GraphQLListenHost = headlessConfig.GraphQLHost,
                        GraphQLListenPort = headlessConfig.GraphQLPort,
                        SecretToken = secretToken,
                        NoCors = headlessConfig.NoCors,
                        UseMagicOnion = headlessConfig.RpcServer,
                        HttpOptions = headlessConfig.RpcServer && headlessConfig.RpcHttpServer == true
                            ? new GraphQLNodeServiceProperties.MagicOnionHttpOptions(
                                $"{headlessConfig.RpcListenHost}:{headlessConfig.RpcListenPort}")
                            : (GraphQLNodeServiceProperties.MagicOnionHttpOptions?)null,
                    };

                    var graphQLService = new GraphQLService(graphQLNodeServiceProperties);
                    hostBuilder = graphQLService.Configure(hostBuilder);
                }

                var properties = NineChroniclesNodeServiceProperties
                    .GenerateLibplanetNodeServiceProperties(
                        headlessConfig.AppProtocolVersionString,
                        headlessConfig.GenesisBlockPath,
                        headlessConfig.Host,
                        headlessConfig.Port,
                        headlessConfig.SwarmPrivateKeyString,
                        headlessConfig.StoreType,
                        headlessConfig.StorePath,
                        headlessConfig.NoReduceStore,
                        headlessConfig.StoreStateCacheSize,
                        headlessConfig.IceServerStrings,
                        headlessConfig.PeerStrings,
                        headlessConfig.TrustedAppProtocolVersionSignerStrings,
                        headlessConfig.NoMiner,
                        confirmations: headlessConfig.Confirmations,
                        nonblockRenderer: headlessConfig.NonblockRenderer,
                        nonblockRendererQueue: headlessConfig.NonblockRendererQueue,
                        messageTimeout: headlessConfig.MessageTimeout,
                        tipTimeout: headlessConfig.TipTimeout,
                        demandBuffer: headlessConfig.DemandBuffer,
                        staticPeerStrings: headlessConfig.StaticPeerStrings,
                        preload: !headlessConfig.SkipPreload,
                        minimumBroadcastTarget: headlessConfig.MinimumBroadcastTarget,
                        bucketSize: headlessConfig.BucketSize,
                        chainTipStaleBehaviorType: headlessConfig.ChainTipStaleBehaviorType,
                        maximumPollPeers: headlessConfig.MaximumPollPeers
                    );

                if (headlessConfig.RpcServer)
                {
                    properties.Render = true;
                    properties.LogActionRenders = true;
                }

                if (headlessConfig.LogActionRenders == true)
                {
                    properties.LogActionRenders = true;
                }

                IActionTypeLoader MakeStaticActionTypeLoader() => new StaticActionTypeLoader(
                    Assembly.GetEntryAssembly() is { } entryAssembly
                        ? new[] { typeof(ActionBase).Assembly, entryAssembly }
                        : new[] { typeof(ActionBase).Assembly },
                    typeof(ActionBase)
                );

                IActionTypeLoader actionTypeLoader;
                if (headlessConfig.ActionTypeLoader is { } actionTypeLoaderConfiguration)
                {
                    if (actionTypeLoaderConfiguration.DynamicActionTypeLoader is { } dynamicActionTypeLoaderConf)
                    {
                        actionTypeLoader = new DynamicActionTypeLoader(
                            dynamicActionTypeLoaderConf.BasePath,
                            dynamicActionTypeLoaderConf.AssemblyFileName,
                            dynamicActionTypeLoaderConf.HardForks.OrderBy(pair => pair.SinceBlockIndex));
                    }
                    else if (actionTypeLoaderConfiguration.StaticActionTypeLoader is { } staticActionTypeLoaderConf)
                    {
                        var assemblies = staticActionTypeLoaderConf.Assemblies?.Select(x => Assembly.Load(File.ReadAllBytes(x))).ToHashSet()
                            ?? throw new CommandExitedException(-1);
                        actionTypeLoader = new StaticActionTypeLoader(assemblies);
                    }
                    else
                    {
                        actionTypeLoader = MakeStaticActionTypeLoader();
                    }
                }
                else
                {
                    actionTypeLoader = MakeStaticActionTypeLoader();
                }

                var minerPrivateKey = string.IsNullOrEmpty(headlessConfig.MinerPrivateKeyString)
                    ? null
                    : new PrivateKey(ByteUtil.ParseHex(headlessConfig.MinerPrivateKeyString));
                TimeSpan minerBlockInterval = TimeSpan.FromMilliseconds(headlessConfig.MinerBlockIntervalMilliseconds);
                var nineChroniclesProperties = new NineChroniclesNodeServiceProperties(actionTypeLoader)
                {
                    MinerPrivateKey = minerPrivateKey,
                    Libplanet = properties,
                    NetworkType = headlessConfig.NetworkType,
                    StrictRender = headlessConfig.StrictRendering,
                    TxLifeTime = TimeSpan.FromMinutes(headlessConfig.TxLifeTime),
                    MinerCount = headlessConfig.MinerCount,
                    MinerBlockInterval = minerBlockInterval,
                    TxQuotaPerSigner = headlessConfig.TxQuotaPerSigner,
                };
                hostBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton(_ => standaloneContext);
                    services.AddSingleton<ConcurrentDictionary<string, ITransaction>>();
                });
                hostBuilder.UseNineChroniclesNode(nineChroniclesProperties, standaloneContext);
                if (headlessConfig.RpcServer)
                {
                    hostBuilder.UseNineChroniclesRPC(
                        NineChroniclesNodeServiceProperties
                            .GenerateRpcNodeServiceProperties(
                                headlessConfig.RpcListenHost,
                                headlessConfig.RpcListenPort,
                                headlessConfig.RpcRemoteServer == true
                            )
                    );
                }

                await hostBuilder.RunConsoleAsync(cancellationToken ?? Context.CancellationToken);
            }
            catch (TaskCanceledException)
            {
                Log.Information("Terminated by the cancellation.");
            }
            catch (Exception e)
            {
                Log.Error(e, "Unexpected exception occurred during Run. {e}", e);
            }

#if SENTRY || ! DEBUG
            }
            catch (CommandExitedException)
            {
                throw;
            }
            catch (Exception exceptionToCapture)
            {
                SentrySdk.CaptureException(exceptionToCapture);
                throw;
            }
#endif
        }
    }
}
