using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Amazon;
using Amazon.CognitoIdentity;
using Amazon.Runtime;
using Cocona;
using Libplanet;
using Libplanet.KeyStore;
using Microsoft.Extensions.Hosting;
using NineChronicles.Headless.Executable.Commands;
using NineChronicles.Headless.Executable.IO;
using NineChronicles.Headless.Properties;
using Org.BouncyCastle.Security;
using Sentry;
using Serilog;
using Serilog.Sinks.PeriodicBatching;
using NineChroniclesActionType = Libplanet.Action.PolymorphicAction<Nekoyume.Action.ActionBase>;

namespace NineChronicles.Headless.Executable
{
    [HasSubCommands(typeof(ValidationCommand), "validation")]
    public class Program : CoconaLiteConsoleAppBase
    {
        const string SentryDsn = "https://ceac97d4a7d34e7b95e4c445b9b5669e@o195672.ingest.sentry.io/5287621";

        private const string LogTemplate =
            "[{Timestamp:HH:mm:ss} {Level:u3}{SubLevel}] {Message:lj}{NewLine}{Exception}";

        static async Task Main(string[] args)
        {
#if SENTRY || ! DEBUG
            using var _ = SentrySdk.Init(ConfigureSentryOptions);
#endif
            await CoconaLiteApp.Create()
                .ConfigureServices(services => services.AddSingleton<IConsole, StandardConsole>())
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
            bool noMiner = false,
            [Option("app-protocol-version", new[] { 'V' }, Description = "App protocol version token")]
            string appProtocolVersionToken = null,
            [Option('G')]
            string genesisBlockPath = null,
            [Option('H')]
            string host = null,
            [Option('P')]
            ushort? port = null,
            [Option("swarm-private-key",
                Description = "The private key used for signing messages and to specify your node. " +
                              "If you leave this this null, randomly generated value will be used.")]
            string swarmPrivateKeyString = null,
            [Option('D')]
            int minimumDifficulty = 5000000,
            [Option("private-key",
                Description = "The private key used for mining blocks and signing txs. " +
                              "Must not be null if you want to turn on mining with libplanet-node.")]
            string privateKeyString = null,
            string storeType = null,
            string storePath = null,
            [Option("ice-server", new [] { 'I', })]
            string[] iceServerStrings = null,
            [Option("peer")]
            string[] peerStrings = null,
            [Option("trusted-app-protocol-version-signer", new[] { 'T' },
                    Description = "Trustworthy signers who claim new app protocol versions")]
            string[] trustedAppProtocolVersionSigners = null,
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
            string graphQLSecretTokenPath = null,
            [Option(Description = "Run without CORS policy.")]
            bool noCors = false,
            [Option("libplanet-node")]
            bool libplanetNode = false,
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
            [Option(Description = "The log minimum level during headless execution.  debug by default.")]
            string logMinimumLevel = "debug",
            [Option(Description = "The Cognito identity for AWS CloudWatch logging.")]
            string awsCognitoIdentity = null,
            [Option(Description = "The access key for AWS CloudWatch logging.")]
            string awsAccessKey = null,
            [Option(Description = "The secret key for AWS CloudWatch logging.")]
            string awsSecretKey = null,
            [Option(Description = "The AWS region for AWS CloudWatch (e.g., us-east-1, ap-northeast-2).")]
            string awsRegion = null,
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
            int demandBuffer = 1150
        )
        {
#if SENTRY || ! DEBUG
            try
            {
#endif
            
            // Setup logger.
            var loggerConf = new LoggerConfiguration()
                .WriteTo.Console(outputTemplate: LogTemplate)
                .ConfigureMinimumLevel(logMinimumLevel);
#if SENTRY || ! DEBUG
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

            if (useBasicAwsCredentials ^ useCognitoCredentials  && !(awsRegion is null))
            {
                var regionEndpoint = RegionEndpoint.GetBySystemName(awsRegion);
                AWSCredentials credentials = useCognitoCredentials
                    ? (AWSCredentials)new CognitoAWSCredentials(awsCognitoIdentity, regionEndpoint)
                    : (AWSCredentials)new BasicAWSCredentials(awsAccessKey, awsSecretKey);

                var guid = LoadAWSSinkGuid();
                if (guid is null)
                {
                    guid = Guid.NewGuid();
                    StoreAWSSinkGuid(guid.Value);   
                }

                var awsSink = new AWSSink(
                    credentials,
                    regionEndpoint,
                    "9c-standalone-logs",
                    guid.ToString());
                var periodicBatchingSink = new PeriodicBatchingSink(awsSink, new PeriodicBatchingSinkOptions
                {
                    Period = TimeSpan.FromSeconds(2),
                    BatchSizeLimit = 1000,
                });
                loggerConf.WriteTo.Sink(periodicBatchingSink);
            }

            Log.Logger = loggerConf.CreateLogger();

            if (!graphQLServer && !libplanetNode)
            {
                throw new CommandExitedException(
                    "Either --graphql-server or --libplanet-node must be present.",
                    -1
                );
            }

            var tasks = new List<Task>();
            try
            {
                IHostBuilder graphQLHostBuilder = Host.CreateDefaultBuilder();

                var standaloneContext = new StandaloneContext
                {
                    KeyStore = Web3KeyStore.DefaultKeyStore,
                };

                if (graphQLServer)
                {
                    string secretToken = null;
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
                    graphQLHostBuilder =
                        graphQLService.Configure(graphQLHostBuilder, standaloneContext);
                    tasks.Add(graphQLHostBuilder.RunConsoleAsync(Context.CancellationToken));

                    await WaitForGraphQLService(graphQLNodeServiceProperties,
                        Context.CancellationToken);
                }

                if (appProtocolVersionToken is null)
                {
                    throw new CommandExitedException(
                        "--app-protocol-version must be present.",
                        -1
                    );
                }

                if (genesisBlockPath is null)
                {
                    throw new CommandExitedException(
                        "--genesis-block-path must be present.",
                        -1
                    );
                }

                RpcNodeServiceProperties? rpcProperties = null;
                var properties = NineChroniclesNodeServiceProperties
                    .GenerateLibplanetNodeServiceProperties(
                        appProtocolVersionToken,
                        genesisBlockPath,
                        host,
                        port,
                        minimumDifficulty,
                        privateKeyString,
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
                        demandBuffer: demandBuffer);


                if (rpcServer)
                {
                    rpcProperties = NineChroniclesNodeServiceProperties
                        .GenerateRpcNodeServiceProperties(rpcListenHost, rpcListenPort);
                    properties.Render = true;
                    properties.LogActionRenders = true;
                }

                if (logActionRenders)
                {
                    properties.LogActionRenders = true;
                }

                var nineChroniclesProperties = new NineChroniclesNodeServiceProperties()
                {
                    Rpc = rpcProperties,
                    Libplanet = properties
                };

                NineChroniclesNodeService nineChroniclesNodeService =
                    StandaloneServices.CreateHeadless(
                        nineChroniclesProperties,
                        standaloneContext,
                        strictRendering: strictRendering,
                        isDev: isDev,
                        blockInterval: blockInterval,
                        reorgInterval: reorgInterval,
                        authorizedMiner: authorizedMiner,
                        txLifeTime: TimeSpan.FromMinutes(txLifeTime));
                standaloneContext.NineChroniclesNodeService = nineChroniclesNodeService;

                if (libplanetNode)
                {
                    if (!properties.NoMiner)
                    {
                        nineChroniclesNodeService.PrivateKey = properties.PrivateKey;
                        nineChroniclesNodeService.StartMining();
                    }

                    IHostBuilder nineChroniclesNodeHostBuilder = Host.CreateDefaultBuilder();
                    nineChroniclesNodeHostBuilder =
                        nineChroniclesNodeService.Configure(nineChroniclesNodeHostBuilder);
                    tasks.Add(
                        nineChroniclesNodeHostBuilder.RunConsoleAsync(Context.CancellationToken));
                }

                await Task.WhenAll(tasks);
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

        private async Task WaitForGraphQLService(
            GraphQLNodeServiceProperties properties,
            CancellationToken cancellationToken)
        {
            using var httpClient = new HttpClient();
            while (!cancellationToken.IsCancellationRequested)
            {
                Log.Debug("Trying to check GraphQL server started...");
                try
                {
                    await httpClient.GetAsync($"http://{IPAddress.Loopback}:{properties.GraphQLListenPort}/health-check", cancellationToken);
                    break;
                }
                catch (HttpRequestException e)
                {
                    Log.Error(e, "An exception occurred during connecting to GraphQL server. {e}", e);
                }

                await Task.Delay(1000, cancellationToken);
            }
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
