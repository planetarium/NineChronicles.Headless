using NineChronicles.Headless.Properties;

namespace NineChronicles.Headless.Executable
{
    public class Configuration
    {
        public const bool DefaultNoReduceStore = false;
        public const int DefaultStoreStateCacheSize = 100;
        public const bool DefaultNoMiner = false;
        public const int DefaultWorkers = 5;
        public const int DefaultConfirmations = 0;
        public const bool DefaultNonblockRenderer = false;
        public const int DefaultNonblockRendererQueue = 512;
        public const int DefaultMessageTimeout = 60;
        public const int DefaultTipTimeout = 60;
        public const int DefaultDemandBuffer = 1150;
        public const bool DefaultSkipPreload = true;
        public const int DefaultMinimumBroadcastTarget = 10;
        public const int DefaultBucketSize = 16;
        public const string DefaultChainTipStaleBehaviorType = "reboot";
        public const int DefaultMaximumPollPeers = int.MaxValue;
        public const NetworkType DefaultNetworkType = Properties.NetworkType.Main;
        public const bool DefaultIsDev = false;
        public const int DefaultBlockInterval = 100;
        public const int DefaultReorgInterval = 100;
        public const bool DefaultStrictRendering = false;
        public const int DefaultTxLifeTime = 1000;
        public const int DefaultMinerCount = 1;
        public const int DefaultTxQuotaPerSigner = 10;
        public const bool DefaultGraphQLServer = false;
        public const bool DefaultNoCors = false;
        public const bool DefaultRpcServer = false;
        public const string DefaultRpcListenHost = "0.0.0.0";

        public class DevConfiguration
        {
            public int? BlockInterval { get; set; }
            public int? ReorgInterval { get; set; }
        }

        public string? AppProtocolVersionString
        {
            get;
            set;
            // set => AppProtocolVersion = Libplanet.Net.AppProtocolVersion.FromToken(value);
        }

        public AppProtocolVersion? AppProtocolVersion { get; set; }

        public string[]? TrustedAppProtocolVersionSignerStrings
        {
            get;
            set;
            // set => TrustedAppProtocolVersionSigners = value
            // ?.Select(s => new PublicKey(ByteUtil.ParseHex(s)))
            // ?.ToHashSet();
        }

        public HashSet<PublicKey>? TrustedAppProtocolVersionSigners { get; set; }
        public string? GenesisBlockPath { get; set; }
        public string? Host { get; set; }
        public ushort? Port { get; set; }

        public string? SwarmPrivateKeyString
        {
            get;
            set;
            // set => SwarmPrivateKey = string.IsNullOrEmpty(value)
            // ? new PrivateKey()
            // : new PrivateKey(ByteUtil.ParseHex(value));
        }

        public PrivateKey? SwarmPrivateKey { get; set; }
        public int? Workers { get; set; }

        // Storage
        public string? StoreType { get; set; }
        public string? StorePath { get; set; }
        public bool? NoReduceStore { get; set; }

        // Miner
        public bool? NoMiner { get; set; }
        public int? MinerCount { get; set; }
        public string? MinerPrivateKeyString { get; set; }

        // Networking
        public NetworkType? NetworkType { get; set; } = Properties.NetworkType.Main;
        public string[]? IceServerStrings { get; set; }
        public string[]? PeerStrings { get; set; }
        public ImmutableArray<BoundPeer>? Peers { get; set; }

        // RPC Server
        public bool? RpcServer { get; set; }
        public string? RpcListenHost { get; set; }
        public int? RpcListenPort { get; set; }
        public bool? RpcRemoteServer { get; set; }
        public bool? RpcHttpServer { get; set; }

        // GraphQL Server
        public bool? GraphQLServer { get; set; }
        public string? GraphQLHost { get; set; }
        public int? GraphQLPort { get; set; }
        public string? GraphQLSecretTokenPath { get; set; }
        public bool? NoCors { get; set; }

        // Rendering
        public bool? NonblockRenderer { get; set; }
        public int? NonblockRendererQueue { get; set; }
        public bool? StrictRendering { get; set; }
        public bool? LogActionRenders { get; set; }

        // Development
        public bool? IsDev { get; set; }

        public int? BlockInterval
        {
            get => Dev.BlockInterval;
            set => Dev.BlockInterval = value;
        }

        public int? ReorgInterval
        {
            get => Dev.ReorgInterval;
            set => Dev.ReorgInterval = value;
        }

        public DevConfiguration Dev { get; } = new();

        // AWS
        public string? AwsCognitoIdentity { get; set; }
        public string? AwsAccessKey { get; set; }
        public string? AwsSecretKey { get; set; }
        public string? AwsRegion { get; set; }

        // Settings
        public int? Confirmations { get; set; }
        public int? TxLifeTime { get; set; }
        public int? MessageTimeout { get; set; }
        public int? TipTimeout { get; set; }
        public int? DemandBuffer { get; set; }
        public string[]? StaticPeerStrings { get; set; }
        public bool? SkipPreload { get; set; }
        public int? MinimumBroadcastTarget { get; set; }
        public int? BucketSize { get; set; }
        public string? ChainTipStaleBehaviorType { get; set; }
        public int? TxQuotaPerSigner { get; set; }
        public int? MaximumPollPeers { get; set; }

        public void Overwrite(
            string? appProtocolVersionString,
            string[]? trustedAppProtocolVersionSignerStrings,
            string? genesisBlockPath,
            string? host,
            ushort? port,
            string? swarmPrivateKeyString,
            int? workers,
            string? storeType,
            string? storePath,
            bool? noReduceStore,
            bool? noMiner,
            int? minerCount,
            string? minerPrivateKeyString,
            NetworkType? networkType,
            string[]? iceServerStrings,
            string[]? peerStrings,
            bool? rpcServer,
            string? rpcListenHost,
            int? rpcListenPort,
            bool? rpcRemoteServer,
            bool? rpcHttpServer,
            bool? graphQlServer,
            string? graphQLHost,
            int? graphQLPort,
            string? graphQlSecretTokenPath,
            bool? noCors,
            bool? nonblockRenderer,
            int? nonblockRendererQueue,
            bool? strictRendering,
            bool? logActionRenders,
            bool? isDev,
            int? blockInterval,
            int? reorgInterval,
            string? awsCognitoIdentity,
            string? awsAccessKey,
            string? awsSecretKey,
            string? awsRegion,
            int? confirmations,
            int? txLifeTime,
            int? messageTimeout,
            int? tipTimeout,
            int? demandBuffer,
            string[]? staticPeerStrings,
            bool? skipPreload,
            int? minimumBroadcastTarget,
            int? bucketSize,
            string? chainTipStaleBehaviorType,
            int? txQuotaPerSigner,
            int? maximumPollPeers
        )
        {
            AppProtocolVersionString = appProtocolVersionString ?? AppProtocolVersionString;
            TrustedAppProtocolVersionSignerStrings =
                trustedAppProtocolVersionSignerStrings ?? TrustedAppProtocolVersionSignerStrings;
            GenesisBlockPath = genesisBlockPath ?? GenesisBlockPath;
            Host = host ?? Host;
            Port = port ?? Port;
            SwarmPrivateKeyString = swarmPrivateKeyString ?? SwarmPrivateKeyString;
            Workers = workers ?? Workers;
            StoreType = storeType ?? StoreType;
            StorePath = storePath ?? StorePath;
            NoReduceStore = noReduceStore ?? NoReduceStore;
            NoMiner = noMiner ?? NoMiner;
            MinerCount = minerCount ?? MinerCount;
            MinerPrivateKeyString = minerPrivateKeyString ?? MinerPrivateKeyString;
            NetworkType = networkType ?? NetworkType;
            IceServerStrings = iceServerStrings ?? IceServerStrings;
            PeerStrings = peerStrings ?? PeerStrings;
            RpcServer = rpcServer ?? RpcServer;
            RpcListenHost = rpcListenHost ?? RpcListenHost;
            RpcListenPort = rpcListenPort ?? RpcListenPort;
            RpcRemoteServer = rpcRemoteServer ?? RpcRemoteServer;
            RpcHttpServer = rpcHttpServer ?? RpcHttpServer;
            GraphQLServer = graphQlServer ?? GraphQLServer;
            GraphQLHost = graphQLHost ?? GraphQLHost;
            GraphQLPort = graphQLPort ?? GraphQLPort;
            GraphQLSecretTokenPath = graphQlSecretTokenPath ?? GraphQLSecretTokenPath;
            NoCors = noCors ?? NoCors;
            NonblockRenderer = nonblockRenderer ?? NonblockRenderer;
            NonblockRendererQueue = nonblockRendererQueue ?? NonblockRendererQueue;
            StrictRendering = strictRendering ?? StrictRendering;
            LogActionRenders = logActionRenders ?? LogActionRenders;
            IsDev = isDev ?? IsDev;
            Dev.BlockInterval = blockInterval ?? Dev.BlockInterval;
            Dev.ReorgInterval = reorgInterval ?? Dev.ReorgInterval;
            AwsCognitoIdentity = awsCognitoIdentity ?? AwsCognitoIdentity;
            AwsAccessKey = awsAccessKey ?? AwsAccessKey;
            AwsSecretKey = awsSecretKey ?? AwsSecretKey;
            AwsRegion = awsRegion ?? AwsRegion;
            Confirmations = confirmations ?? Confirmations;
            TxLifeTime = txLifeTime ?? TxLifeTime;
            MessageTimeout = messageTimeout ?? MessageTimeout;
            TipTimeout = tipTimeout ?? TipTimeout;
            DemandBuffer = demandBuffer ?? DemandBuffer;
            StaticPeerStrings = staticPeerStrings ?? StaticPeerStrings;
            SkipPreload = skipPreload ?? SkipPreload;
            MinimumBroadcastTarget = minimumBroadcastTarget ?? MinimumBroadcastTarget;
            BucketSize = bucketSize ?? BucketSize;
            ChainTipStaleBehaviorType = chainTipStaleBehaviorType ?? ChainTipStaleBehaviorType;
            TxQuotaPerSigner = txQuotaPerSigner ?? TxQuotaPerSigner;
            MaximumPollPeers = maximumPollPeers ?? MaximumPollPeers;
        }
    }
}
