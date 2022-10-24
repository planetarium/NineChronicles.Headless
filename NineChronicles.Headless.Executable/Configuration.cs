using System;
using System.IO;
using NineChronicles.Headless.Properties;

namespace NineChronicles.Headless.Executable
{
    public class Configuration
    {
        public class DevConfiguration
        {
            public int BlockInterval { get; set; } = 100;
            public int ReorgInterval { get; set; } = 100;
        }

        public string? AppProtocolVersionString { get; set; }

        public string[]? TrustedAppProtocolVersionSignerStrings { get; set; }

        public string? GenesisBlockPath { get; set; }
        public string? Host { get; set; }
        public ushort? Port { get; set; }

        public string? SwarmPrivateKeyString { get; set; }

        public int Workers { get; set; } = 5;

        // Storage
        public string? StoreType { get; set; }

        public string? StorePath { get; set; } =
            Path.Combine(
                new string[]
                {
                    Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    "AppData", "Local", "planetarium", "9c-main-partition"
                }
            );

        public bool NoReduceStore { get; set; }
        public int StoreStateCacheSize { get; set; } = 100;

        // Miner
        public bool NoMiner { get; set; }
        public int MinerCount { get; set; } = 1;
        public string? MinerPrivateKeyString { get; set; }

        // Networking
        public NetworkType NetworkType { get; set; } = NetworkType.Main;
        public string[]? IceServerStrings { get; set; }
        public string[]? PeerStrings { get; set; }

        // RPC Server
        public bool RpcServer { get; set; }
        public string RpcListenHost { get; set; } = "0.0.0.0";
        public int? RpcListenPort { get; set; }
        public bool? RpcRemoteServer { get; set; }
        public bool? RpcHttpServer { get; set; }

        // GraphQL Server
        public bool GraphQLServer { get; set; }
        public string? GraphQLHost { get; set; }
        public int? GraphQLPort { get; set; }
        public string? GraphQLSecretTokenPath { get; set; }
        public bool NoCors { get; set; }

        // Rendering
        public bool NonblockRenderer { get; set; }
        public int NonblockRendererQueue { get; set; } = 512;
        public bool StrictRendering { get; set; }
        public bool? LogActionRenders { get; set; }

        // Development
        public bool IsDev { get; set; }

        public int BlockInterval
        {
            get => Dev.BlockInterval;
            set => Dev.BlockInterval = value;
        }

        public int ReorgInterval
        {
            get => Dev.ReorgInterval;
            set => Dev.ReorgInterval = value;
        }

        public DevConfiguration Dev { get; } = new();

        // Settings
        public int Confirmations { get; set; }
        public int TxLifeTime { get; set; } = 1000;
        public int MessageTimeout { get; set; } = 60;
        public int TipTimeout { get; set; } = 60;
        public int DemandBuffer { get; set; } = 1150;
        public string[]? StaticPeerStrings { get; set; }
        public bool SkipPreload { get; set; }
        public int MinimumBroadcastTarget { get; set; } = 10;
        public int BucketSize { get; set; } = 16;
        public string ChainTipStaleBehaviorType { get; set; } = "reboot";
        public int TxQuotaPerSigner { get; set; } = 10;
        public int MaximumPollPeers { get; set; } = int.MaxValue;

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
