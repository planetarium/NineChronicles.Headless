using Amazon;
using Amazon.CognitoIdentity;
using Amazon.Runtime;
using Amazon.S3;
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
using System.IO;
using System.Threading.Tasks;

namespace NineChronicles.Headless.Executable
{
    [HasSubCommands(typeof(ValidationCommand), "validation")]
    [HasSubCommands(typeof(ChainCommand), "chain")]
    [HasSubCommands(typeof(NineChronicles.Headless.Executable.Commands.KeyCommand), "key")]
    [HasSubCommands(typeof(ApvCommand), "apv")]
    public class Program : CoconaLiteConsoleAppBase
    {
        const string SentryDsn = "https://ceac97d4a7d34e7b95e4c445b9b5669e@o195672.ingest.sentry.io/5287621";

        static async Task Main(string[] args)
        {
#if SENTRY || ! DEBUG
            using var _ = SentrySdk.Init(ConfigureSentryOptions);
#endif
            await CoconaLiteApp.Create()
                .ConfigureServices(services =>
                {
                    services.AddSingleton<IConsole, StandardConsole>();
                    services.AddSingleton<IKeyStore>(Web3KeyStore.DefaultKeyStore);
                })
                .RunAsync<Program>(args);
        }

        static void ConfigureSentryOptions(SentryOptions o)
        {
            o.SendDefaultPii = true;
            o.Dsn = new Dsn(SentryDsn);
            // TODO: o.Release 설정하면 좋을 것 같은데 빌드 버전 체계가 아직 없어서 어떻게 해야 할 지...
            // https://docs.sentry.io/workflow/releases/?platform=csharp
#if DEBUG
            o.Debug = true;
#endif
        }

        [PrimaryCommand]
        public async Task Run(
            [Option("app-protocol-version", new[] { 'V' }, Description = "App protocol version token")]
            string appProtocolVersionToken,
            [Option('G')]
            string genesisBlockPath,
            bool noMiner = false,
            [Option('H')]
            string? host = null,
            [Option('P')]
            ushort? port = null,
            [Option("swarm-private-key",
                Description = "The private key used for signing messages and to specify your node. " +
                              "If you leave this null, a randomly generated value will be used.")]
            string? swarmPrivateKeyString = null,
            [Option('D')]
            int minimumDifficulty = 5000000,
            [Option("miner-private-key",
                Description = "The private key used for mining blocks. " +
                              "Must not be null if you want to turn on mining with libplanet-node.")]
            string? minerPrivateKeyString = null,
            string? storeType = null,
            string? storePath = null,
            [Option("ice-server", new [] { 'I', })]
            string[]? iceServerStrings = null,
            [Option("peer")]
            string[]? peerStrings = null,
            [Option("trusted-app-protocol-version-signer", new[] { 'T' },
                    Description = "Trustworthy signers who claim new app protocol versions")]
            string[]? trustedAppProtocolVersionSigners = null,
            bool rpcServer = false,
            string rpcListenHost = "0.0.0.0",
            int? rpcListenPort = null,
            [Option("graphql-server")]
            bool graphQLServer = false,
            [Option("graphql-host")]
            string graphQLHost = "0.0.0.0",
            [Option("graphql-port")]
            int? graphQLPort = null,
            [Option("graphql-secret-token-path", Description = "The path to write GraphQL secret token. " +
                                                               "If you want to protect this headless application, " +
                                                               "you should use this option and take it into headers.")]
            string? graphQLSecretTokenPath = null,
            [Option(Description = "Run without CORS policy.")]
            bool noCors = false,
            [Option("workers", Description = "Number of workers to use in Swarm")]
            int workers = 5,
            [Option(
                "confirmations",
                Description =
                    "The number of required confirmations to recognize a block.  0 by default."
            )]
            int confirmations = 0,
            [Option(
                "max-transactions",
                Description =
                    "The number of maximum transactions can be included in a single block. " +
                    "Unlimited if the value is less then or equal to 0.  100 by default."
            )]
            int maximumTransactions = 100,
            [Option("strict-rendering", Description = "Flag to turn on validating action renderer.")]
            bool strictRendering = false,
            [Option("dev", Description = "Flag to turn on the dev mode.  false by default.")]
            bool isDev = false,
            [Option(
                "dev.block-interval",
                Description =
                    "The time interval between blocks. It's unit is milliseconds. Works only when dev mode is on.  10000 (ms) by default.")]
            int blockInterval = 10000,
            [Option(
                "dev.reorg-interval",
                Description =
                    "The size of reorg interval. Works only when dev mode is on.  0 by default.")]
            int reorgInterval = 0,
            [Option(Description = "Log action renders besides block renders.  --rpc-server implies this.")]
            bool logActionRenders = false,
            [Option(Description = "The Cognito identity for AWS CloudWatch logging.")]
            string? awsCognitoIdentity = null,
            [Option(Description = "The access key for AWS CloudWatch logging.")]
            string? awsAccessKey = null,
            [Option(Description = "The secret key for AWS CloudWatch logging.")]
            string? awsSecretKey = null,
            [Option(Description = "The AWS region for AWS CloudWatch (e.g., us-east-1, ap-northeast-2).")]
            string? awsRegion = null,
            [Option(Description = "Run as an authorized miner, which mines only blocks that should be authorized.")]
            bool authorizedMiner = false,
            [Option(Description = "The lifetime of each transaction, which uses minute as its unit.  60 (m) by default.")]
            int txLifeTime = 60,
            [Option(Description = "The grace period for new messages, which uses second as its unit.  60 (s) by default.")]
            int messageTimeout = 60,
            [Option(Description = "The grace period for tip update, which uses second as its unit.  60 (s) by default.")]
            int tipTimeout = 60,
            [Option(Description =
                "A number that determines how far behind the demand the tip of the chain " +
                "will publish `NodeException` to GraphQL subscriptions.  1150 blocks by default.")]
            int demandBuffer = 1150,
            [Option("static-peer",
                Description = "A list of peers that the node will continue to maintain.")]
            string[]? staticPeerStrings = null,
            [Option("miner-count", Description = "The number of miner task(thread).")]
            int minerCount = 1,
            [Option(Description ="Run node without preloading.")]
            bool skipPreload = false,
            [Option(Description = "Minimum number of peers to broadcast message.  10 by default.")]
            int minimumBroadcastTarget = 10,
            [Option(Description =
                "Number of the peers can be stored in each bucket.  16 by default.")]
            int bucketSize = 16,
            [Option(Description =
                "Determines behavior when the chain's tip is stale. \"reboot\" and \"preload\" " +
                "is available and \"reboot\" option is selected by default.")]
            string chainTipStaleBehaviorType = "reboot",
            [Option(Description = "The interval between block polling.  15 seconds by default.")]
            int pollInterval = 15,
            [Option(Description = "The maximum number of peers to poll blocks.  int.MaxValue by default.")]
            int maximumPollPeers = int.MaxValue
        )
        {
#if SENTRY || ! DEBUG
            try
            {
#endif
            
            // Setup logger.
            var configurationBuilder = new ConfigurationBuilder().AddJsonFile("appsettings.json");
            var configuration = configurationBuilder.Build();
            var loggerConf = new LoggerConfiguration()
                .ReadFrom.Configuration(configuration)
                .Destructure.UsingAttributes();
#if SENTRY || !DEBUG
            loggerConf = loggerConf
                .WriteTo.Sentry(o =>
                {
                    o.InitializeSdk = false;
                });
#endif
            bool useBasicAwsCredentials = !(awsAccessKey is null) && !(awsSecretKey is null);
            bool useCognitoCredentials = !(awsCognitoIdentity is null);
            if (useBasicAwsCredentials && useCognitoCredentials)
            {
                const string message =
                    "You must choose to use only one credential between basic credential " +
                    "(i.e., --aws-access-key, --aws-secret-key) and " +
                    "Cognito credential (i.e., --aws-cognito-identity).";
                throw new CommandExitedException(message, -1);
            }

            // Clean-up previous temporary log files.
            if (Directory.Exists("_logs"))
            {
                Directory.Delete("_logs", true);
            }

            if (useBasicAwsCredentials ^ useCognitoCredentials  && !(awsRegion is null))
            {
                RegionEndpoint regionEndpoint = RegionEndpoint.GetBySystemName(awsRegion);
                AWSCredentials credentials = useCognitoCredentials
                    ? (AWSCredentials)new CognitoAWSCredentials(awsCognitoIdentity, regionEndpoint)
                    : (AWSCredentials)new BasicAWSCredentials(awsAccessKey, awsSecretKey);

                var guid = LoadAWSSinkGuid();
                if (guid is null)
                {
                    guid = Guid.NewGuid();
                    StoreAWSSinkGuid(guid.Value);   
                }

                loggerConf = loggerConf.WriteTo.AmazonS3(
                    new AmazonS3Client(credentials, regionEndpoint),
                    "_logs/log.json",
                    "9c-headless-logs",
                    formatter: new CompactJsonFormatter(),
                    rollingInterval: Serilog.Sinks.AmazonS3.RollingInterval.Hour,
                    batchingPeriod: TimeSpan.FromMinutes(10),
                    batchSizeLimit: 10000,
                    bucketPath: guid.ToString()
                );
            }

            Log.Logger = loggerConf.CreateLogger();

            if (!noMiner && minerPrivateKeyString is null)
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

                if (graphQLServer)
                {
                    string? secretToken = null;
                    if (graphQLSecretTokenPath is { })
                    {
                        var buffer = new byte[40];
                        new SecureRandom().NextBytes(buffer);
                        secretToken = Convert.ToBase64String(buffer);
                        await File.WriteAllTextAsync(graphQLSecretTokenPath, secretToken);
                    }
                    var graphQLNodeServiceProperties = new GraphQLNodeServiceProperties
                    {
                        GraphQLServer = graphQLServer,
                        GraphQLListenHost = graphQLHost,
                        GraphQLListenPort = graphQLPort,
                        SecretToken = secretToken,
                        NoCors = noCors,
                    };

                    var graphQLService = new GraphQLService(graphQLNodeServiceProperties);
                    hostBuilder = graphQLService.Configure(hostBuilder);
                }

                var properties = NineChroniclesNodeServiceProperties
                    .GenerateLibplanetNodeServiceProperties(
                        appProtocolVersionToken,
                        genesisBlockPath,
                        host,
                        port,
                        swarmPrivateKeyString,
                        minimumDifficulty,
                        storeType,
                        storePath,
                        100,
                        iceServerStrings,
                        peerStrings,
                        trustedAppProtocolVersionSigners,
                        noMiner,
                        workers: workers,
                        confirmations: confirmations,
                        maximumTransactions: maximumTransactions,
                        messageTimeout: messageTimeout,
                        tipTimeout: tipTimeout,
                        demandBuffer: demandBuffer,
                        staticPeerStrings: staticPeerStrings,
                        preload: !skipPreload,
                        minimumBroadcastTarget: minimumBroadcastTarget,
                        bucketSize: bucketSize,
                        chainTipStaleBehaviorType: chainTipStaleBehaviorType,
                        pollInterval: pollInterval,
                        maximumPollPeers: maximumPollPeers
                    );

                if (rpcServer)
                {
                    properties.Render = true;
                    properties.LogActionRenders = true;
                }

                if (logActionRenders)
                {
                    properties.LogActionRenders = true;
                }

                var minerPrivateKey = string.IsNullOrEmpty(minerPrivateKeyString)
                    ? null
                    : new PrivateKey(ByteUtil.ParseHex(minerPrivateKeyString));
                var nineChroniclesProperties = new NineChroniclesNodeServiceProperties()
                {
                    MinerPrivateKey = minerPrivateKey,
                    Libplanet = properties,
                    Dev = isDev,
                    StrictRender = strictRendering,
                    BlockInterval = blockInterval,
                    ReorgInterval = reorgInterval,
                    AuthorizedMiner = authorizedMiner,
                    TxLifeTime = TimeSpan.FromMinutes(txLifeTime),
                    MinerCount = minerCount,
                };
                hostBuilder.ConfigureServices(services =>
                {
                    services.AddSingleton(_ => standaloneContext);
                });
                hostBuilder.UseNineChroniclesNode(nineChroniclesProperties, standaloneContext);
                if (rpcServer)
                {
                    hostBuilder.UseNineChroniclesRPC(
                        NineChroniclesNodeServiceProperties
                        .GenerateRpcNodeServiceProperties(rpcListenHost, rpcListenPort)
                    );
                }

                await hostBuilder.RunConsoleAsync(Context.CancellationToken);
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

        private Guid? LoadAWSSinkGuid()
        {
            string path = AWSSinkGuidPath();
            if (!File.Exists(path))
            {
                Console.Error.WriteLine($"AWSSink id doesn't exist. (path: {path})");
                return null;
            }

            string guidString = File.ReadAllText(AWSSinkGuidPath());
            if (Guid.TryParse(guidString, out Guid guid))
            {
                return guid;
            }

            Console.Error.WriteLine($"AWSSink id seems broken. (id: {guidString}");
            return null;
        }

        private void StoreAWSSinkGuid(Guid guid)
        {
            File.WriteAllText(AWSSinkGuidPath(), guid.ToString());
        }

        private string AWSSinkGuidPath()
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "planetarium",
                ".aws_sink_cloudwatch_guid");
        }
    }
}
