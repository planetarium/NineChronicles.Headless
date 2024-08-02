using System;
using System.IO;
using Libplanet.Headless;
using Nekoyume;
using NineChronicles.Headless.Properties;

namespace NineChronicles.Headless.Executable
{
    public class Configuration
    {
        public string? AppProtocolVersionString { get; set; }

        public string[]? TrustedAppProtocolVersionSignerStrings { get; set; }

        public string? GenesisBlockPath { get; set; }
        public string? Host { get; set; }
        public ushort? Port { get; set; }

        public string? SwarmPrivateKeyString { get; set; }

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
        public int MinerBlockIntervalMilliseconds { get; set; }

        public Planet Planet { get; set; } = Planet.Odin;

        // Networking
        public string[]? IceServerStrings { get; set; }
        public string[]? PeerStrings { get; set; }

        // RPC Server
        public bool RpcServer { get; set; }
        public string RpcListenHost { get; set; } = "0.0.0.0";
        public int? RpcListenPort { get; set; }
        public bool? RpcRemoteServer { get; set; }
        public bool? RpcHttpServer { get; set; }

        // RemoteKeyValueService
        public bool RemoteKeyValueService { get; set; } = false;

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

        // Settings
        public int Confirmations { get; set; }
        public int TxLifeTime { get; set; } = 1000;
        public int MessageTimeout { get; set; } = 60;
        public int TipTimeout { get; set; } = 60;
        public int DemandBuffer { get; set; } = 1150;
        public bool SkipPreload { get; set; }
        public int MinimumBroadcastTarget { get; set; } = 10;
        public int BucketSize { get; set; } = 16;
        public string ChainTipStaleBehaviorType { get; set; } = "reboot";
        public int TxQuotaPerSigner { get; set; } = 10;
        public int MaximumPollPeers { get; set; } = int.MaxValue;
        public ActionTypeLoaderConfiguration? ActionTypeLoader { get; set; } = null;

        // Consensus
        public string? ConsensusPrivateKeyString { get; set; }
        public string[]? ConsensusSeedStrings { get; set; }
        public ushort? ConsensusPort { get; set; }
        public double? ConsensusTargetBlockIntervalMilliseconds { get; set; }
        public int? ConsensusProposeSecondBase { get; set; }

        public int? MaxTransactionPerBlock { get; set; }

        public AccessControlServiceOptions? AccessControlService { get; set; }

        public int ArenaParticipantsSyncInterval { get; set; } = 1000;

        public void Overwrite(
            string? appProtocolVersionString,
            string[]? trustedAppProtocolVersionSignerStrings,
            string? genesisBlockPath,
            string? host,
            ushort? port,
            string? swarmPrivateKeyString,
            string? storeType,
            string? storePath,
            bool? noReduceStore,
            bool? noMiner,
            int? minerCount,
            string? minerPrivateKeyString,
            int? minerBlockIntervalMilliseconds,
            Planet? planet,
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
            int? confirmations,
            int? txLifeTime,
            int? messageTimeout,
            int? tipTimeout,
            int? demandBuffer,
            bool? skipPreload,
            int? minimumBroadcastTarget,
            int? bucketSize,
            string? chainTipStaleBehaviorType,
            int? txQuotaPerSigner,
            int? maximumPollPeers,
            ushort? consensusPort,
            string? consensusPrivateKeyString,
            string[]? consensusSeedStrings,
            double? consensusTargetBlockIntervalMilliseconds,
            int? consensusProposeSecondBase,
            int? maxTransactionPerBlock,
            int? arenaParticipantsSyncInterval,
            bool? remoteKeyValueService
        )
        {
            AppProtocolVersionString = appProtocolVersionString ?? AppProtocolVersionString;
            TrustedAppProtocolVersionSignerStrings =
                trustedAppProtocolVersionSignerStrings ?? TrustedAppProtocolVersionSignerStrings;
            GenesisBlockPath = genesisBlockPath ?? GenesisBlockPath;
            Host = host ?? Host;
            Port = port ?? Port;
            SwarmPrivateKeyString = swarmPrivateKeyString ?? SwarmPrivateKeyString;
            StoreType = storeType ?? StoreType;
            StorePath = storePath ?? StorePath;
            NoReduceStore = noReduceStore ?? NoReduceStore;
            NoMiner = noMiner ?? NoMiner;
            MinerCount = minerCount ?? MinerCount;
            MinerPrivateKeyString = minerPrivateKeyString ?? MinerPrivateKeyString;
            MinerBlockIntervalMilliseconds = minerBlockIntervalMilliseconds ?? MinerBlockIntervalMilliseconds;
            Planet = planet ?? Planet;
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
            Confirmations = confirmations ?? Confirmations;
            TxLifeTime = txLifeTime ?? TxLifeTime;
            MessageTimeout = messageTimeout ?? MessageTimeout;
            TipTimeout = tipTimeout ?? TipTimeout;
            DemandBuffer = demandBuffer ?? DemandBuffer;
            SkipPreload = skipPreload ?? SkipPreload;
            MinimumBroadcastTarget = minimumBroadcastTarget ?? MinimumBroadcastTarget;
            BucketSize = bucketSize ?? BucketSize;
            ChainTipStaleBehaviorType = chainTipStaleBehaviorType ?? ChainTipStaleBehaviorType;
            TxQuotaPerSigner = txQuotaPerSigner ?? TxQuotaPerSigner;
            MaximumPollPeers = maximumPollPeers ?? MaximumPollPeers;
            ConsensusPort = consensusPort ?? ConsensusPort;
            ConsensusSeedStrings = consensusSeedStrings ?? ConsensusSeedStrings;
            ConsensusPrivateKeyString = consensusPrivateKeyString ?? ConsensusPrivateKeyString;
            ConsensusTargetBlockIntervalMilliseconds = consensusTargetBlockIntervalMilliseconds ?? ConsensusTargetBlockIntervalMilliseconds;
            ConsensusProposeSecondBase = consensusProposeSecondBase ?? ConsensusProposeSecondBase;
            MaxTransactionPerBlock = maxTransactionPerBlock ?? MaxTransactionPerBlock;
            ArenaParticipantsSyncInterval = arenaParticipantsSyncInterval ?? ArenaParticipantsSyncInterval;
            RemoteKeyValueService = remoteKeyValueService ?? RemoteKeyValueService;
        }
    }
}
