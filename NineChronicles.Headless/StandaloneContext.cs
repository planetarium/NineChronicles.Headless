using System;
using System.Collections.Concurrent;
using System.Reactive.Subjects;
using Libplanet.Crypto;
using Libplanet.Blockchain;
using Libplanet.KeyStore;
using Libplanet.Net;
using Libplanet.Headless;
using Libplanet.Store;
using Nekoyume.Model.State;
using NineChronicles.Headless.GraphTypes;
using NineChronicles.Headless.Utils;

namespace NineChronicles.Headless
{
    public class StandaloneContext
    {
        private BlockChain? _blockChain;
        private IKeyStore? _keyStore;
        private IStore? _store;
        private Swarm? _swarm;

        public BlockChain BlockChain
        {
            get => _blockChain ??
                throw new InvalidOperationException($"{nameof(BlockChain)} property is not set yet.");
            set => _blockChain = value;
        }
        public IKeyStore KeyStore
        {
            get => _keyStore ??
                throw new InvalidOperationException($"{nameof(KeyStore)} property is not set yet.");
            set => _keyStore = value;
        }
        public bool BootstrapEnded { get; set; }
        public bool PreloadEnded { get; set; }
        public bool IsMining { get; set; }
        public ReplaySubject<NodeStatusType> NodeStatusSubject { get; } = new(1);
        public ReplaySubject<BlockSyncState> PreloadStateSubject { get; } = new(5);

        public Subject<DifferentAppProtocolVersionEncounter> DifferentAppProtocolVersionEncounterSubject { get; } =
            new();

        public Subject<Notification> NotificationSubject { get; } = new();
        public Subject<NodeException> NodeExceptionSubject { get; } = new();
        public NineChroniclesNodeService? NineChroniclesNodeService { get; set; }

        public ConcurrentDictionary<Address,
                ReplaySubject<string>>
            AgentAddresses
        { get; } = new ConcurrentDictionary<Address, ReplaySubject<string>>();

        public NodeStatusType NodeStatus => new(this)
        {
            BootstrapEnded = BootstrapEnded,
            PreloadEnded = PreloadEnded,
            IsMining = IsMining,
        };

        public IStore Store
        {
            get => _store ??
                throw new InvalidOperationException($"{nameof(Store)} property is not set yet.");
            internal set => _store = value;
        }

        public Swarm Swarm
        {
            get => _swarm ??
                throw new InvalidOperationException($"{nameof(Swarm)} property is not set yet.");
            internal set => _swarm = value;
        }

        public CurrencyFactory? CurrencyFactory { get; set; }

        public FungibleAssetValueFactory? FungibleAssetValueFactory { get; set; }

        internal TimeSpan DifferentAppProtocolVersionEncounterInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal TimeSpan NotificationInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal TimeSpan NodeExceptionInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal TimeSpan MonsterCollectionStateInterval { get; set; } = TimeSpan.FromSeconds(30);

        internal TimeSpan MonsterCollectionStatusInterval { get; set; } = TimeSpan.FromSeconds(30);
    }
}
